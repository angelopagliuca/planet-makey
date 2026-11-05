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
        private const int PositionFieldCount = 3;
        private const string LoadVerb = "LOAD";
        private const int LoadFieldCount = 2;
        private const string StartVerb = "START";

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

        /// <summary>
        /// Reads <c>HIT &lt;x&gt; &lt;y&gt;</c>, a hit with no color: the game reports it with the color of
        /// the ball loaded in the cannon. The verb is not case sensitive, and surrounding whitespace
        /// is ignored. x and y must be finite numbers; they are not limited to 0 to 1.
        /// </summary>
        /// <returns>True and the position if the message is a valid color-less HIT; otherwise false.</returns>
        public static bool TryParsePosition(string message, out Vector2 position)
        {
            position = default;

            string[] fields = SplitFields(message);
            if (fields == null || fields.Length != PositionFieldCount)
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

            position = new Vector2(x, y);
            return true;
        }

        /// <summary>
        /// Reads <c>LOAD &lt;color&gt;</c>, sent by the cannon's color sensor when a ball is loaded. The
        /// verb and the color are not case sensitive, and surrounding whitespace is ignored.
        /// </summary>
        /// <returns>True and the color if the message is a valid LOAD; otherwise false.</returns>
        public static bool TryParseLoad(string message, out BallColor color)
        {
            color = default;

            string[] fields = SplitFields(message);
            if (fields == null || fields.Length != LoadFieldCount)
            {
                return false;
            }

            if (!string.Equals(fields[0], LoadVerb, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return TryParseColor(fields[1], out color);
        }

        /// <summary>
        /// True if the message is <c>START</c>, the booth's start button. The verb is not case
        /// sensitive and surrounding whitespace is ignored; anything else on the line makes it invalid.
        /// </summary>
        public static bool IsStart(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            // A null separator splits on any whitespace, which also drops a trailing newline.
            string[] fields = message.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            return fields.Length == 1 && string.Equals(fields[0], StartVerb, StringComparison.OrdinalIgnoreCase);
        }

        // Null for a message with nothing in it. A null separator splits on any whitespace, which
        // also drops a trailing line end.
        private static string[] SplitFields(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            return message.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
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
