using NUnit.Framework;
using PlanetMakey.Game.Audio;

namespace PlanetMakey.Tests.EditMode
{
    public sealed class AudioFadeTests
    {
        private const float Tolerance = 1e-4f;

        [TestCase(0f, 1f, 0.5f, 2f, 0.25f)]
        [TestCase(0.9f, 1f, 0.5f, 2f, 1f)]
        [TestCase(1f, 0f, 1f, 2f, 0.5f)]
        [TestCase(0.2f, 0f, 1f, 2f, 0f)]
        public void Approach_FadesAtASteadyRate_AndStopsOnTheTarget(float level, float target, float deltaTime, float seconds, float expected)
        {
            Assert.That(AudioFade.Approach(level, target, deltaTime, seconds), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void Approach_TwoSecondFade_TakesTwoSecondsInSmallSteps()
        {
            float level = 0f;
            for (int i = 0; i < 120; i++)
            {
                level = AudioFade.Approach(level, 1f, 1f / 60f, 2f);
            }

            Assert.That(level, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Approach_NoDuration_ArrivesAtOnce()
        {
            Assert.That(AudioFade.Approach(0f, 1f, 0.016f, 0f), Is.EqualTo(1f));
            Assert.That(AudioFade.Approach(1f, 0f, 0.016f, -1f), Is.EqualTo(0f));
        }

        [TestCase(0f, 0f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(1f, 1f)]
        [TestCase(0.25f, 0.15625f)]
        [TestCase(-1f, 0f)]
        [TestCase(2f, 1f)]
        public void Gain_IsAnSCurveFromSilentToFull(float level, float expected)
        {
            Assert.That(AudioFade.Gain(level), Is.EqualTo(expected).Within(Tolerance));
        }

        [TestCase(132f, 132f, 1f, false)]
        [TestCase(132f, 132.8f, 1f, false)]
        [TestCase(132f, 160f, 1f, true)]
        [TestCase(160f, 132f, 1f, true)]
        [TestCase(132f, 130.5f, 1f, true)]
        public void LengthsDiffer_WarnsOnlyBeyondTheTolerance(float round, float clip, float tolerance, bool expected)
        {
            Assert.That(AudioFade.LengthsDiffer(round, clip, tolerance), Is.EqualTo(expected));
        }
    }
}
