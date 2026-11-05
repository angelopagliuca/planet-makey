using NUnit.Framework;
using PlanetMakey.Game.Ufo;
using UnityEngine;

namespace PlanetMakey.Tests.EditMode
{
    public sealed class MakeyMotionTests
    {
        private const float Tolerance = 1e-3f;

        [Test]
        public void FacingYaw_StraightInFrontOfTheCamera_IsZero()
        {
            float yaw = MakeyMotion.FacingYawDegrees(new Vector3(25f, 4f, 12f), Vector3.forward, new Vector3(25f, 5f, 22f));

            Assert.That(yaw, Is.EqualTo(0f).Within(Tolerance));
        }

        [TestCase(5f, 10f)]
        [TestCase(-5f, 10f)]
        [TestCase(12f, 3f)]
        public void FacingYaw_ToOneSide_TurnsTheRestingFrontOntoTheCamera(float sideways, float ahead)
        {
            Vector3 cameraPosition = new Vector3(0f, 3f, 0f);
            Vector3 makeyPosition = new Vector3(sideways, 6f, ahead);

            float yaw = MakeyMotion.FacingYawDegrees(cameraPosition, Vector3.forward, makeyPosition);

            Vector3 turned = Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.back;
            Vector3 toCamera = cameraPosition - makeyPosition;
            toCamera.y = 0f;
            Assert.That(Vector3.Angle(turned, toCamera), Is.LessThan(0.1f));
        }

        [Test]
        public void FacingYaw_FollowsACameraThatIsTurned()
        {
            // A camera turned 90 degrees to look along +X; Makey straight ahead of it needs no turn.
            float yaw = MakeyMotion.FacingYawDegrees(Vector3.zero, Vector3.right, new Vector3(10f, 2f, 0f));

            Assert.That(yaw, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void FacingYaw_NoUsableDirection_IsZero()
        {
            // Looking straight down has no heading, and Makey right above the camera has no direction to it.
            Assert.That(MakeyMotion.FacingYawDegrees(Vector3.zero, Vector3.down, new Vector3(3f, 0f, 3f)), Is.EqualTo(0f));
            Assert.That(MakeyMotion.FacingYawDegrees(Vector3.zero, Vector3.forward, new Vector3(0f, 5f, 0f)), Is.EqualTo(0f));
        }

        [Test]
        public void Spring_WithLittleDamping_OvershootsAndThenSettlesOnTheTarget()
        {
            MakeyMotion.Spring spring = default;
            float highest = 0f;

            for (int i = 0; i < 300; i++)
            {
                spring.Step(1f, 60f, 7f, 0.01f);
                highest = Mathf.Max(highest, spring.Value);
            }

            Assert.That(highest, Is.GreaterThan(1.05f), "overshoot");
            Assert.That(spring.Value, Is.EqualTo(1f).Within(0.01f), "settled");
            Assert.That(spring.Velocity, Is.EqualTo(0f).Within(0.01f), "at rest");
        }

        [Test]
        public void Spring_WithHeavyDamping_NeverPassesTheTarget()
        {
            MakeyMotion.Spring spring = default;
            float highest = 0f;

            for (int i = 0; i < 600; i++)
            {
                spring.Step(1f, 60f, 40f, 0.01f);
                highest = Mathf.Max(highest, spring.Value);
            }

            Assert.That(highest, Is.LessThanOrEqualTo(1.001f));
            Assert.That(spring.Value, Is.EqualTo(1f).Within(0.02f));
        }

        [Test]
        public void Spring_ASlowFrame_IsCappedAndStaysFinite()
        {
            MakeyMotion.Spring spring = default;

            spring.Step(1f, 60f, 7f, 10f);

            Assert.That(float.IsFinite(spring.Value), Is.True);
            Assert.That(spring.Value, Is.LessThan(1f));
        }

        [Test]
        public void Spring_ZeroTime_ChangesNothing()
        {
            MakeyMotion.Spring spring = new MakeyMotion.Spring(0.4f, 2f);

            spring.Step(1f, 60f, 7f, 0f);

            Assert.That(spring.Value, Is.EqualTo(0.4f));
            Assert.That(spring.Velocity, Is.EqualTo(2f));
        }

        [Test]
        public void Spring_Reset_PutsItAtRest()
        {
            MakeyMotion.Spring spring = default;
            spring.Step(1f, 60f, 7f, 0.02f);

            spring.Reset();

            Assert.That(spring.Value, Is.EqualTo(0f));
            Assert.That(spring.Velocity, Is.EqualTo(0f));
        }
    }
}
