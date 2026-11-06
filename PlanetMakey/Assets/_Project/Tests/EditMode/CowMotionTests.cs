using NUnit.Framework;
using PlanetMakey.Game.Cows;
using UnityEngine;

namespace PlanetMakey.Tests.EditMode
{
    public sealed class CowMotionTests
    {
        private const float Tolerance = 1e-4f;

        [TestCase(0f, 0f)]
        [TestCase(0.25f, 0.75f)]
        [TestCase(0.5f, 1f)]
        [TestCase(1f, 0f)]
        [TestCase(-1f, 0f)]
        [TestCase(2f, 0f)]
        public void HopHeight_IsAnArcFromTheGroundToTheTopAndBack(float t, float expected)
        {
            Assert.That(CowMotion.HopHeight(t), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void HopStretch_IsNormalAtBothEndsOfTheHop()
        {
            Assert.That(CowMotion.HopStretch(0f, 0.1f), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(CowMotion.HopStretch(1f, 0.1f), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void HopStretch_StretchesOnTheWayUp_AndSquashesOnLanding()
        {
            Assert.That(CowMotion.HopStretch(0.25f, 0.1f), Is.EqualTo(1.1f).Within(Tolerance));
            Assert.That(CowMotion.HopStretch(0.5f, 0.1f), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(CowMotion.HopStretch(0.9f, 0.1f), Is.EqualTo(1f - 0.14f).Within(Tolerance));
        }

        [Test]
        public void HopStretch_NoAmount_IsAlwaysOne()
        {
            for (int i = 0; i <= 10; i++)
            {
                Assert.That(CowMotion.HopStretch(i / 10f, 0f), Is.EqualTo(1f).Within(Tolerance), "step " + i);
            }
        }

        [TestCase(0f, 0f)]
        [TestCase(0.425f, 0.5f)]
        [TestCase(0.85f, 1f)]
        [TestCase(1f, 1f)]
        [TestCase(-1f, 0f)]
        public void HopTurn_EasesFromNothingToDoneBeforeLanding(float t, float expected)
        {
            Assert.That(CowMotion.HopTurn(t), Is.EqualTo(expected).Within(1e-3f));
        }

        [Test]
        public void Shake_StaysWithinOneInEveryDirection()
        {
            for (int i = 0; i < 200; i++)
            {
                Vector3 shake = CowMotion.Shake(i * 0.37f, 3.1f);
                Assert.That(Mathf.Abs(shake.x), Is.LessThanOrEqualTo(1f), "x at " + i);
                Assert.That(Mathf.Abs(shake.y), Is.LessThanOrEqualTo(1f), "y at " + i);
                Assert.That(Mathf.Abs(shake.z), Is.LessThanOrEqualTo(1f), "z at " + i);
            }
        }

        [Test]
        public void Shake_ChangesSmoothly_AndDiffersBetweenCows()
        {
            Vector3 now = CowMotion.Shake(5f, 2f);
            Vector3 soon = CowMotion.Shake(5.001f, 2f);
            Assert.That((now - soon).magnitude, Is.LessThan(0.05f));

            Vector3 other = CowMotion.Shake(5f, 9f);
            Assert.That((now - other).magnitude, Is.GreaterThan(0.001f));
        }

        [TestCase(0f, 0.8f, 0.5f, 1f)]
        [TestCase(0.8f, 0.8f, 0.5f, 1f)]
        [TestCase(0.9f, 0.8f, 0.5f, 0.75f)]
        [TestCase(1f, 0.8f, 0.5f, 0.5f)]
        [TestCase(2f, 0.8f, 0.5f, 0.5f)]
        public void EndShrink_StaysFullSizeUntilTheStart_ThenEasesToTheEndScale(float progress, float start, float endScale, float expected)
        {
            Assert.That(CowMotion.EndShrink(progress, start, endScale), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void EndShrink_StartAtOneOrMore_NeverShrinks()
        {
            Assert.That(CowMotion.EndShrink(1f, 1f, 0.5f), Is.EqualTo(1f));
            Assert.That(CowMotion.EndShrink(1f, 3f, 0.5f), Is.EqualTo(1f));
        }

        [Test]
        public void PopScale_StartsAtZeroAndEndsAtExactlyOne()
        {
            Assert.That(CowMotion.PopScale(0f, 1.7f), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(CowMotion.PopScale(1f, 1.7f), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void PopScale_SwellsPastOneOnTheWay_ButNotWithoutOvershoot()
        {
            // 0.875 + 0.125 * 1.7 = 1.0875 at the middle.
            Assert.That(CowMotion.PopScale(0.5f, 1.7f), Is.EqualTo(1.0875f).Within(Tolerance));
            for (int i = 0; i <= 10; i++)
            {
                Assert.That(CowMotion.PopScale(i / 10f, 0f), Is.LessThanOrEqualTo(1f + Tolerance), "step " + i);
            }
        }
    }
}
