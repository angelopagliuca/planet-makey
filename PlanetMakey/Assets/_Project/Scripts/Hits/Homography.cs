using System;
using UnityEngine;

namespace PlanetMakey.Hits
{
    /// <summary>
    /// A projective mapping of the plane: it can shift, scale, rotate and skew, and it also
    /// handles a sensor or projector that is mounted off-axis. It is used to turn a position
    /// reported by the wall sensor into a position in the projected picture. Plain C# with no
    /// scene objects, so it can be tested on its own.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Identity"/> or <see cref="TrySolve"/> to get one. A value made with
    /// <c>default</c> has all-zero entries and maps every point to NaN.
    /// </remarks>
    public readonly struct Homography
    {
        private const int PointCount = 4;
        private const int MatrixLength = 9;
        private const double PivotEpsilon = 1e-9;
        private const double DeterminantEpsilon = 1e-9;
        private const float WeightEpsilon = 1e-9f;
        private const float CrossEpsilon = 1e-6f;

        private readonly float _m00;
        private readonly float _m01;
        private readonly float _m02;
        private readonly float _m10;
        private readonly float _m11;
        private readonly float _m12;
        private readonly float _m20;
        private readonly float _m21;
        private readonly float _m22;

        private Homography(
            float m00, float m01, float m02,
            float m10, float m11, float m12,
            float m20, float m21, float m22)
        {
            _m00 = m00;
            _m01 = m01;
            _m02 = m02;
            _m10 = m10;
            _m11 = m11;
            _m12 = m12;
            _m20 = m20;
            _m21 = m21;
            _m22 = m22;
        }

        /// <summary>The mapping that leaves every point where it is.</summary>
        public static Homography Identity => new Homography(1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f);

        /// <summary>
        /// Finds the mapping that takes each of the four <paramref name="source"/> points to the
        /// matching <paramref name="destination"/> point.
        /// </summary>
        /// <returns>
        /// False if either set is not four points, or is not a proper convex quadrilateral.
        /// That covers three points in a line, a repeated point, and points given in an order
        /// that makes the shape cross itself.
        /// </returns>
        public static bool TrySolve(Vector2[] source, Vector2[] destination, out Homography result)
        {
            result = Identity;

            if (source == null || destination == null || source.Length != PointCount || destination.Length != PointCount)
            {
                return false;
            }

            if (!IsConvexQuad(source) || !IsConvexQuad(destination))
            {
                return false;
            }

            // Eight unknowns (the ninth entry is fixed to 1), two equations per point pair.
            // Each row is the equation as coefficients followed by the right-hand side.
            double[,] system = new double[8, 9];
            for (int i = 0; i < PointCount; i++)
            {
                double x = source[i].x;
                double y = source[i].y;
                double u = destination[i].x;
                double v = destination[i].y;

                int row = 2 * i;
                system[row, 0] = x;
                system[row, 1] = y;
                system[row, 2] = 1.0;
                system[row, 6] = -u * x;
                system[row, 7] = -u * y;
                system[row, 8] = u;

                system[row + 1, 3] = x;
                system[row + 1, 4] = y;
                system[row + 1, 5] = 1.0;
                system[row + 1, 6] = -v * x;
                system[row + 1, 7] = -v * y;
                system[row + 1, 8] = v;
            }

            double[] h = new double[8];
            if (!SolveLinearSystem(system, h))
            {
                return false;
            }

            float[] values =
            {
                (float)h[0], (float)h[1], (float)h[2],
                (float)h[3], (float)h[4], (float)h[5],
                (float)h[6], (float)h[7], 1f
            };

            return TryFromArray(values, out result);
        }

        /// <summary>
        /// Rebuilds a mapping from the nine values returned by <see cref="ToArray"/>.
        /// </summary>
        /// <returns>False if the values are the wrong count, not finite, or not a usable mapping.</returns>
        public static bool TryFromArray(float[] values, out Homography result)
        {
            result = Identity;

            if (values == null || values.Length != MatrixLength)
            {
                return false;
            }

            for (int i = 0; i < MatrixLength; i++)
            {
                if (!IsFinite(values[i]))
                {
                    return false;
                }
            }

            // A zero determinant squashes the plane onto a line or a point and cannot be undone.
            double determinant =
                (double)values[0] * ((double)values[4] * values[8] - (double)values[5] * values[7])
                - (double)values[1] * ((double)values[3] * values[8] - (double)values[5] * values[6])
                + (double)values[2] * ((double)values[3] * values[7] - (double)values[4] * values[6]);
            if (Math.Abs(determinant) < DeterminantEpsilon)
            {
                return false;
            }

            result = new Homography(
                values[0], values[1], values[2],
                values[3], values[4], values[5],
                values[6], values[7], values[8]);
            return true;
        }

        /// <summary>
        /// Maps a point. Returns (NaN, NaN) if the point is sent to infinity, which the hit
        /// resolver treats as off the picture.
        /// </summary>
        public Vector2 Apply(Vector2 point)
        {
            float weight = _m20 * point.x + _m21 * point.y + _m22;
            if (Mathf.Abs(weight) < WeightEpsilon)
            {
                return new Vector2(float.NaN, float.NaN);
            }

            float x = (_m00 * point.x + _m01 * point.y + _m02) / weight;
            float y = (_m10 * point.x + _m11 * point.y + _m12) / weight;
            return new Vector2(x, y);
        }

        /// <summary>The nine entries, row by row, for saving.</summary>
        public float[] ToArray()
        {
            return new[] { _m00, _m01, _m02, _m10, _m11, _m12, _m20, _m21, _m22 };
        }

        // A quadrilateral is convex and has no three points in a line when the four turns
        // around its corners all go the same way and none is flat. A shape that crosses itself
        // has turns in both directions.
        private static bool IsConvexQuad(Vector2[] points)
        {
            bool firstTurnsLeft = false;

            for (int i = 0; i < PointCount; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % PointCount];
                Vector2 c = points[(i + 2) % PointCount];

                float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);

                // Written so that NaN fails.
                if (!(Mathf.Abs(cross) >= CrossEpsilon))
                {
                    return false;
                }

                if (i == 0)
                {
                    firstTurnsLeft = cross > 0f;
                }
                else if ((cross > 0f) != firstTurnsLeft)
                {
                    return false;
                }
            }

