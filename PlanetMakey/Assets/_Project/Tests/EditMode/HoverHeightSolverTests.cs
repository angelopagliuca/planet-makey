using NUnit.Framework;
using PlanetMakey.Game.Ufo;
using UnityEngine;

namespace PlanetMakey.Tests.EditMode
{
    public sealed class HoverHeightSolverTests
    {
        private const float Tolerance = 1e-3f;

        [Test]
        public void LevelCamera_MiddleOfTheScreen_IsTheCameraHeight()
        {
            bool found = HoverHeightSolver.TryHeightAtViewportY(
                new Vector3(0f, 3f, 0f), Vector3.forward, Vector3.up, 60f, new Vector3(2f, 0f, 10f), 0.5f, out float y);

            Assert.That(found, Is.True);
            Assert.That(y, Is.EqualTo(3f).Within(Tolerance));
        }

        [Test]
        public void LevelCamera_ThreeQuartersUp_MatchesTheHandCalculation()
        {
            // Ten units away, half the screen height covers 10 * tan(30 degrees) = 5.7735 units,
            // and 0.75 is half of that above the middle: 3 + 2.8868.
            bool found = HoverHeightSolver.TryHeightAtViewportY(
                new Vector3(0f, 3f, 0f), Vector3.forward, Vector3.up, 60f, new Vector3(0f, 0f, 10f), 0.75f, out float y);

            Assert.That(found, Is.True);
            Assert.That(y, Is.EqualTo(5.8868f).Within(Tolerance));
        }

        [Test]
        public void PitchedCamera_AgreesWithUnitysOwnProjection()
        {
            GameObject holder = new GameObject("HoverHeightSolverTestCamera");
            try
            {
                Camera camera = holder.AddComponent<Camera>();
                camera.fieldOfView = 45f;
                camera.aspect = 16f / 9f;
                holder.transform.position = new Vector3(25f, 4f, 12f);
                holder.transform.rotation = Quaternion.Euler(-10f, 0f, 0f);

                Vector3 line = new Vector3(23f, 0f, 24f);
                float[] wanted = { 0.3f, 0.5f, 0.68f, 0.85f };

                foreach (float viewportY in wanted)
                {
                    bool found = HoverHeightSolver.TryHeightAtViewportY(
                        holder.transform.position, holder.transform.forward, holder.transform.up,
                        camera.fieldOfView, line, viewportY, out float y);

                    Assert.That(found, Is.True, "found at " + viewportY);

                    Vector3 shown = camera.WorldToViewportPoint(new Vector3(line.x, y, line.z));
                    Assert.That(shown.y, Is.EqualTo(viewportY).Within(Tolerance), "screen height at " + viewportY);
                }
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void LineBehindTheCamera_HasNoAnswer()
        {
            bool found = HoverHeightSolver.TryHeightAtViewportY(
                new Vector3(0f, 3f, 0f), Vector3.forward, Vector3.up, 60f, new Vector3(0f, 0f, -5f), 0.5f, out float y);

            Assert.That(found, Is.False);
            Assert.That(y, Is.EqualTo(0f));
        }

        [Test]
        public void CameraLookingStraightUp_HasNoAnswerForTheMiddle()
        {
            // Forward is up and the camera's up is back, so the line's height cannot move the point on screen.
            bool found = HoverHeightSolver.TryHeightAtViewportY(
                new Vector3(0f, 3f, 0f), Vector3.up, Vector3.back, 60f, new Vector3(0f, 0f, 10f), 0.5f, out float y);

            Assert.That(found, Is.False);
        }

        [TestCase(0f)]
        [TestCase(-10f)]
        [TestCase(180f)]
        [TestCase(float.NaN)]
        public void BadFieldOfView_HasNoAnswer(float fov)
        {
            bool found = HoverHeightSolver.TryHeightAtViewportY(
                new Vector3(0f, 3f, 0f), Vector3.forward, Vector3.up, fov, new Vector3(0f, 0f, 10f), 0.5f, out float y);

            Assert.That(found, Is.False);
        }

        [Test]
        public void BadScreenHeight_HasNoAnswer()
        {
            bool found = HoverHeightSolver.TryHeightAtViewportY(
                new Vector3(0f, 3f, 0f), Vector3.forward, Vector3.up, 60f, new Vector3(0f, 0f, 10f), float.NaN, out float y);

            Assert.That(found, Is.False);
        }

        [TestCase(8f, 0.5f)]
        [TestCase(3f, 0.5f)]
        [TestCase(11f, 0.75f)]
        [TestCase(14f, 1f)]
        [TestCase(20f, 1f)]
        public void RangeScale_GrowsFromTheNearShareToTheWholeRange(float depth, float expected)
        {
            float scale = HoverHeightSolver.RangeScale(depth, 8f, 14f, 0.5f);

            Assert.That(scale, Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void RangeScale_ZeroNearShare_StartsAtZero()
        {
            Assert.That(HoverHeightSolver.RangeScale(8f, 8f, 14f, 0f), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(HoverHeightSolver.RangeScale(11f, 8f, 14f, 0f), Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void RangeScale_FarNotBeyondNear_JumpsAtTheFarDistance()
        {
            Assert.That(HoverHeightSolver.RangeScale(9f, 10f, 10f, 0.4f), Is.EqualTo(0.4f).Within(Tolerance));
            Assert.That(HoverHeightSolver.RangeScale(10f, 10f, 10f, 0.4f), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(HoverHeightSolver.RangeScale(9f, 10f, 5f, 0.4f), Is.EqualTo(1f).Within(Tolerance));
        }
    }
}
