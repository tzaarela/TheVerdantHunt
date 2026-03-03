using PurrNet.Prediction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VerdantHunt.Player
{
    public class PredictedPlayer : PredictedIdentity<PlayerInput, PlayerState>
    {
        [Header("Config")]
        [SerializeField] MovementConfig config;

        [Header("Input Actions")]
        [SerializeField] InputActionReference moveAction;
        [SerializeField] InputActionReference sprintAction;
        [SerializeField] InputActionReference crouchAction;
        [SerializeField] InputActionReference attackAction;
        [SerializeField] InputActionReference interactAction;

        [Header("References")]
        [SerializeField] Animator animator;
        [SerializeField] Transform cameraTarget;
        [SerializeField] PlayerCameraController playerCameraController;
        [SerializeField] PredictedRigidbody predictedRigidbody;

        // Animator parameter hashes
        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
        static readonly int IsSprintingHash = Animator.StringToHash("IsSprinting");
        static readonly int MoveDirXHash = Animator.StringToHash("MoveDirX");
        static readonly int MoveDirYHash = Animator.StringToHash("MoveDirY");

		private void Awake()
		{
            Application.targetFrameRate = 120;

			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}

		protected override void LateAwake()
		{
			base.LateAwake();

            if (isOwner)
            {
                playerCameraController.Init();
            }
		}

		void OnEnable()
        {
            if (isOwner) EnableActions(true);
        }

        void OnDisable()
        {
            if (isOwner) EnableActions(false);
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

            input.cameraForward = playerCameraController.Forward;
        }

        // --- Input Validation ---

        protected override void SanitizeInput(ref PlayerInput input)
        {
            input.moveDir = Vector2.ClampMagnitude(input.moveDir, 1f);
        }

        // --- Extrapolation (missing remote input) ---

        protected override void ModifyExtrapolatedInput(ref PlayerInput input)
        {
            //input.moveDir *= 0.4f;
            //input.releaseBow = false;
            //input.melee = false;
            //input.interact = false;
        }

        // --- Core Simulation (deterministic, every tick) ---

        protected override void Simulate(PlayerInput input, ref PlayerState state, float delta)
        {
            // Movement direction relative to facing
            Vector3 direction = (transform.forward * input.moveDir.y + transform.right * input.moveDir.x); 

            // Speed selection
            state.isSprinting = input.sprint && state.stamina > 0f && !input.crouch;
            state.isCrouching = input.crouch;

            float speed = state.isSprinting ? config.sprintSpeed : config.walkSpeed;
            if (state.isCrouching)
                speed = config.crouchSpeed;

            // Store movement info for animations
            state.horizontalSpeed = direction.magnitude * speed;
            state.moveDirX = input.moveDir.x;
            state.moveDirY = input.moveDir.y;

            // Final movement
            predictedRigidbody.AddForce(direction * speed * config.acceleration);

            if (input.cameraForward != Vector3.zero)
            {
                var camForward = input.cameraForward;
                camForward.y = 0f;
                if (camForward.magnitude > 0.0001f)
                {
                    predictedRigidbody.MoveRotation(Quaternion.LookRotation(camForward.normalized));
				}
			}

            // Stamina
            if (state.isSprinting)
                state.stamina = Mathf.Max(0f, state.stamina - config.staminaSprintDrain * delta);
            else
                state.stamina = Mathf.Min(config.maxStamina, state.stamina + config.staminaRegenRate * delta);
        }

        // --- View (every visual frame, NOT during rollback) ---

        protected override void UpdateView(PlayerState viewState, PlayerState? verified)
        {
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