            return true;
        }

        // Gaussian elimination with partial pivoting on an n by n+1 augmented matrix. The
        // matrix is overwritten. Returns false if the system has no single solution.
        private static bool SolveLinearSystem(double[,] augmented, double[] solution)
        {
            int n = solution.Length;

            for (int column = 0; column < n; column++)
            {
                int pivotRow = column;
                double largest = Math.Abs(augmented[column, column]);
                for (int row = column + 1; row < n; row++)
                {
                    double candidate = Math.Abs(augmented[row, column]);
                    if (candidate > largest)
                    {
                        largest = candidate;
                        pivotRow = row;
                    }
                }

                if (!(largest >= PivotEpsilon))
                {
                    return false;
                }

                if (pivotRow != column)
                {
                    for (int k = 0; k <= n; k++)
                    {
                        double swap = augmented[column, k];
                        augmented[column, k] = augmented[pivotRow, k];
                        augmented[pivotRow, k] = swap;
                    }
                }

                for (int row = column + 1; row < n; row++)
                {
                    double factor = augmented[row, column] / augmented[column, column];
                    for (int k = column; k <= n; k++)
                    {
                        augmented[row, k] -= factor * augmented[column, k];
                    }
                }
            }

            for (int row = n - 1; row >= 0; row--)
            {
                double sum = augmented[row, n];
                for (int k = row + 1; k < n; k++)
                {
                    sum -= augmented[row, k] * solution[k];
                }

                solution[row] = sum / augmented[row, row];
            }

            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
