using NUnit.Framework;
using PlanetMakey.Game.Ufo;

namespace PlanetMakey.Tests.EditMode
{
    public sealed class BeamShapeTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void WidthFactor_IsTheTopAtTheShipAndTheBottomAtTheGround()
        {
            Assert.That(BeamShape.WidthFactor(0f, 0.8f, 5f, 2.2f), Is.EqualTo(0.8f).Within(Tolerance));
            Assert.That(BeamShape.WidthFactor(1f, 0.8f, 5f, 2.2f), Is.EqualTo(5f).Within(Tolerance));
        }

        [Test]
        public void WidthFactor_PowerOne_IsAStraightCone()
        {
            Assert.That(BeamShape.WidthFactor(0.5f, 1f, 3f, 1f), Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void WidthFactor_PowerAboveOne_StaysSlimLongerThanACone()
        {
            float cone = BeamShape.WidthFactor(0.5f, 1f, 5f, 1f);
            float skirt = BeamShape.WidthFactor(0.5f, 1f, 5f, 2.5f);

            Assert.That(skirt, Is.LessThan(cone));
            Assert.That(skirt, Is.GreaterThan(1f));
        }

        [Test]
        public void WidthFactor_NeverShrinksOnTheWayDown()
        {
            float last = BeamShape.WidthFactor(0f, 0.8f, 5f, 2.2f);
            for (int i = 1; i <= 20; i++)
            {
                float now = BeamShape.WidthFactor(i / 20f, 0.8f, 5f, 2.2f);
                Assert.That(now, Is.GreaterThanOrEqualTo(last - Tolerance), "step " + i);
                last = now;
            }
        }

        [Test]
        public void WidthFactor_OutOfRangePositionAndPower_AreClamped()
        {
            Assert.That(BeamShape.WidthFactor(-1f, 1f, 4f, 2f), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(BeamShape.WidthFactor(2f, 1f, 4f, 2f), Is.EqualTo(4f).Within(Tolerance));
            Assert.That(float.IsFinite(BeamShape.WidthFactor(0.5f, 1f, 4f, 0f)), Is.True);
        }

        [TestCase(0f, 0.6f, 0f)]
        [TestCase(0.6f, 0.6f, 0f)]
        [TestCase(0.8f, 0.6f, 0.5f)]
        [TestCase(1f, 0.6f, 1f)]
        [TestCase(0.3f, 0.6f, 0f)]
        public void Urgency_IsZeroUntilTheStartThenGrowsToOne(float progress, float start, float expected)
        {
            Assert.That(BeamShape.Urgency(progress, start), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void Urgency_StartAtOneOrMore_NeverGrows()
        {
            Assert.That(BeamShape.Urgency(1f, 1f), Is.EqualTo(0f));
            Assert.That(BeamShape.Urgency(1f, 2f), Is.EqualTo(0f));
        }

        [TestCase(0.2f, 0.2f, 1f)]
        [TestCase(0.1f, 0.2f, 0.5f)]
        [TestCase(0f, 0.2f, 0f)]
        [TestCase(-1f, 0.2f, 0f)]
        [TestCase(5f, 0.2f, 1f)]
        public void FadeOut_RunsFromOneToZero(float secondsLeft, float duration, float expected)
        {
            Assert.That(BeamShape.FadeOut(secondsLeft, duration), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void FadeOut_NoDuration_IsZeroAtOnce()
        {
            Assert.That(BeamShape.FadeOut(1f, 0f), Is.EqualTo(0f));
            Assert.That(BeamShape.FadeOut(1f, -1f), Is.EqualTo(0f));
        }

        [TestCase(0f, 4f, 0.3f, 1f)]
        [TestCase(2f, 4f, 0.3f, 0.7f)]
        [TestCase(4f, 4f, 0.3f, 1f)]
        [TestCase(1f, 4f, 0.3f, 0.85f)]
        public void Breath_SwellsBetweenOneAndOneMinusDepth(float time, float period, float depth, float expected)
        {
            Assert.That(BeamShape.Breath(time, period, depth), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void Breath_NoPeriodOrNoDepth_IsSteady_AndDepthIsClamped()
        {
            Assert.That(BeamShape.Breath(1.3f, 0f, 0.5f), Is.EqualTo(1f));
            Assert.That(BeamShape.Breath(1.3f, 4f, 0f), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(BeamShape.Breath(2f, 4f, 5f), Is.EqualTo(0f).Within(Tolerance));
        }

        [TestCase(0f, 1f, 0.25f, 1f, 0.25f)]
        [TestCase(0.9f, 1f, 0.25f, 1f, 1f)]
        [TestCase(1f, 0f, 0.5f, 2f, 0.75f)]
        [TestCase(0.1f, 0f, 0.5f, 2f, 0f)]
        public void MoveLevel_StepsAtASteadyRate_AndStopsOnTheTarget(float level, float target, float deltaTime, float seconds, float expected)
        {
            Assert.That(BeamShape.MoveLevel(level, target, deltaTime, seconds), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void MoveLevel_NoDuration_ArrivesAtOnce()
        {
            Assert.That(BeamShape.MoveLevel(0f, 1f, 0.016f, 0f), Is.EqualTo(1f));
            Assert.That(BeamShape.MoveLevel(1f, 0f, 0.016f, -1f), Is.EqualTo(0f));
        }

        [Test]
        public void SpotAngle_CoversTheFootWithMargin()
        {
            // A foot of radius 1.25 seen from 5 units above, with a third of margin: 2 * atan(1.25 * 1.3 / 5).
            float angle = BeamShape.SpotAngleDegrees(1.25f, 5f, 1.3f, 10f, 140f);

            Assert.That(angle, Is.EqualTo(36f).Within(0.1f));
        }

        [Test]
        public void SpotAngle_IsClamped_AndAVeryShortBeamIsNotInfinitelyWide()
        {
            Assert.That(BeamShape.SpotAngleDegrees(1.25f, 5f, 1.3f, 50f, 140f), Is.EqualTo(50f));
            Assert.That(BeamShape.SpotAngleDegrees(1.25f, 0f, 1.3f, 10f, 140f), Is.LessThanOrEqualTo(140f));
            Assert.That(BeamShape.SpotAngleDegrees(0f, 5f, 1.3f, 10f, 140f), Is.EqualTo(10f));
        }
    }
}
