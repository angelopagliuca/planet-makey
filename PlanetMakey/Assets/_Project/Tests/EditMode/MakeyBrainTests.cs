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
        private const float LeaveSeconds = 3f;

        private static MakeyBrain NewBrain(int seed = 1)
        {
            return new MakeyBrain(EnterSeconds, DazeSeconds, RecoverSeconds, new Random(seed));
        }

        private static MakeyBrain NewLeavingCapableBrain()
        {
            return new MakeyBrain(EnterSeconds, DazeSeconds, RecoverSeconds, LeaveSeconds, new Random(1));
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

        private static MakeyBrain ShieldedBrainWithColors(int seed, params BallColor[] colors)
        {
            var brain = new MakeyBrain(EnterSeconds, DazeSeconds, RecoverSeconds, LeaveSeconds, new Random(seed), colors);
            brain.Enter();
            brain.Tick(EnterSeconds);
            return brain;
        }

        [Test]
        public void ShieldColors_OnlyTheListedColorsAreUsed_AndNeverTwiceInARow()
        {
            MakeyBrain brain = ShieldedBrainWithColors(3, BallColor.Purple, BallColor.Orange, BallColor.Green);
            var allowed = new HashSet<BallColor> { BallColor.Purple, BallColor.Orange, BallColor.Green };
            var seen = new HashSet<BallColor> { brain.ShieldColor };
            BallColor previous = brain.ShieldColor;
            Assert.That(allowed, Does.Contain(previous));

            for (int i = 0; i < 200; i++)
            {
                brain.ReceiveBall(brain.ShieldColor);
                brain.Tick(DazeSeconds);
                brain.Tick(RecoverSeconds);

                Assert.That(allowed, Does.Contain(brain.ShieldColor), "cycle " + i);
                Assert.That(brain.ShieldColor, Is.Not.EqualTo(previous), "cycle " + i);
                previous = brain.ShieldColor;
                seen.Add(previous);
            }

            Assert.That(seen, Is.EquivalentTo(allowed));
        }

        [Test]
        public void ShieldColors_TwoColors_AlternateEveryTime()
        {
            MakeyBrain brain = ShieldedBrainWithColors(5, BallColor.Red, BallColor.Blue);
            BallColor previous = brain.ShieldColor;

            for (int i = 0; i < 20; i++)
            {
                brain.ReceiveBall(brain.ShieldColor);
                brain.Tick(DazeSeconds);
                brain.Tick(RecoverSeconds);

                Assert.That(brain.ShieldColor, Is.Not.EqualTo(previous), "cycle " + i);
                previous = brain.ShieldColor;
            }
        }

        [Test]
        public void ShieldColors_OneColor_IsAlwaysThatColor()
        {
            MakeyBrain brain = ShieldedBrainWithColors(2, BallColor.Orange);

            for (int i = 0; i < 10; i++)
            {
                Assert.That(brain.ShieldColor, Is.EqualTo(BallColor.Orange));
                brain.ReceiveBall(BallColor.Orange);
                brain.Tick(DazeSeconds);
                brain.Tick(RecoverSeconds);
            }
        }

        [Test]
        public void ShieldColors_NullEmptyAndRepeats_FallBackOrCountOnce()
        {
            MakeyBrain none = new MakeyBrain(EnterSeconds, DazeSeconds, RecoverSeconds, LeaveSeconds, new Random(1), null);
            MakeyBrain empty = ShieldedBrainWithColors(1);
            none.Enter();
            none.Tick(EnterSeconds);
            Assert.That(Enum.IsDefined(typeof(BallColor), none.ShieldColor), Is.True);
            Assert.That(Enum.IsDefined(typeof(BallColor), empty.ShieldColor), Is.True);

            // The same color listed twice is one color, so it still never repeats back to back with a second one.
            MakeyBrain twice = ShieldedBrainWithColors(4, BallColor.Red, BallColor.Red, BallColor.Blue);
            BallColor previous = twice.ShieldColor;
            for (int i = 0; i < 20; i++)
            {
                twice.ReceiveBall(twice.ShieldColor);
                twice.Tick(DazeSeconds);
                twice.Tick(RecoverSeconds);
                Assert.That(twice.ShieldColor, Is.Not.EqualTo(previous), "cycle " + i);
                previous = twice.ShieldColor;
            }
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
            Assert.Throws<ArgumentNullException>(() => new MakeyBrain(1f, 1f, 1f, 1f, null));
        }

        [Test]
        public void Leave_FromEveryStateButAway_StartsLeaving()
        {
            MakeyBrain entering = NewLeavingCapableBrain();
            entering.Enter();
            entering.Leave();
            Assert.That(entering.State, Is.EqualTo(MakeyState.Leaving), "entering");

            MakeyBrain shielded = NewLeavingCapableBrain();
            shielded.Enter();
            shielded.Tick(EnterSeconds);
            shielded.Leave();
            Assert.That(shielded.State, Is.EqualTo(MakeyState.Leaving), "shielded");

            MakeyBrain dazed = NewLeavingCapableBrain();
            dazed.Enter();
            dazed.Tick(EnterSeconds);
            dazed.ReceiveBall(dazed.ShieldColor);
            dazed.Leave();
            Assert.That(dazed.State, Is.EqualTo(MakeyState.Leaving), "dazed");

            MakeyBrain recovering = NewLeavingCapableBrain();
            recovering.Enter();
            recovering.Tick(EnterSeconds);
            recovering.ReceiveBall(recovering.ShieldColor);
            recovering.Tick(DazeSeconds);
            recovering.Leave();
            Assert.That(recovering.State, Is.EqualTo(MakeyState.Leaving), "recovering");
        }

        [Test]
        public void Leave_WhileAwayOrAlreadyLeaving_DoesNothing()
        {
            MakeyBrain brain = NewLeavingCapableBrain();
            var states = new List<MakeyState>();
            brain.StateChanged += states.Add;

            brain.Leave();
            Assert.That(brain.State, Is.EqualTo(MakeyState.Away));
            Assert.That(states, Is.Empty);

            brain.Enter();
            brain.Leave();
            brain.Tick(1f);
            brain.Leave();

            Assert.That(brain.State, Is.EqualTo(MakeyState.Leaving));
            Assert.That(brain.StateProgress, Is.EqualTo(1f / LeaveSeconds).Within(1e-5f));
            Assert.That(states, Is.EqualTo(new[] { MakeyState.Entering, MakeyState.Leaving }));
        }

        [Test]
        public void Leaving_LastsTheLeaveTime_ThenMakeyIsAway_AndCanEnterAgain()
        {
            MakeyBrain brain = ShieldedLeavingBrain();
            brain.Leave();

            brain.Tick(LeaveSeconds - 0.5f);
            Assert.That(brain.State, Is.EqualTo(MakeyState.Leaving));
            Assert.That(brain.StateProgress, Is.EqualTo((LeaveSeconds - 0.5f) / LeaveSeconds).Within(1e-5f));

            brain.Tick(0.5f);
            Assert.That(brain.State, Is.EqualTo(MakeyState.Away));
            Assert.That(brain.StateProgress, Is.EqualTo(0f));

            brain.Enter();
            Assert.That(brain.State, Is.EqualTo(MakeyState.Entering));
        }

        [Test]
        public void Balls_AreIgnored_WhileLeaving()
        {
            MakeyBrain brain = ShieldedLeavingBrain();
            brain.Leave();

            AssertEveryColorIgnored(brain);
            Assert.That(brain.State, Is.EqualTo(MakeyState.Leaving));
        }

        [Test]
        public void Leaving_WithAHugeStep_EndsOnlyThatState()
        {
            MakeyBrain brain = ShieldedLeavingBrain();
            brain.Leave();

            brain.Tick(1000f);

            Assert.That(brain.State, Is.EqualTo(MakeyState.Away));
        }

        [Test]
        public void TheFourArgumentConstructor_LeavesInstantly()
        {
            MakeyBrain brain = ShieldedBrain();
            brain.Leave();
            Assert.That(brain.StateProgress, Is.EqualTo(1f));

            brain.Tick(0.001f);

            Assert.That(brain.State, Is.EqualTo(MakeyState.Away));
        }

        private static MakeyBrain ShieldedLeavingBrain()
        {
            MakeyBrain brain = NewLeavingCapableBrain();
            brain.Enter();
            brain.Tick(EnterSeconds);
            return brain;
        }
    }
}
