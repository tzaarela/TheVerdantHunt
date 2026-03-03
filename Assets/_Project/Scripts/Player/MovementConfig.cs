using UnityEngine;

namespace VerdantHunt.Player
{
    [CreateAssetMenu(fileName = "MovementConfig", menuName = "VerdantHunt/Movement Config")]
    public class MovementConfig : ScriptableObject
    {
        [Header("Speed")]
        public float walkSpeed = 2.5f;
        public float sprintSpeed = 5f;
        public float crouchSpeed = 1.5f;

        [Header("Physics")]
        public float acceleration = 30f;
        public float linearDrag = 5f;
        public float gravity = -15f;
        public float groundedDownForce = -0.5f;

        [Header("Look")]
        public float mouseSensitivity = 0.15f;

        [Header("Stamina")]
        public float maxStamina = 100f;
        public float staminaSprintDrain = 15f;
        public float staminaRegenRate = 10f;

        [Header("Animation")]
        public float animationDampTime = 0.1f;
    }
}
