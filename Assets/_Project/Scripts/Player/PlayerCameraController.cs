using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VerdantHunt.Player
{
    /// <summary>
    /// Local-only camera controller for the owning player.
    /// Manages yaw and pitch visually — neither is predicted.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerCameraController : MonoBehaviour
    {
        [SerializeField] MovementConfig config;
        [SerializeField] float minPitch = -60f;
        [SerializeField] float maxPitch = 60f;
        [SerializeField] InputActionReference lookAction;
        [SerializeField] CinemachineCamera cinemachineCamera;

        Transform _cameraTarget;
        Vector2 _currentRotation;
        bool _initialized;

        public Vector3 Forward => Quaternion.Euler(_currentRotation.x, _currentRotation.y, 0f) * Vector3.forward;

		private void Awake()
        {
            cinemachineCamera.Priority.Value = -1;
        }

        public void Init()
        {

            cinemachineCamera.Priority.Value = 10;
            _initialized = true;
        }

        public void LateUpdate()
        {
            if (!_initialized) return;

            Vector2 mouseDelta = lookAction?.action?.ReadValue<Vector2>() ?? Vector2.zero;

            var mouseX = mouseDelta.x * config.mouseSensitivity;
            var mouseY = mouseDelta.y * config.mouseSensitivity;
            _currentRotation.x = Mathf.Clamp(_currentRotation.x - mouseY, minPitch, maxPitch);
            _currentRotation.y += mouseX;

            transform.localRotation = Quaternion.Euler(_currentRotation.x, 0f, 0f);
		}
        
        public void SetCameraTarget(Transform target)
        {
            _cameraTarget = target;
            cinemachineCamera.Follow = _cameraTarget;
		}
    }
}
