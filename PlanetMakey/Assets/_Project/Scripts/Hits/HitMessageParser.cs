using System;
using System.Globalization;
using PlanetMakey.Core;
using UnityEngine;

namespace PlanetMakey.Hits
{
    /// <summary>
    /// Reads the text messages the booth hardware sends. The format is described in
    /// PROTOCOL.md at the repository root. Plain C# with no scene objects, so it can be tested
    /// on its own.
    /// </summary>
    public static class HitMessageParser
    {
        private const string HitVerb = "HIT";
        private const int HitFieldCount = 4;

        /// <summary>
        /// Reads <c>HIT &lt;x&gt; &lt;y&gt; &lt;color&gt;</c>. The verb and the color are not case
        /// sensitive, and surrounding whitespace is ignored. x and y must be finite numbers; they
        /// are not limited to 0 to 1, because the hit resolver decides what is off the picture.
        /// </summary>
        /// <returns>True and the hit if the message is a valid HIT; otherwise false.</returns>
        public static bool TryParseHit(string message, out BallHit hit)
        {
            hit = default;

            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            // A null separator splits on any whitespace, which also drops a trailing newline.
            string[] fields = message.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != HitFieldCount)
            {
                return false;
            }

            if (!string.Equals(fields[0], HitVerb, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!TryParseNumber(fields[1], out float x) || !TryParseNumber(fields[2], out float y))
            {
                return false;
            }

            if (!TryParseColor(fields[3], out BallColor color))
            {
                return false;
            }

            hit = new BallHit(new Vector2(x, y), color);
            return true;
        }

        private static bool TryParseNumber(string token, out float value)
        {
            // The invariant culture keeps "." as the decimal point whatever the PC's language is.
            if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return false;
            }

            // "NaN" and "Infinity" parse as numbers but are never a position on the wall.
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool TryParseColor(string token, out BallColor color)
        {
            // Matched by name and not with Enum.TryParse, which would also accept "2" or "Red,Blue".
            if (string.Equals(token, "RED", StringComparison.OrdinalIgnoreCase))
            {
                color = BallColor.Red;
                return true;
            }

            if (string.Equals(token, "BLUE", StringComparison.OrdinalIgnoreCase))
            {
                color = BallColor.Blue;
                return true;
            }

            if (string.Equals(token, "YELLOW", StringComparison.OrdinalIgnoreCase))
            {
                color = BallColor.Yellow;
                return true;
            }

            if (string.Equals(token, "GREEN", StringComparison.OrdinalIgnoreCase))
            {
                color = BallColor.Green;
                return true;
            }

            color = default;
            return false;
        }
    }
}
