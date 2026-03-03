using UnityEngine;
using UnityEngine.InputSystem;

namespace VerdantHunt.Player
{
    /// <summary>
    /// Local-only camera controller. Manages both yaw and pitch visually.
    /// Yaw is NOT applied through prediction/reconciliation to avoid mouse jitter.
    /// Pitch is local-only (never sent over the network).
    /// Only active for the owning player; remote players use state.yaw from prediction.
    /// </summary>
    public class PlayerCameraController : MonoBehaviour
    {
        [SerializeField] MovementConfig config;
        [SerializeField] float minPitch = -60f;
        [SerializeField] float maxPitch = 60f;
        [SerializeField] Transform cameraTarget;
        [SerializeField] InputActionReference lookAction;

        PredictedPlayer _player;
        float _pitch;
        float _yaw;
        bool _initialized;

        void Awake()
        {
            _player = GetComponent<PredictedPlayer>();
        }

        void LateUpdate()
        {
            if (_player == null)
                return;

            if (_player.isOwner)
            {
                // Initialize local yaw from the predicted state on first frame
                if (!_initialized)
                {
                    _yaw = transform.eulerAngles.y;
                    _initialized = true;
                }

                var lookDelta = lookAction?.action?.ReadValue<Vector2>() ?? Vector2.zero;

                // Yaw — applied locally for instant response, no reconciliation jitter
                _yaw += lookDelta.x * config.mouseSensitivity;

                // Pitch — local only, never predicted
                _pitch -= lookDelta.y * config.mouseSensitivity;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

                // Apply yaw to character transform
                transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

                // Apply pitch to camera target
                if (cameraTarget != null)
                    cameraTarget.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
            else
            {
                // Remote players: apply yaw from predicted state (via UpdateView on PredictedPlayer)
                // We read viewState.yaw indirectly — PredictedTransform handles interpolation,
                // but since we removed yaw from UpdateView, apply it here from the state.
                transform.rotation = Quaternion.Euler(0f, _player.currentState.yaw, 0f);
            }
        }
    }
}
