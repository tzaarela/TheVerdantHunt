using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VerdantHunt.Player
{
    /// <summary>
    /// Local-only camera controller for the owning player.
    /// Manages yaw and pitch visually — neither is predicted.
    /// </summary>
    public class PlayerCameraController : MonoBehaviour
    {
        [SerializeField] MovementConfig config;
        [SerializeField] float minPitch = -60f;
        [SerializeField] float maxPitch = 60f;
        [SerializeField] InputActionReference lookAction;
        [SerializeField] CinemachineCamera cinemachineCamera;

        Transform cameraTarget;
        PredictedPlayer _player;
        float _pitch;
        float _yaw;
        bool _initialized;

        public float Yaw => _yaw;

        public static PlayerCameraController Instance;

        void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Debug.LogWarning("Multiple PlayerCameraController instances found. This may cause issues with the static Instance reference.");
        }

        void LateUpdate()
        {
            if (_player == null)
                return;

            if (!_initialized)
            {
                _yaw = _player.transform.eulerAngles.y;
                _initialized = true;
            }

            var lookDelta = lookAction?.action?.ReadValue<Vector2>() ?? Vector2.zero;

            _yaw += lookDelta.x * config.mouseSensitivity;

            _pitch -= lookDelta.y * config.mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            // Apply yaw to character transform
            _player.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

            // Apply pitch to camera target
            if (cameraTarget != null)
                cameraTarget.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        public void SetPredictedPlayer(PredictedPlayer player)
        {
            _player = player;
		}

        public void SetCameraTarget(Transform target)
        {
            cameraTarget = target;
            cinemachineCamera.Follow = cameraTarget;
		}
    }
}
