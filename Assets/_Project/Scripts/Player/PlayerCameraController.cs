using UnityEngine;
using UnityEngine.InputSystem;

namespace VerdantHunt.Player
{
    /// <summary>
    /// Local-only camera controller. Manages pitch (vertical look).
    /// Yaw is driven by the predicted character rotation.
    /// Only active for the owning player.
    /// </summary>
    public class PlayerCameraController : MonoBehaviour
    {
        [SerializeField] float pitchSpeed = 0.15f;
        [SerializeField] float minPitch = -60f;
        [SerializeField] float maxPitch = 60f;
        [SerializeField] Transform cameraTarget;
        [SerializeField] InputActionReference lookAction;

        PredictedPlayer _player;
        float _pitch;

        void Awake()
        {
            _player = GetComponent<PredictedPlayer>();
        }

        void LateUpdate()
        {
            if (_player == null || !_player.isOwner)
                return;

            var lookDelta = lookAction?.action?.ReadValue<Vector2>() ?? Vector2.zero;
            _pitch -= lookDelta.y * pitchSpeed;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            if (cameraTarget != null)
                cameraTarget.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }
}
