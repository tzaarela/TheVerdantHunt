using PurrNet.Prediction;
using UnityEngine;

namespace VerdantHunt.Player
{
    public struct PlayerInput : IPredictedData
    {
        // Continuous (set in GetFinalInput)
        public Vector2 moveDir;
        public float yaw; // absolute yaw from camera controller (not a delta)
        public bool sprint;
        public bool crouch;
        public bool drawBow;

        // Edge-triggered (use |= in UpdateInput)
        public bool releaseBow;
        public bool melee;
        public bool interact;

        public void Dispose() { }
    }
}
