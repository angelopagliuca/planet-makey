using System;
using System.Collections.Generic;
using NUnit.Framework;
using PlanetMakey.Core;
using PlanetMakey.Game.Ufo;

namespace PlanetMakey.Tests.EditMode
{
    public sealed class MakeyBrainTests
    {
        private const float EnterSeconds = 1f;
        private const float DazeSeconds = 2f;
        private const float RecoverSeconds = 1f;

        private static MakeyBrain NewBrain(int seed = 1)
        {
            return new MakeyBrain(EnterSeconds, DazeSeconds, RecoverSeconds, new Random(seed));
        }

        private static MakeyBrain ShieldedBrain(int seed = 1)
        {
            MakeyBrain brain = NewBrain(seed);
            brain.Enter();
            brain.Tick(EnterSeconds);
            return brain;
        }

        private static MakeyBrain DazedBrain()
        {
            MakeyBrain brain = ShieldedBrain();
            brain.ReceiveBall(brain.ShieldColor);
            return brain;
        }

        private static BallColor DifferentColor(BallColor color)
        {
            return color == BallColor.Red ? BallColor.Blue : BallColor.Red;
        }

        [Test]
        public void NewBrain_IsAway()
        {
            MakeyBrain brain = NewBrain();

            Assert.That(brain.State, Is.EqualTo(MakeyState.Away));
            Assert.That(brain.StateProgress, Is.EqualTo(0f));
        }

        [Test]
        public void Enter_FliesInForTheEnterTime_ThenRaisesAShield()
        {
            MakeyBrain brain = NewBrain();

            brain.Enter();
            Assert.That(brain.State, Is.EqualTo(MakeyState.Entering));

            brain.Tick(0.5f);
            Assert.That(brain.State, Is.EqualTo(MakeyState.Entering));
            Assert.That(brain.StateProgress, Is.EqualTo(0.5f).Within(1e-5f));

            brain.Tick(0.5f);
            Assert.That(brain.State, Is.EqualTo(MakeyState.Shielded));
            Assert.That(brain.StateProgress, Is.EqualTo(0f));
        }

        [Test]
        public void Enter_WhenNotAway_DoesNothing()
        {
            MakeyBrain brain = ShieldedBrain();
            BallColor color = brain.ShieldColor;

            brain.Enter();

            Assert.That(brain.State, Is.EqualTo(MakeyState.Shielded));
            Assert.That(brain.ShieldColor, Is.EqualTo(color));
        }

        [Test]
        public void Tick_WhileAway_DoesNothing()
        {
            MakeyBrain brain = NewBrain();

            brain.Tick(100f);

            Assert.That(brain.State, Is.EqualTo(MakeyState.Away));
        }

        [Test]
        public void MatchingColor_BreaksTheShield_AndDazesMakey()
        {
            MakeyBrain brain = ShieldedBrain();

            MakeyHitResult result = brain.ReceiveBall(brain.ShieldColor);

            Assert.That(result, Is.EqualTo(MakeyHitResult.Shattered));
            Assert.That(brain.State, Is.EqualTo(MakeyState.Dazed));
        }

        [Test]
        public void WrongColor_BouncesOff_AndChangesNothing()
        {
            MakeyBrain brain = ShieldedBrain();
            BallColor shield = brain.ShieldColor;

            MakeyHitResult result = brain.ReceiveBall(DifferentColor(shield));

            Assert.That(result, Is.EqualTo(MakeyHitResult.Deflected));
            Assert.That(brain.State, Is.EqualTo(MakeyState.Shielded));
            Assert.That(brain.ShieldColor, Is.EqualTo(shield));
        }

        [Test]
        public void Balls_AreIgnored_WhileAway()
        {
            AssertEveryColorIgnored(NewBrain());
        }

        [Test]
        public void Balls_AreIgnored_WhileEntering()
        {
            MakeyBrain brain = NewBrain();
            brain.Enter();

            AssertEveryColorIgnored(brain);
        }

        [Test]
        public void Balls_AreIgnored_WhileDazed()
        {
            MakeyBrain brain = DazedBrain();

            AssertEveryColorIgnored(brain);
            Assert.That(brain.State, Is.EqualTo(MakeyState.Dazed));
        }

        [Test]
        public void Balls_AreIgnored_WhileRecovering()
        {
            MakeyBrain brain = DazedBrain();
            brain.Tick(DazeSeconds);
            Assert.That(brain.State, Is.EqualTo(MakeyState.Recovering));

            AssertEveryColorIgnored(brain);
            Assert.That(brain.State, Is.EqualTo(MakeyState.Recovering));
        }

        private static void AssertEveryColorIgnored(MakeyBrain brain)
        {
            foreach (BallColor color in Enum.GetValues(typeof(BallColor)))
            {
                Assert.That(brain.ReceiveBall(color), Is.EqualTo(MakeyHitResult.Ignored), color.ToString());
            }
        }

