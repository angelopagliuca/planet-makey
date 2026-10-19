using NUnit.Framework;
using PlanetMakey.Hits;
using UnityEngine;

namespace PlanetMakey.Tests.EditMode
{
    public sealed class HomographyTests
    {
        private const float Tolerance = 1e-4f;

        // Counter-clockwise from the bottom left, the order the calibration targets use.
        private static Vector2[] UnitSquare()
        {
            return new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
        }

        private static void AssertNear(Vector2 expected, Vector2 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance), "x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance), "y");
        }

        [Test]
        public void Identity_LeavesPointsUnchanged()
        {
            AssertNear(new Vector2(0.3f, 0.7f), Homography.Identity.Apply(new Vector2(0.3f, 0.7f)));
            AssertNear(new Vector2(-2f, 5f), Homography.Identity.Apply(new Vector2(-2f, 5f)));
        }

        [Test]
        public void TrySolve_SameQuadTwice_BehavesAsIdentity()
        {
            bool solved = Homography.TrySolve(UnitSquare(), UnitSquare(), out Homography h);

            Assert.That(solved, Is.True);
            AssertNear(new Vector2(0.25f, 0.75f), h.Apply(new Vector2(0.25f, 0.75f)));
        }

        [Test]
        public void TrySolve_ScaleAndOffset_RecoversTheMappingAndMapsOtherPoints()
        {
            // The mapping is p -> 0.5 * p + (0.1, 0.2).
            Vector2[] destination =
            {
                new Vector2(0.1f, 0.2f), new Vector2(0.6f, 0.2f), new Vector2(0.6f, 0.7f), new Vector2(0.1f, 0.7f)
            };

            bool solved = Homography.TrySolve(UnitSquare(), destination, out Homography h);

            Assert.That(solved, Is.True);
            AssertNear(new Vector2(0.35f, 0.45f), h.Apply(new Vector2(0.5f, 0.5f)));
            AssertNear(new Vector2(0.2f, 0.6f), h.Apply(new Vector2(0.2f, 0.8f)));
        }

        [Test]
        public void TrySolve_SquareToTrapezoid_MapsCornersAndAKnownInteriorPoint()
        {
            // By hand: u = (x + 0.5 y) / (1 + y), v = 2 y / (1 + y). The top edge is half as wide
            // as the bottom edge, as seen by a sensor looking at the wall from the side.
            Vector2[] destination =
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.75f, 1f), new Vector2(0.25f, 1f)
            };

            bool solved = Homography.TrySolve(UnitSquare(), destination, out Homography h);

            Assert.That(solved, Is.True);
            AssertNear(destination[0], h.Apply(new Vector2(0f, 0f)));
            AssertNear(destination[1], h.Apply(new Vector2(1f, 0f)));
            AssertNear(destination[2], h.Apply(new Vector2(1f, 1f)));
            AssertNear(destination[3], h.Apply(new Vector2(0f, 1f)));
            AssertNear(new Vector2(0.5f, 2f / 3f), h.Apply(new Vector2(0.5f, 0.5f)));
            AssertNear(new Vector2(5f / 6f, 2f / 3f), h.Apply(new Vector2(1f, 0.5f)));
        }

        [Test]
        public void TrySolve_MirroredQuad_Succeeds()
        {
            // Left and right swapped: a sensor that reports x backwards still has a valid mapping.
            Vector2[] mirrored =
            {
                new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f)
            };

            bool solved = Homography.TrySolve(UnitSquare(), mirrored, out Homography h);

            Assert.That(solved, Is.True);
            AssertNear(new Vector2(0.75f, 0.5f), h.Apply(new Vector2(0.25f, 0.5f)));
        }

        [Test]
        public void TrySolve_ThreePointsInALine_Fails()
        {
            Vector2[] source =
            {
                new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f)
            };

            Assert.That(Homography.TrySolve(source, UnitSquare(), out _), Is.False);
        }

        [Test]
        public void TrySolve_RepeatedPoint_Fails()
        {
            Vector2[] source =
            {
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
            };

            Assert.That(Homography.TrySolve(source, UnitSquare(), out _), Is.False);
        }

        [Test]
        public void TrySolve_PointsInCrossingOrder_Fails()
        {
            // Bottom left, top right, bottom right, top left: the outline crosses itself.
            Vector2[] source =
            {
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 1f)
            };

            Assert.That(Homography.TrySolve(source, UnitSquare(), out _), Is.False);
        }

        [Test]
        public void TrySolve_WrongPointCountOrNull_Fails()
        {
            Vector2[] three = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f) };

            Assert.That(Homography.TrySolve(three, UnitSquare(), out _), Is.False);
            Assert.That(Homography.TrySolve(UnitSquare(), three, out _), Is.False);
            Assert.That(Homography.TrySolve(null, UnitSquare(), out _), Is.False);
            Assert.That(Homography.TrySolve(UnitSquare(), null, out _), Is.False);
        }

        [Test]
        public void TrySolve_NotANumberPoint_Fails()
        {
            Vector2[] source = UnitSquare();
            source[2] = new Vector2(float.NaN, 1f);

            Assert.That(Homography.TrySolve(source, UnitSquare(), out _), Is.False);
        }

        [Test]
        public void ToArray_ThenTryFromArray_GivesTheSameMapping()
        {
            Vector2[] destination =
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.75f, 1f), new Vector2(0.25f, 1f)
            };
            bool solved = Homography.TrySolve(UnitSquare(), destination, out Homography original);

            bool restored = Homography.TryFromArray(original.ToArray(), out Homography copy);

            Assert.That(solved, Is.True);
            Assert.That(restored, Is.True);
            AssertNear(original.Apply(new Vector2(0.3f, 0.6f)), copy.Apply(new Vector2(0.3f, 0.6f)));
        }

        [Test]
        public void TryFromArray_RejectsWrongLengthNullNonFiniteAndSingularValues()
        {
            Assert.That(Homography.TryFromArray(null, out _), Is.False);
            Assert.That(Homography.TryFromArray(new float[8], out _), Is.False);
            Assert.That(Homography.TryFromArray(new float[9], out _), Is.False, "all zero");
            Assert.That(
                Homography.TryFromArray(new[] { 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, float.NaN }, out _),
                Is.False,
                "NaN entry");
            Assert.That(
                Homography.TryFromArray(new[] { 1f, 0f, 0f, 2f, 0f, 0f, 0f, 0f, 1f }, out _),
                Is.False,
                "rows collapse the plane onto a line");
        }

        [Test]
        public void Apply_PointSentToInfinity_ReturnsNotANumber()
        {
            // The weight is x - 0.5, which is zero along x = 0.5.
            bool built = Homography.TryFromArray(new[] { 1f, 0f, 0f, 0f, 1f, 0f, 1f, 0f, -0.5f }, out Homography h);

            Vector2 result = h.Apply(new Vector2(0.5f, 0.3f));

            Assert.That(built, Is.True);
            Assert.That(float.IsNaN(result.x), Is.True);
            Assert.That(float.IsNaN(result.y), Is.True);
        }
    }
}
