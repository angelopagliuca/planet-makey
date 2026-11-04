using System.Collections.Generic;
using NUnit.Framework;
using PlanetMakey.Game.Round;

namespace PlanetMakey.Tests.EditMode
{
    public sealed class RoundBrainTests
    {
        private const float DuskSeconds = 2f;
        private const float NightSeconds = 10f;
        private const float DawnSeconds = 4f;

        private static RoundBrain NewBrain()
        {
            return new RoundBrain(DuskSeconds, NightSeconds, DawnSeconds);
        }

        private static RoundBrain NightBrain()
        {
            RoundBrain brain = NewBrain();
            brain.RequestStart();
            brain.Tick(DuskSeconds);
            return brain;
        }

        [Test]
        public void NewBrain_IsIdle_WithTheFullNightAhead()
        {
            RoundBrain brain = NewBrain();

            Assert.That(brain.State, Is.EqualTo(RoundState.Idle));
            Assert.That(brain.StateProgress, Is.EqualTo(0f));
            Assert.That(brain.NightSecondsLeft, Is.EqualTo(NightSeconds));
        }

        [Test]
        public void Tick_WhileIdle_DoesNothing()
        {
            RoundBrain brain = NewBrain();

            brain.Tick(1000f);

            Assert.That(brain.State, Is.EqualTo(RoundState.Idle));
        }

        [Test]
        public void RequestStart_WhileIdle_StartsDusk()
        {
            RoundBrain brain = NewBrain();

            bool started = brain.RequestStart();

            Assert.That(started, Is.True);
            Assert.That(brain.State, Is.EqualTo(RoundState.Dusk));
        }

        [Test]
        public void RequestStart_WhenNotIdle_IsIgnored_InEveryState()
        {
            RoundBrain brain = NewBrain();
            brain.RequestStart();
            Assert.That(brain.RequestStart(), Is.False, "dusk");
            Assert.That(brain.State, Is.EqualTo(RoundState.Dusk));

            brain.Tick(DuskSeconds);
            Assert.That(brain.RequestStart(), Is.False, "night");
            Assert.That(brain.State, Is.EqualTo(RoundState.Night));

            brain.Tick(NightSeconds);
            Assert.That(brain.RequestStart(), Is.False, "dawn");
            Assert.That(brain.State, Is.EqualTo(RoundState.Dawn));
        }

        [Test]
        public void ARound_RunsDuskNightDawnIdle_ForTheirTimes()
        {
            RoundBrain brain = NewBrain();
            brain.RequestStart();

            brain.Tick(DuskSeconds - 0.5f);
            Assert.That(brain.State, Is.EqualTo(RoundState.Dusk));
            Assert.That(brain.StateProgress, Is.EqualTo((DuskSeconds - 0.5f) / DuskSeconds).Within(1e-5f));

            brain.Tick(0.5f);
            Assert.That(brain.State, Is.EqualTo(RoundState.Night));

            brain.Tick(NightSeconds - 0.5f);
            Assert.That(brain.State, Is.EqualTo(RoundState.Night));

            brain.Tick(0.5f);
            Assert.That(brain.State, Is.EqualTo(RoundState.Dawn));

            brain.Tick(DawnSeconds - 0.5f);
            Assert.That(brain.State, Is.EqualTo(RoundState.Dawn));

            brain.Tick(0.5f);
            Assert.That(brain.State, Is.EqualTo(RoundState.Idle));
        }

        [Test]
        public void NightSecondsLeft_CountsDown_DuringTheNight_AndIsZeroAtDawn()
        {
            RoundBrain brain = NightBrain();
            Assert.That(brain.NightSecondsLeft, Is.EqualTo(NightSeconds).Within(1e-5f));

            brain.Tick(3f);
            Assert.That(brain.NightSecondsLeft, Is.EqualTo(NightSeconds - 3f).Within(1e-5f));

            brain.Tick(NightSeconds);
            Assert.That(brain.State, Is.EqualTo(RoundState.Dawn));
            Assert.That(brain.NightSecondsLeft, Is.EqualTo(0f));
        }

        [Test]
        public void NightSecondsLeft_IsTheFullNight_DuringDusk()
        {
            RoundBrain brain = NewBrain();
            brain.RequestStart();
            brain.Tick(1f);

            Assert.That(brain.NightSecondsLeft, Is.EqualTo(NightSeconds));
        }

