using PurrNet.Prediction;
using UnityEngine;

namespace VerdantHunt.Player
{
    public struct PlayerState : IPredictedData<PlayerState>
    {
        public Vector3 position;
        public Vector3 velocity;
        public float stamina;
        public float health;
        public int arrowCount;
        public float drawStrength;
        public float horizontalSpeed;
        public float moveDirX;
        public float moveDirY;
        public bool isCrouching;
        public bool isSprinting;

        public void Dispose() { }
    }
}
