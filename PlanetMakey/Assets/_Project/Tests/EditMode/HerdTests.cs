using System;
using System.Collections.Generic;
using NUnit.Framework;
using PlanetMakey.Game.Cows;

namespace PlanetMakey.Tests.EditMode
{
    public sealed class HerdTests
    {
        [Test]
        public void NewHerd_HasEveryCowFree()
        {
            var herd = new Herd(4);

            Assert.That(herd.Count, Is.EqualTo(4));
            Assert.That(herd.FreeCount, Is.EqualTo(4));
            Assert.That(herd.RemainingCount, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(herd.GetState(i), Is.EqualTo(CowState.Free));
            }
        }

        [Test]
        public void AHerdOfNoCows_IsAllowed_ButNotANegativeOne()
        {
            var empty = new Herd(0);

            Assert.That(empty.Count, Is.EqualTo(0));
            Assert.That(empty.RemainingCount, Is.EqualTo(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Herd(-1));
        }

        [Test]
        public void TryPickFree_MarksTheCowTargeted_AndStillCountsItAsSafe()
        {
            var herd = new Herd(3);

            bool picked = herd.TryPickFree(new Random(1), -1, out int index);

            Assert.That(picked, Is.True);
            Assert.That(index, Is.InRange(0, 2));
            Assert.That(herd.GetState(index), Is.EqualTo(CowState.Targeted));
            Assert.That(herd.FreeCount, Is.EqualTo(2));
            Assert.That(herd.RemainingCount, Is.EqualTo(3));
        }

        [Test]
        public void TryPickFree_ReturnsFalseAndMinusOne_WhenNoCowIsFree()
        {
            var herd = new Herd(1);
            herd.TryPickFree(new Random(1), -1, out int first);

            bool picked = herd.TryPickFree(new Random(1), -1, out int index);

            Assert.That(first, Is.EqualTo(0));
            Assert.That(picked, Is.False);
            Assert.That(index, Is.EqualTo(-1));
        }

        [Test]
        public void TryPickFree_OnAnEmptyHerd_ReturnsFalse()
        {
            Assert.That(new Herd(0).TryPickFree(new Random(1), -1, out int index), Is.False);
            Assert.That(index, Is.EqualTo(-1));
        }

        [Test]
        public void TryPickFree_NeverPicksATargetedOrGoneCow_AndPicksEachOnce()
        {
            var herd = new Herd(5);
            var random = new Random(3);
            var picked = new HashSet<int>();

            for (int i = 0; i < 5; i++)
            {
                Assert.That(herd.TryPickFree(random, -1, out int index), Is.True);
                Assert.That(picked.Add(index), Is.True, "cow " + index + " was picked twice");
            }

            Assert.That(herd.FreeCount, Is.EqualTo(0));
            Assert.That(herd.TryPickFree(random, -1, out _), Is.False);
        }

        [Test]
        public void TryPickFree_SkipsTheCowToAvoid_WhenAnotherIsFree()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var herd = new Herd(2);

                herd.TryPickFree(new Random(seed), 0, out int index);

                Assert.That(index, Is.EqualTo(1), "seed " + seed);
            }
        }

        [Test]
        public void TryPickFree_PicksTheCowToAvoid_WhenItIsTheOnlyOneFree()
        {
            var herd = new Herd(1);

            bool picked = herd.TryPickFree(new Random(1), 0, out int index);

            Assert.That(picked, Is.True);
            Assert.That(index, Is.EqualTo(0));
        }

        [Test]
        public void TryPickFree_IgnoresAnAvoidNumberThatIsNotACow()
        {
            var herd = new Herd(2);

            Assert.That(herd.TryPickFree(new Random(1), 99, out int index), Is.True);
            Assert.That(index, Is.InRange(0, 1));
        }

        [Test]
        public void TryPickFree_EventuallyPicksEveryFreeCow()
        {
            var seen = new HashSet<int>();
            var random = new Random(11);

            for (int i = 0; i < 200; i++)
            {
                var herd = new Herd(4);
                herd.TryPickFree(random, -1, out int index);
                seen.Add(index);
            }

            Assert.That(seen.Count, Is.EqualTo(4));
        }

