using NUnit.Framework;
using PlanetMakey.Game.Round;

namespace PlanetMakey.Tests.EditMode
{
    public sealed class RoundHudMathTests
    {
        private const float Tolerance = 1e-4f;

        [TestCase(RoundState.Idle, false)]
        [TestCase(RoundState.Dusk, true)]
        [TestCase(RoundState.Night, true)]
        [TestCase(RoundState.Dawn, true)]
        public void IsRoundVisible_IsHiddenOnlyWhileIdle(RoundState state, bool expected)
        {
            Assert.That(RoundHudMath.IsRoundVisible(state), Is.EqualTo(expected));
        }

        [TestCase(0, 3, true)]
        [TestCase(2, 3, true)]
        [TestCase(3, 3, false)]
        [TestCase(9, 3, false)]
        [TestCase(0, 0, false)]
        [TestCase(-1, 3, false)]
        public void IsCowLit_TheFirstIconsAreLitUpToTheSafeCount(int index, int safe, bool expected)
        {
            Assert.That(RoundHudMath.IsCowLit(index, safe), Is.EqualTo(expected));
        }

        [TestCase(RoundState.Idle, 0.7f, 0f)]
        [TestCase(RoundState.Dusk, 0.7f, 0f)]
        [TestCase(RoundState.Night, 0f, 0f)]
        [TestCase(RoundState.Night, 0.5f, 0.5f)]
        [TestCase(RoundState.Night, 1.5f, 1f)]
        [TestCase(RoundState.Dawn, 0.2f, 1f)]
        public void NightBarProgress_FollowsTheNight(RoundState state, float stateProgress, float expected)
        {
            Assert.That(RoundHudMath.NightBarProgress(state, stateProgress), Is.EqualTo(expected).Within(Tolerance));
        }

        [TestCase(0f, 0.85f, 0f)]
        [TestCase(0.85f, 0.85f, 0f)]
        [TestCase(0.925f, 0.85f, 0.5f)]
        [TestCase(1f, 0.85f, 1f)]
        [TestCase(2f, 0.85f, 1f)]
        [TestCase(0.5f, 0f, 0.5f)]
        public void SunBlend_StaysMoonUntilTheStart_ThenEasesToSun(float progress, float blendStart, float expected)
        {
            Assert.That(RoundHudMath.SunBlend(progress, blendStart), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void SunBlend_StartAtOneOrMore_IsSunOnlyAtTheEnd()
        {
            Assert.That(RoundHudMath.SunBlend(0.99f, 1f), Is.EqualTo(0f));
            Assert.That(RoundHudMath.SunBlend(1f, 1f), Is.EqualTo(1f));
            Assert.That(RoundHudMath.SunBlend(0.5f, 3f), Is.EqualTo(0f));
        }

        [TestCase(0f, 1f, 0.25f, 1f, 0.25f)]
        [TestCase(0.9f, 1f, 0.25f, 1f, 1f)]
        [TestCase(1f, 0f, 0.5f, 2f, 0.75f)]
        public void Approach_StepsAtASteadyRate_AndStopsOnTheTarget(float current, float target, float deltaTime, float seconds, float expected)
        {
            Assert.That(RoundHudMath.Approach(current, target, deltaTime, seconds), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void Approach_NoDuration_ArrivesAtOnce()
        {
            Assert.That(RoundHudMath.Approach(0f, 1f, 0.016f, 0f), Is.EqualTo(1f));
            Assert.That(RoundHudMath.Approach(1f, 0f, 0.016f, -1f), Is.EqualTo(0f));
        }
    }
}
