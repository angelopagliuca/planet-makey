using System;
using System.Collections.Generic;
using NUnit.Framework;
using PlanetMakey.Game.Cows;
using PlanetMakey.Game.Ufo;

namespace PlanetMakey.Tests.EditMode
{
    public sealed class AbductionBrainTests
    {
        private const float Approach = 1f;
        private const float Beam = 1f;
        private const float Lift = 4f;
        private const float Celebrate = 1f;

        private static AbductionBrain NewBrain(Herd herd, int seed = 1)
        {
            return new AbductionBrain(herd, Approach, Beam, Lift, Celebrate, new Random(seed));
        }

        // Runs one attempt up to the start of the lift.
        private static void ToLifting(AbductionBrain brain)
        {
            brain.Begin();
            brain.Tick(Approach);
            brain.Tick(Beam);
        }

        [Test]
        public void NewBrain_IsIdle_WithNoTarget()
        {
            AbductionBrain brain = NewBrain(new Herd(3));

            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.Idle));
            Assert.That(brain.TargetIndex, Is.EqualTo(-1));
            Assert.That(brain.PhaseProgress, Is.EqualTo(0f));
        }

        [Test]
        public void Begin_PicksACow_AndStartsApproaching()
        {
            var herd = new Herd(3);
            AbductionBrain brain = NewBrain(herd);
            var events = new List<string>();
            brain.PhaseChanged += phase => events.Add("phase:" + phase);
            brain.CowTargeted += index => events.Add("targeted:" + index);

            brain.Begin();

            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.Approaching));
            Assert.That(brain.TargetIndex, Is.InRange(0, 2));
            Assert.That(herd.GetState(brain.TargetIndex), Is.EqualTo(CowState.Targeted));
            Assert.That(events, Is.EqualTo(new[] { "phase:Approaching", "targeted:" + brain.TargetIndex }));
        }

        [Test]
        public void Begin_WhileAnAttemptIsRunning_DoesNothing()
        {
            var herd = new Herd(3);
            AbductionBrain brain = NewBrain(herd);
            brain.Begin();
            int target = brain.TargetIndex;

            brain.Begin();

            Assert.That(brain.TargetIndex, Is.EqualTo(target));
            Assert.That(herd.FreeCount, Is.EqualTo(2));
        }

        [Test]
        public void AnAttempt_GoesThroughEveryPhase_ThenAbsorbsTheCow()
        {
            var herd = new Herd(3);
            AbductionBrain brain = NewBrain(herd);
            var absorbed = new List<int>();
            brain.CowAbsorbed += absorbed.Add;
            brain.Begin();
            int target = brain.TargetIndex;

            brain.Tick(Approach);
            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.LoweringBeam));
            Assert.That(brain.TargetIndex, Is.EqualTo(target));

            brain.Tick(Beam);
            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.Lifting));

            brain.Tick(Lift - 1f);
            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.Lifting));
            Assert.That(brain.PhaseProgress, Is.EqualTo(0.75f).Within(1e-5f));

            brain.Tick(1f);
            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.Celebrating));
            Assert.That(brain.TargetIndex, Is.EqualTo(-1), "the cow is gone, so there is no target");
            Assert.That(herd.GetState(target), Is.EqualTo(CowState.Gone));
            Assert.That(herd.RemainingCount, Is.EqualTo(2));
            Assert.That(absorbed, Is.EqualTo(new[] { target }));
        }

        [Test]
        public void PhaseProgress_RisesThroughEachPhase()
        {
            AbductionBrain brain = NewBrain(new Herd(2));
            brain.Begin();

            brain.Tick(Approach * 0.5f);
            Assert.That(brain.PhaseProgress, Is.EqualTo(0.5f).Within(1e-5f));

            brain.Tick(Approach * 0.5f);
            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.LoweringBeam));
            Assert.That(brain.PhaseProgress, Is.EqualTo(0f));
        }

        [Test]
        public void AfterCelebrating_ThePhaseGoesIdleThenTheNextCowIsPicked_AndItIsADifferentOne()
        {
            var herd = new Herd(3);
            AbductionBrain brain = NewBrain(herd);
            ToLifting(brain);
            int first = brain.TargetIndex;
            brain.Tick(Lift);
            var phases = new List<AbductionPhase>();
            brain.PhaseChanged += phases.Add;

            brain.Tick(Celebrate);

            Assert.That(phases, Is.EqualTo(new[] { AbductionPhase.Idle, AbductionPhase.Approaching }));
            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.Approaching));
            Assert.That(brain.TargetIndex, Is.Not.EqualTo(first));
            Assert.That(herd.GetState(brain.TargetIndex), Is.EqualTo(CowState.Targeted));
        }

        [Test]
        public void LosingEveryCow_RaisesHerdEmpty_AndTheBrainStaysIdle()
        {
            var herd = new Herd(2);
            AbductionBrain brain = NewBrain(herd);
            int empty = 0;
            brain.HerdEmpty += () => empty++;

            for (int cow = 0; cow < 2; cow++)
            {
                ToLifting(brain);
                brain.Tick(Lift);
                brain.Tick(Celebrate);
            }

            Assert.That(herd.RemainingCount, Is.EqualTo(0));
            Assert.That(empty, Is.EqualTo(1));
            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.Idle));
            Assert.That(brain.TargetIndex, Is.EqualTo(-1));
        }

        [Test]
        public void Begin_WithEveryCowLost_RaisesHerdEmptyEachTime()
        {
            var herd = new Herd(1);
            AbductionBrain brain = NewBrain(herd);
            ToLifting(brain);
            brain.Tick(Lift);
            brain.Tick(Celebrate);
            int empty = 0;
            brain.HerdEmpty += () => empty++;

            brain.Begin();
            brain.Begin();

            Assert.That(empty, Is.EqualTo(2));
            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.Idle));
        }

        [Test]
        public void Begin_OnAnEmptyHerd_RaisesHerdEmpty()
        {
            AbductionBrain brain = NewBrain(new Herd(0));
            int empty = 0;
            brain.HerdEmpty += () => empty++;

            brain.Begin();

            Assert.That(empty, Is.EqualTo(1));
            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.Idle));
        }

        [Test]
        public void Abort_WhileApproaching_ReleasesTheCow_WithoutItHavingLeftTheGround()
        {
            AssertAbort(brain => brain.Begin(), expectedWasLifted: false);
        }

        [Test]
        public void Abort_WhileLoweringTheBeam_ReleasesTheCow_WithoutItHavingLeftTheGround()
        {
            AssertAbort(
                brain =>
                {
                    brain.Begin();
                    brain.Tick(Approach);
                },
                expectedWasLifted: false);
        }

        [Test]
        public void Abort_WhileLifting_DropsTheCow_AndSaysItHadStartedToRise()
        {
            AssertAbort(brain => ToLifting(brain), expectedWasLifted: true);
        }

        private static void AssertAbort(Action<AbductionBrain> getTo, bool expectedWasLifted)
        {
            var herd = new Herd(3);
            AbductionBrain brain = NewBrain(herd);
            var drops = new List<string>();
            brain.CowDropped += (index, wasLifted) => drops.Add(index + ":" + wasLifted);
            getTo(brain);
            int target = brain.TargetIndex;

            brain.Abort();

            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.Idle));
            Assert.That(brain.TargetIndex, Is.EqualTo(-1));
            Assert.That(herd.GetState(target), Is.EqualTo(CowState.Free));
            Assert.That(herd.RemainingCount, Is.EqualTo(3));
            Assert.That(drops, Is.EqualTo(new[] { target + ":" + expectedWasLifted }));
        }

        [Test]
        public void Abort_WhileCelebrating_DropsNothing_AndTheAbsorbedCowStaysGone()
        {
            var herd = new Herd(3);
            AbductionBrain brain = NewBrain(herd);
            ToLifting(brain);
            int lost = brain.TargetIndex;
            brain.Tick(Lift);
            int drops = 0;
            brain.CowDropped += (index, wasLifted) => drops++;

            brain.Abort();

            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.Idle));
            Assert.That(drops, Is.EqualTo(0));
            Assert.That(herd.GetState(lost), Is.EqualTo(CowState.Gone));
        }

        [Test]
        public void Abort_WhenIdle_DoesNothing()
        {
            AbductionBrain brain = NewBrain(new Herd(2));
            int events = 0;
            brain.PhaseChanged += phase => events++;
            brain.CowDropped += (index, wasLifted) => events++;

            brain.Abort();

            Assert.That(events, Is.EqualTo(0));
        }

        [Test]
        public void AfterADrop_TheNextCowIsADifferentOne_WhenOthersAreFree()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var herd = new Herd(3);
                AbductionBrain brain = NewBrain(herd, seed);
                brain.Begin();
                int dropped = brain.TargetIndex;
                brain.Abort();

                brain.Begin();

                Assert.That(brain.TargetIndex, Is.Not.EqualTo(dropped), "seed " + seed);
            }
        }

        [Test]
        public void AfterADrop_ASingleCowIsPickedAgain()
        {
            var herd = new Herd(1);
            AbductionBrain brain = NewBrain(herd);
            brain.Begin();
            brain.Abort();

            brain.Begin();

            Assert.That(brain.TargetIndex, Is.EqualTo(0));
            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.Approaching));
        }

        [Test]
        public void Tick_WithZeroNegativeOrNotANumber_ChangesNothing()
        {
            AbductionBrain brain = NewBrain(new Herd(2));
            brain.Begin();

            brain.Tick(0f);
            brain.Tick(-3f);
            brain.Tick(float.NaN);

            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.Approaching));
            Assert.That(brain.PhaseProgress, Is.EqualTo(0f));
        }

        [Test]
        public void Tick_WhileIdle_DoesNothing()
        {
            AbductionBrain brain = NewBrain(new Herd(2));

            brain.Tick(100f);

            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.Idle));
        }

        [Test]
        public void Tick_WithAHugeStep_EndsOnlyTheCurrentPhase()
        {
            AbductionBrain brain = NewBrain(new Herd(3));
            brain.Begin();

            brain.Tick(1000f);
            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.LoweringBeam));

            brain.Tick(1000f);
            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.Lifting));

            brain.Tick(1000f);
            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.Celebrating));

            brain.Tick(1000f);
            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.Approaching), "celebrating ended and the next cow was picked");
        }

        [Test]
        public void ZeroDurations_EndEachPhaseOnTheNextTick()
        {
            var brain = new AbductionBrain(new Herd(3), 0f, 0f, 0f, 0f, new Random(1));
            brain.Begin();
            Assert.That(brain.PhaseProgress, Is.EqualTo(1f));

            brain.Tick(0.001f);
            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.LoweringBeam));

            brain.Tick(0.001f);
            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.Lifting));

            brain.Tick(0.001f);
            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.Celebrating));
        }

        [Test]
        public void NegativeDurations_CountAsZero()
        {
            var brain = new AbductionBrain(new Herd(2), -1f, -1f, -1f, -1f, new Random(1));
            brain.Begin();

            brain.Tick(0.001f);

            Assert.That(brain.Phase, Is.EqualTo(AbductionPhase.LoweringBeam));
        }

        [Test]
        public void EveryCowIsEventuallyTaken_NeverTheSameCowTwice()
        {
            var herd = new Herd(6);
            AbductionBrain brain = NewBrain(herd, 9);
            var taken = new List<int>();
            brain.CowAbsorbed += taken.Add;

            for (int i = 0; i < 6; i++)
            {
                ToLifting(brain);
                brain.Tick(Lift);
                brain.Tick(Celebrate);
            }

            Assert.That(taken.Count, Is.EqualTo(6));
            Assert.That(new HashSet<int>(taken).Count, Is.EqualTo(6));
            Assert.That(herd.RemainingCount, Is.EqualTo(0));
        }

        [Test]
        public void Constructor_WithoutAHerdOrARandom_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new AbductionBrain(null, 1f, 1f, 1f, 1f, new Random(1)));
            Assert.Throws<ArgumentNullException>(() => new AbductionBrain(new Herd(1), 1f, 1f, 1f, 1f, null));
        }
    }
}