        [Test]
        public void Release_PutsATargetedCowBackInTheHerd()
        {
            var herd = new Herd(2);
            herd.TryPickFree(new Random(1), -1, out int index);

            bool released = herd.Release(index);

            Assert.That(released, Is.True);
            Assert.That(herd.GetState(index), Is.EqualTo(CowState.Free));
            Assert.That(herd.FreeCount, Is.EqualTo(2));
        }

        [Test]
        public void Release_DoesNothing_ForACowThatIsNotTargeted()
        {
            var herd = new Herd(2);

            Assert.That(herd.Release(0), Is.False);
            Assert.That(herd.GetState(0), Is.EqualTo(CowState.Free));
        }

        [Test]
        public void Remove_LosesATargetedCow_ForGood()
        {
            var herd = new Herd(3);
            herd.TryPickFree(new Random(1), -1, out int index);

            bool removed = herd.Remove(index);

            Assert.That(removed, Is.True);
            Assert.That(herd.GetState(index), Is.EqualTo(CowState.Gone));
            Assert.That(herd.RemainingCount, Is.EqualTo(2));
            Assert.That(herd.FreeCount, Is.EqualTo(2));
            Assert.That(herd.Release(index), Is.False, "a lost cow cannot be released");
            Assert.That(herd.Remove(index), Is.False, "a lost cow cannot be lost again");
        }

        [Test]
        public void Remove_DoesNothing_ForACowThatIsNotTargeted()
        {
            var herd = new Herd(2);

            Assert.That(herd.Remove(0), Is.False);
            Assert.That(herd.RemainingCount, Is.EqualTo(2));
        }

        [Test]
        public void RemainingCount_FallsToZero_WhenEveryCowIsLost()
        {
            var herd = new Herd(3);
            var random = new Random(5);

            for (int i = 0; i < 3; i++)
            {
                herd.TryPickFree(random, -1, out int index);
                herd.Remove(index);
            }

            Assert.That(herd.RemainingCount, Is.EqualTo(0));
            Assert.That(herd.FreeCount, Is.EqualTo(0));
        }

        [Test]
        public void Reset_BringsEveryCowBack()
        {
            var herd = new Herd(3);
            var random = new Random(5);
            herd.TryPickFree(random, -1, out int lost);
            herd.Remove(lost);
            herd.TryPickFree(random, -1, out _);

            herd.Reset();

            Assert.That(herd.FreeCount, Is.EqualTo(3));
            Assert.That(herd.RemainingCount, Is.EqualTo(3));
        }

        [Test]
        public void Changed_IsRaisedForEveryRealChange_AndNotForNoOps()
        {
            var herd = new Herd(2);
            int changes = 0;
            herd.Changed += () => changes++;

            herd.TryPickFree(new Random(1), -1, out int index);
            Assert.That(changes, Is.EqualTo(1));

            herd.Release(index);
            Assert.That(changes, Is.EqualTo(2));

            herd.Release(index);
            herd.Remove(index);
            Assert.That(changes, Is.EqualTo(2), "no-ops raise nothing");

            herd.TryPickFree(new Random(1), -1, out index);
            herd.Remove(index);
            Assert.That(changes, Is.EqualTo(4));

            herd.Reset();
            Assert.That(changes, Is.EqualTo(5));
        }

        [Test]
        public void ANumberThatIsNotACow_Throws()
        {
            var herd = new Herd(2);

            Assert.Throws<ArgumentOutOfRangeException>(() => herd.GetState(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => herd.GetState(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => herd.Release(5));
            Assert.Throws<ArgumentOutOfRangeException>(() => herd.Remove(-1));
        }

        [Test]
        public void TryPickFree_WithoutARandom_Throws()
        {
            var herd = new Herd(2);

            Assert.Throws<ArgumentNullException>(() => herd.TryPickFree(null, -1, out _));
        }
    }
}
