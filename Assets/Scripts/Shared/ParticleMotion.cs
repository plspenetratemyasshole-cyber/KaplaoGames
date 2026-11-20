using UnityEngine;

namespace KaplaoGames.Shared
{
    public static class ParticleMotion
    {
        /// <summary>
        /// Radial/tangential acceleration relative to the emitter origin (local position).
        /// Tangential force swirls particles around the emitter; only applied once the particle
        /// has moved away from the origin (avoids velocity-flip artifacts at spawn).
        /// </summary>
        public static Vector2 ComputeAcceleration(
            Vector2 localPosition,
            Vector2 gravity,
            float accelRad,
            float accelTan)
        {
            Vector2 accel = gravity;

            if (localPosition.sqrMagnitude > 0.0001f)
            {
                Vector2 radial = localPosition.normalized;
                Vector2 tangent = new Vector2(-radial.y, radial.x);
                accel += radial * accelRad;
                accel += tangent * accelTan;
            }

            return accel;
        }
    }
}
