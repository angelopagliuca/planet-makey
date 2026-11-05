using UnityEngine;

namespace PlanetMakey.Game.Ufo
{
    /// <summary>
    /// Works out how high a point on a vertical line must be for a perspective camera to show it at a
    /// given screen height. Makey uses it to keep his hover height inside the flight band: the band is
    /// set on the screen (0 at the bottom, 1 at the top), but he moves in world units.
    /// </summary>
    /// <remarks>
    /// Plain math on numbers taken from the camera, so it can be tested without a scene. The screen
    /// height of a point is a linear function of its world height, so the answer is a closed form with
    /// no iteration.
    /// </remarks>
    public static class HoverHeightSolver
    {
        // Below this the equation has no usable solution: the camera looks straight along the line.
        private const float MinDivisor = 1e-5f;

        // A point must be at least this far in front of the camera to be on screen.
        private const float MinDepth = 1e-3f;

        /// <summary>
        /// The share of the hover height range to use over a cow at a given distance from the camera:
        /// <paramref name="nearScale"/> at or nearer than <paramref name="nearDepth"/>, 1 at or beyond
        /// <paramref name="farDepth"/>, and a straight line between. The same world height looks higher on
        /// screen over a near cow, so a near cow gets a smaller share.
        /// </summary>
        /// <param name="depth">The cow's distance from the camera along its view direction.</param>
        /// <param name="nearDepth">The distance at or below which the near share applies.</param>
        /// <param name="farDepth">The distance at or beyond which the whole range applies.</param>
        /// <param name="nearScale">The share used for a near cow, 0 to 1.</param>
        public static float RangeScale(float depth, float nearDepth, float farDepth, float nearScale)
        {
            if (farDepth <= nearDepth)
            {
                // No slope to walk along: the share jumps from the near value to 1 at the far distance.
                return depth >= farDepth ? 1f : nearScale;
            }

            float along = Mathf.InverseLerp(nearDepth, farDepth, depth);
            return Mathf.Lerp(nearScale, 1f, along);
        }

        /// <summary>
        /// Finds the world height at which the point above (or below) <paramref name="pointOnVerticalLine"/>
        /// appears at <paramref name="viewportY"/>. Only the x and z of the point are used.
        /// </summary>
        /// <param name="cameraPosition">The camera's position in world space.</param>
        /// <param name="cameraForward">The camera's forward direction (unit length).</param>
        /// <param name="cameraUp">The camera's up direction (unit length).</param>
        /// <param name="verticalFovDegrees">The camera's vertical field of view in degrees.</param>
        /// <param name="pointOnVerticalLine">Any point on the vertical line; its x and z set the line.</param>
        /// <param name="viewportY">The wanted screen height: 0 at the bottom, 1 at the top.</param>
        /// <param name="worldY">The world height that gives it, when this returns true.</param>
        /// <returns>
        /// False when there is no answer: a bad field of view or screen height, a camera looking along the
        /// line, or a result behind the camera.
        /// </returns>
        public static bool TryHeightAtViewportY(
            Vector3 cameraPosition,
            Vector3 cameraForward,
            Vector3 cameraUp,
            float verticalFovDegrees,
            Vector3 pointOnVerticalLine,
            float viewportY,
            out float worldY)
        {
            worldY = 0f;

            if (float.IsNaN(viewportY) || float.IsInfinity(viewportY))
            {
                return false;
            }

            if (float.IsNaN(verticalFovDegrees) || verticalFovDegrees <= 0f || verticalFovDegrees >= 180f)
            {
                return false;
            }

            // How far the wanted screen height is from the middle, in units of "depth times
            // tan(half fov)": a point at screen height v satisfies up = k * forward, with k = (2v - 1) * tan.
            float tan = Mathf.Tan(verticalFovDegrees * 0.5f * Mathf.Deg2Rad);
            float k = (2f * viewportY - 1f) * tan;

            // The horizontal part of the vector from the camera to the line, which does not change with height.
            Vector3 horizontal = new Vector3(
                pointOnVerticalLine.x - cameraPosition.x,
                0f,
                pointOnVerticalLine.z - cameraPosition.z);

            float divisor = cameraUp.y - k * cameraForward.y;
            if (Mathf.Abs(divisor) < MinDivisor)
            {
                return false;
            }

            float heightAboveCamera = (k * Vector3.Dot(horizontal, cameraForward) - Vector3.Dot(horizontal, cameraUp)) / divisor;

            float depth = heightAboveCamera * cameraForward.y + Vector3.Dot(horizontal, cameraForward);
            if (depth <= MinDepth)
            {
                return false;
            }

            worldY = cameraPosition.y + heightAboveCamera;
            return true;
        }
    }
}
