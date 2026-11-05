using NUnit.Framework;
using PlanetMakey.Game.Round;
using UnityEngine;

namespace PlanetMakey.Tests.EditMode
{
    public sealed class DayNightCurveTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void NightAmount_IdleIsDay()
        {
            Assert.That(DayNightCurve.NightAmount(RoundState.Idle, 0.5f), Is.EqualTo(0f));
        }

        [TestCase(0f, 0f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(1f, 1f)]
        public void NightAmount_DuskDarkensFromDayToNight(float progress, float expected)
        {
            Assert.That(DayNightCurve.NightAmount(RoundState.Dusk, progress), Is.EqualTo(expected).Within(Tolerance));
        }

        [TestCase(0f)]
        [TestCase(0.3f)]
        [TestCase(1f)]
        public void NightAmount_NightIsAlwaysFullyDark(float progress)
        {
            Assert.That(DayNightCurve.NightAmount(RoundState.Night, progress), Is.EqualTo(1f));
        }

        [TestCase(0f, 1f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(1f, 0f)]
        public void NightAmount_DawnBrightensFromNightToDay(float progress, float expected)
        {
            Assert.That(DayNightCurve.NightAmount(RoundState.Dawn, progress), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void NightAmount_DuskIsNeverBrighterThanBefore()
        {
            float last = 0f;
            for (int i = 0; i <= 20; i++)
            {
                float now = DayNightCurve.NightAmount(RoundState.Dusk, i / 20f);
                Assert.That(now, Is.GreaterThanOrEqualTo(last - Tolerance), "step " + i);
                last = now;
            }
        }

        [TestCase(0f, 0f)]
        [TestCase(0.5f, 1f)]
        [TestCase(1f, 0f)]
        [TestCase(0.25f, 0.75f)]
        [TestCase(0.75f, 0.75f)]
        public void Warmth_PeaksHalfwayAndVanishesInFullDayAndFullNight(float night, float expected)
        {
            Assert.That(DayNightCurve.Warmth(night), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void Warmth_OutOfRangeIsClampedToZero()
        {
            Assert.That(DayNightCurve.Warmth(-1f), Is.EqualTo(0f));
            Assert.That(DayNightCurve.Warmth(2f), Is.EqualTo(0f));
        }

        [Test]
        public void HorizonColor_IsTheDayColorInDayAndTheNightColorInNight()
        {
            Color day = new Color(0.8f, 0.9f, 1f, 1f);
            Color night = new Color(0.2f, 0.1f, 0.3f, 1f);
            Color warm = new Color(1f, 0.5f, 0.2f, 1f);

            AssertNear(day, DayNightCurve.HorizonColor(day, night, warm, 0f, 0.85f));
            AssertNear(night, DayNightCurve.HorizonColor(day, night, warm, 1f, 0.85f));
        }

        private static void AssertNear(Color expected, Color actual)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(Tolerance), "r");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(Tolerance), "g");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(Tolerance), "b");
        }

        [Test]
        public void HorizonColor_HalfwayIsTheMixPulledTowardsTheWarmGlow()
        {
            Color day = new Color(0.8f, 0.9f, 1f, 1f);
            Color night = new Color(0.2f, 0.1f, 0.3f, 1f);
            Color warm = new Color(1f, 0.5f, 0.2f, 1f);

            Color result = DayNightCurve.HorizonColor(day, night, warm, 0.5f, 0.5f);

            Color mix = Color.Lerp(day, night, 0.5f);
            Color expected = Color.Lerp(mix, warm, 0.5f);
            Assert.That(result.r, Is.EqualTo(expected.r).Within(Tolerance));
            Assert.That(result.g, Is.EqualTo(expected.g).Within(Tolerance));
            Assert.That(result.b, Is.EqualTo(expected.b).Within(Tolerance));
        }

        [Test]
        public void HorizonColor_NoGlowStrength_IgnoresTheWarmColor()
        {
            Color day = Color.white;
            Color night = Color.black;
            Color warm = Color.red;

            Color result = DayNightCurve.HorizonColor(day, night, warm, 0.5f, 0f);

            Assert.That(result.r, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(result.g, Is.EqualTo(0.5f).Within(Tolerance));
        }
    }
}