        [Test]
        public void AfterTheShieldBreaks_MakeyIsDazed_ThenRecovers_ThenShieldsInAnotherColor()
        {
            MakeyBrain brain = ShieldedBrain();
            BallColor first = brain.ShieldColor;
            brain.ReceiveBall(first);

            brain.Tick(DazeSeconds - 0.5f);
            Assert.That(brain.State, Is.EqualTo(MakeyState.Dazed));
            Assert.That(brain.StateProgress, Is.EqualTo((DazeSeconds - 0.5f) / DazeSeconds).Within(1e-5f));

            brain.Tick(0.5f);
            Assert.That(brain.State, Is.EqualTo(MakeyState.Recovering));

            brain.Tick(RecoverSeconds);
            Assert.That(brain.State, Is.EqualTo(MakeyState.Shielded));
            Assert.That(brain.ShieldColor, Is.Not.EqualTo(first));
        }

        [Test]
        public void ShieldColor_NeverRepeatsBackToBack_AndEveryColorComesUp()
        {
            MakeyBrain brain = ShieldedBrain(7);
            var seen = new HashSet<BallColor> { brain.ShieldColor };
            BallColor previous = brain.ShieldColor;

            for (int i = 0; i < 300; i++)
            {
                brain.ReceiveBall(brain.ShieldColor);
                brain.Tick(DazeSeconds);
                brain.Tick(RecoverSeconds);

                Assert.That(brain.State, Is.EqualTo(MakeyState.Shielded));
                Assert.That(brain.ShieldColor, Is.Not.EqualTo(previous), "cycle " + i);
                previous = brain.ShieldColor;
                seen.Add(previous);
            }

            Assert.That(seen.Count, Is.EqualTo(Enum.GetValues(typeof(BallColor)).Length));
        }

        [Test]
        public void Tick_WithZeroNegativeOrNotANumber_ChangesNothing()
        {
            MakeyBrain brain = NewBrain();
            brain.Enter();

            brain.Tick(0f);
            brain.Tick(-5f);
            brain.Tick(float.NaN);

            Assert.That(brain.State, Is.EqualTo(MakeyState.Entering));
            Assert.That(brain.StateProgress, Is.EqualTo(0f));
        }

        [Test]
        public void Tick_WithAHugeStep_EndsOnlyTheCurrentState()
        {
            MakeyBrain brain = NewBrain();
            brain.Enter();

            brain.Tick(1000f);
            Assert.That(brain.State, Is.EqualTo(MakeyState.Shielded));

            brain.ReceiveBall(brain.ShieldColor);
            brain.Tick(1000f);
            Assert.That(brain.State, Is.EqualTo(MakeyState.Recovering));

            brain.Tick(1000f);
            Assert.That(brain.State, Is.EqualTo(MakeyState.Shielded));
        }

        [Test]
        public void StateChanged_IsRaisedForEveryChange_InOrder()
        {
            MakeyBrain brain = NewBrain();
            var states = new List<MakeyState>();
            brain.StateChanged += states.Add;

            brain.Enter();
            brain.Tick(EnterSeconds);
            brain.ReceiveBall(brain.ShieldColor);
            brain.Tick(DazeSeconds);
            brain.Tick(RecoverSeconds);

            Assert.That(
                states,
                Is.EqualTo(new[]
                {
                    MakeyState.Entering, MakeyState.Shielded, MakeyState.Dazed,
                    MakeyState.Recovering, MakeyState.Shielded
                }));
        }

        [Test]
        public void StateChanged_ShowsTheNewShieldColor_WhenShielded()
        {
            MakeyBrain brain = NewBrain();
            BallColor? colorWhenTold = null;
            brain.StateChanged += state =>
            {
                if (state == MakeyState.Shielded)
                {
                    colorWhenTold = brain.ShieldColor;
                }
            };

            brain.Enter();
            brain.Tick(EnterSeconds);

            Assert.That(colorWhenTold, Is.EqualTo(brain.ShieldColor));
        }

        [Test]
        public void ZeroDurations_EndEachStateOnTheNextTick()
        {
            var brain = new MakeyBrain(0f, 0f, 0f, new Random(1));

            brain.Enter();
            Assert.That(brain.StateProgress, Is.EqualTo(1f));

            brain.Tick(0.001f);
            Assert.That(brain.State, Is.EqualTo(MakeyState.Shielded));

            brain.ReceiveBall(brain.ShieldColor);
            brain.Tick(0.001f);
            Assert.That(brain.State, Is.EqualTo(MakeyState.Recovering));

            brain.Tick(0.001f);
            Assert.That(brain.State, Is.EqualTo(MakeyState.Shielded));
        }

        [Test]
        public void NegativeDurations_CountAsZero()
        {
            var brain = new MakeyBrain(-1f, -1f, -1f, new Random(1));

            brain.Enter();
            brain.Tick(0.001f);

            Assert.That(brain.State, Is.EqualTo(MakeyState.Shielded));
        }

        [Test]
        public void SameSeed_GivesTheSameShieldColors()
        {
            MakeyBrain a = ShieldedBrain(42);
            MakeyBrain b = ShieldedBrain(42);

            for (int i = 0; i < 20; i++)
            {
                Assert.That(a.ShieldColor, Is.EqualTo(b.ShieldColor), "cycle " + i);
                a.ReceiveBall(a.ShieldColor);
                a.Tick(DazeSeconds);
                a.Tick(RecoverSeconds);
                b.ReceiveBall(b.ShieldColor);
                b.Tick(DazeSeconds);
                b.Tick(RecoverSeconds);
            }
        }

        [Test]
        public void Constructor_WithoutARandom_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MakeyBrain(1f, 1f, 1f, null));
        }
    }
}
