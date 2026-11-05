using UnityEngine;

namespace PlanetMakey.Game.Ufo
{
    /// <summary>
    /// Small pieces of math behind Makey's lively look, kept apart from <see cref="MakeyView"/> so they
    /// can be tested without a scene.
    /// </summary>
    public static class MakeyMotion
    {
        // Below this a direction on the ground has no usable heading.
        private const float MinFlatLength = 1e-4f;

        /// <summary>
        /// How far to turn Makey around the up axis, in degrees, so that he faces the camera instead of
        /// the direction the camera looks. When he stands straight in front of the camera the answer
        /// is 0; the further to one side he is, the more he turns to keep looking at it.
        /// </summary>
        /// <remarks>
        /// It assumes his model faces the camera when he is straight in front of it, which is how the
        /// scene is set up. Applying the result with <c>Quaternion.AngleAxis(degrees, Vector3.up)</c>
        /// turns that resting front onto the direction to the camera.
        /// </remarks>
        /// <param name="cameraPosition">The camera's position.</param>
        /// <param name="cameraForward">The camera's forward direction.</param>
        /// <param name="makeyPosition">Makey's position.</param>
        public static float FacingYawDegrees(Vector3 cameraPosition, Vector3 cameraForward, Vector3 makeyPosition)
        {
            Vector3 restFront = -Flat(cameraForward);
            Vector3 toCamera = Flat(cameraPosition - makeyPosition);

            if (restFront.sqrMagnitude < MinFlatLength || toCamera.sqrMagnitude < MinFlatLength)
            {
                return 0f;
            }

            return Vector3.SignedAngle(restFront, toCamera, Vector3.up);
        }

        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction;
        }

        /// <summary>
        /// A value that follows a target like a weight on a spring. With little damping it overshoots
        /// the target and settles, which is what makes a leaning ship look as if it has weight.
        /// </summary>
        public struct Spring
        {
            // The longest time step used. A slow frame is not allowed to blow the spring up.
            private const float MaxStep = 0.05f;

            /// <summary>Where the spring is now.</summary>
            public float Value { get; private set; }

            /// <summary>How fast it is moving.</summary>
            public float Velocity { get; private set; }

            /// <summary>Makes a spring that starts at a value with a speed.</summary>
            public Spring(float value, float velocity)
            {
                Value = value;
                Velocity = velocity;
            }

            /// <summary>Moves the spring towards <paramref name="target"/> for one frame.</summary>
            /// <param name="target">The value it is pulled towards.</param>
            /// <param name="stiffness">How hard it is pulled. Higher is quicker.</param>
            /// <param name="damping">How much movement is held back. About 0.45 times twice the square root of the stiffness overshoots a little.</param>
            /// <param name="deltaTime">The frame's time in seconds.</param>
            public void Step(float target, float stiffness, float damping, float deltaTime)
            {
                float dt = Mathf.Min(deltaTime, MaxStep);
                if (dt <= 0f)
                {
                    return;
                }

                Velocity += (stiffness * (target - Value) - damping * Velocity) * dt;
                Value += Velocity * dt;
            }

            /// <summary>Puts the spring at rest at zero.</summary>
            public void Reset()
            {
                Value = 0f;
                Velocity = 0f;
            }
        }
    }
}