        [Test]
        public void EndNightEarly_DuringTheNight_StartsDawn_AndZeroesTheClock()
        {
            RoundBrain brain = NightBrain();
            brain.Tick(2f);

            brain.EndNightEarly();

            Assert.That(brain.State, Is.EqualTo(RoundState.Dawn));
            Assert.That(brain.NightSecondsLeft, Is.EqualTo(0f));
            Assert.That(brain.StateProgress, Is.EqualTo(0f));
        }

        [Test]
        public void EndNightEarly_OutsideTheNight_IsIgnored()
        {
            RoundBrain brain = NewBrain();
            brain.EndNightEarly();
            Assert.That(brain.State, Is.EqualTo(RoundState.Idle));

            brain.RequestStart();
            brain.EndNightEarly();
            Assert.That(brain.State, Is.EqualTo(RoundState.Dusk));

            brain.Tick(DuskSeconds);
            brain.Tick(NightSeconds);
            Assert.That(brain.State, Is.EqualTo(RoundState.Dawn));
            brain.EndNightEarly();
            Assert.That(brain.State, Is.EqualTo(RoundState.Dawn));
        }

        [Test]
        public void AfterTheRound_AnotherCanStart()
        {
            RoundBrain brain = NewBrain();
            brain.RequestStart();
            brain.Tick(DuskSeconds);
            brain.Tick(NightSeconds);
            brain.Tick(DawnSeconds);
            Assert.That(brain.State, Is.EqualTo(RoundState.Idle));

            Assert.That(brain.RequestStart(), Is.True);
            Assert.That(brain.State, Is.EqualTo(RoundState.Dusk));
            Assert.That(brain.NightSecondsLeft, Is.EqualTo(NightSeconds));
        }

        [Test]
        public void Tick_WithZeroNegativeOrNotANumber_ChangesNothing()
        {
            RoundBrain brain = NewBrain();
            brain.RequestStart();

            brain.Tick(0f);
            brain.Tick(-5f);
            brain.Tick(float.NaN);

            Assert.That(brain.State, Is.EqualTo(RoundState.Dusk));
            Assert.That(brain.StateProgress, Is.EqualTo(0f));
        }

        [Test]
        public void Tick_WithAHugeStep_EndsOnlyTheCurrentState()
        {
            RoundBrain brain = NewBrain();
            brain.RequestStart();

            brain.Tick(1000f);
            Assert.That(brain.State, Is.EqualTo(RoundState.Night));

            brain.Tick(1000f);
            Assert.That(brain.State, Is.EqualTo(RoundState.Dawn));

            brain.Tick(1000f);
            Assert.That(brain.State, Is.EqualTo(RoundState.Idle));
        }

        [Test]
        public void StateChanged_IsRaisedForEveryChange_InOrder()
        {
            RoundBrain brain = NewBrain();
            var states = new List<RoundState>();
            brain.StateChanged += states.Add;

            brain.RequestStart();
            brain.Tick(DuskSeconds);
            brain.Tick(NightSeconds);
            brain.Tick(DawnSeconds);

            Assert.That(
                states,
                Is.EqualTo(new[] { RoundState.Dusk, RoundState.Night, RoundState.Dawn, RoundState.Idle }));
        }

        [Test]
        public void StateChanged_IsNotRaised_ByIgnoredCalls()
        {
            RoundBrain brain = NewBrain();
            int count = 0;
            brain.StateChanged += state => count++;

            brain.EndNightEarly();
            brain.Tick(5f);
            brain.RequestStart();
            brain.RequestStart();
            brain.EndNightEarly();

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void ZeroDurations_EndEachStateOnTheNextTick()
        {
            var brain = new RoundBrain(0f, 0f, 0f);

            brain.RequestStart();
            Assert.That(brain.StateProgress, Is.EqualTo(1f));

            brain.Tick(0.001f);
            Assert.That(brain.State, Is.EqualTo(RoundState.Night));

            brain.Tick(0.001f);
            Assert.That(brain.State, Is.EqualTo(RoundState.Dawn));

            brain.Tick(0.001f);
            Assert.That(brain.State, Is.EqualTo(RoundState.Idle));
        }

        [Test]
        public void NegativeDurations_CountAsZero()
        {
            var brain = new RoundBrain(-1f, -1f, -1f);

            brain.RequestStart();
            brain.Tick(0.001f);

            Assert.That(brain.State, Is.EqualTo(RoundState.Night));
            Assert.That(brain.NightSecondsLeft, Is.EqualTo(0f));
        }
    }
}
