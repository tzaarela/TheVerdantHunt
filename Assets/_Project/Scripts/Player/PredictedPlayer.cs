using PurrNet.Prediction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VerdantHunt.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PredictedPlayer : PredictedIdentity<PlayerInput, PlayerState>
    {
        [Header("Config")]
        [SerializeField] MovementConfig config;

        [Header("Input Actions")]
        [SerializeField] InputActionReference moveAction;
        [SerializeField] InputActionReference lookAction;
        [SerializeField] InputActionReference sprintAction;
        [SerializeField] InputActionReference crouchAction;
        [SerializeField] InputActionReference attackAction;
        [SerializeField] InputActionReference interactAction;

        [Header("References")]
        [SerializeField] Animator animator;

        CharacterController _cc;
        float _accumulatedYaw;

        // Animator parameter hashes
        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
        static readonly int IsSprintingHash = Animator.StringToHash("IsSprinting");
        static readonly int MoveDirXHash = Animator.StringToHash("MoveDirX");
        static readonly int MoveDirYHash = Animator.StringToHash("MoveDirY");

        protected void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        void OnEnable()
        {
            EnableActions(true);
        }

        void OnDisable()
        {
            EnableActions(false);
        }

        void EnableActions(bool enable)
        {
            if (moveAction == null) return;

            var asset = moveAction.asset;
            if (asset == null) return;

            if (enable)
                asset.Enable();
            else
                asset.Disable();
        }

        // --- Initial State ---

        protected override PlayerState GetInitialState() => new PlayerState
        {
            position = transform.position,
            yaw = transform.eulerAngles.y,
            health = 100f,
            stamina = config.maxStamina,
            arrowCount = 10,
        };

        // --- Unity Bridging ---

        protected override void GetUnityState(ref PlayerState state)
        {
            state.position = transform.position;
        }

        protected override void SetUnityState(PlayerState state)
        {
            transform.position = state.position;
        }

        // --- Input (every Unity frame) ---

        protected override void UpdateInput(ref PlayerInput input)
        {
            // Accumulate mouse X delta between ticks
            var lookDelta = lookAction?.action?.ReadValue<Vector2>() ?? Vector2.zero;
            _accumulatedYaw += lookDelta.x;

            // Edge-triggered: |= so we don't miss presses between ticks
            if (attackAction?.action != null && attackAction.action.WasReleasedThisFrame())
                input.releaseBow |= true;
            if (interactAction?.action != null && interactAction.action.WasPressedThisFrame())
                input.interact |= true;
        }

        // --- Input (once per tick) ---

        protected override void GetFinalInput(ref PlayerInput input)
        {
            // Continuous inputs — read current state
            input.moveDir = moveAction?.action?.ReadValue<Vector2>() ?? Vector2.zero;
            input.sprint = sprintAction?.action?.IsPressed() ?? false;
            input.crouch = crouchAction?.action?.IsPressed() ?? false;
            input.drawBow = attackAction?.action?.IsPressed() ?? false;

            // Transfer accumulated yaw and reset
            input.lookYaw = _accumulatedYaw;
            _accumulatedYaw = 0f;
        }

        // --- Input Validation ---

        protected override void SanitizeInput(ref PlayerInput input)
        {
            input.moveDir = Vector2.ClampMagnitude(input.moveDir, 1f);
            input.lookYaw = Mathf.Clamp(input.lookYaw, -180f, 180f);
        }

        // --- Extrapolation (missing remote input) ---

        protected override void ModifyExtrapolatedInput(ref PlayerInput input)
        {
            input.moveDir *= 0.4f;
            input.releaseBow = false;
            input.melee = false;
            input.interact = false;
        }

        // --- Core Simulation (deterministic, every tick) ---

        protected override void Simulate(PlayerInput input, ref PlayerState state, float delta)
        {
            // Rotation
            state.yaw += input.lookYaw * config.mouseSensitivity;

            // Movement direction relative to facing
            float yawRad = state.yaw * Mathf.Deg2Rad;
            var forward = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
            var right = new Vector3(forward.z, 0f, -forward.x);
            var moveWorld = Vector3.ClampMagnitude(
                forward * input.moveDir.y + right * input.moveDir.x, 1f);

            // Speed selection
            state.isSprinting = input.sprint && state.stamina > 0f && !input.crouch;
            state.isCrouching = input.crouch;

            float speed = state.isSprinting ? config.sprintSpeed : config.walkSpeed;
            if (state.isCrouching)
                speed = config.crouchSpeed;

            // Gravity
            if (_cc.isGrounded && state.velocity.y < 0f)
                state.velocity.y = config.groundedDownForce;
            state.velocity.y += config.gravity * delta;

            // Store movement info for animations
            state.horizontalSpeed = moveWorld.magnitude * speed;
            state.moveDirX = input.moveDir.x;
            state.moveDirY = input.moveDir.y;

            // Final movement
            var finalMove = (moveWorld * speed) + (Vector3.up * state.velocity.y);
            _cc.Move(finalMove * delta);

            // Read position back from CharacterController
            state.position = transform.position;

            // Stamina
            if (state.isSprinting)
                state.stamina = Mathf.Max(0f, state.stamina - config.staminaSprintDrain * delta);
            else
                state.stamina = Mathf.Min(config.maxStamina, state.stamina + config.staminaRegenRate * delta);
        }

        // --- View (every visual frame, NOT during rollback) ---

        protected override void UpdateView(PlayerState viewState, PlayerState? verified)
        {
            // Yaw rotation is applied locally by PlayerCameraController (not predicted)
            // to avoid reconciliation jitter on mouse look.

            if (animator != null)
            {
                float normalizedSpeed = Mathf.Clamp01(viewState.horizontalSpeed / config.sprintSpeed);
                animator.SetFloat(SpeedHash, normalizedSpeed, config.animationDampTime, Time.deltaTime);
                animator.SetFloat(MoveDirXHash, viewState.moveDirX, config.animationDampTime, Time.deltaTime);
                animator.SetFloat(MoveDirYHash, viewState.moveDirY, config.animationDampTime, Time.deltaTime);
                animator.SetBool(IsCrouchingHash, viewState.isCrouching);
                animator.SetBool(IsSprintingHash, viewState.isSprinting);
            }
        }
    }
}
