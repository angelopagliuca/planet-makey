using NUnit.Framework;
using PlanetMakey.Core;
using PlanetMakey.Hits;
using UnityEngine;

namespace PlanetMakey.Tests.EditMode
{
    public sealed class HitMessageParserTests
    {
        [TestCase("START")]
        [TestCase("start")]
        [TestCase("Start")]
        [TestCase("  START  ")]
        [TestCase("START\n")]
        [TestCase("START\r\n")]
        [TestCase("\tSTART")]
        public void IsStart_AcceptsStart_InAnyCaseWithWhitespace(string message)
        {
            Assert.That(HitMessageParser.IsStart(message), Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("STARTX")]
        [TestCase("XSTART")]
        [TestCase("STAR T")]
        [TestCase("START 1")]
        [TestCase("START START")]
        [TestCase("STOP")]
        [TestCase("HELLO")]
        [TestCase("HIT 0.5 0.5 RED")]
        public void IsStart_RejectsAnythingElse(string message)
        {
            Assert.That(HitMessageParser.IsStart(message), Is.False);
        }

        [Test]
        public void Start_IsNotAHit()
        {
            Assert.That(HitMessageParser.TryParseHit("START", out BallHit _), Is.False);
        }

        [Test]
        public void AHit_IsStillAHit_AndNotAStart()
        {
            bool parsed = HitMessageParser.TryParseHit("HIT 0.25 0.75 blue", out BallHit hit);

            Assert.That(parsed, Is.True);
            Assert.That(hit.Color, Is.EqualTo(BallColor.Blue));
            Assert.That(hit.Position.x, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(hit.Position.y, Is.EqualTo(0.75f).Within(1e-5f));
            Assert.That(HitMessageParser.IsStart("HIT 0.25 0.75 blue"), Is.False);
        }

        [Test]
        public void TryParsePosition_ReadsTheTwoNumbers()
        {
            bool parsed = HitMessageParser.TryParsePosition("HIT 0.25 0.75", out Vector2 position);

            Assert.That(parsed, Is.True);
            Assert.That(position.x, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(position.y, Is.EqualTo(0.75f).Within(1e-5f));
        }

        [TestCase("hit 0.5 0.5")]
        [TestCase("Hit 0.5 0.5")]
        [TestCase("  HIT   0.5    0.5  ")]
        [TestCase("HIT 0.5 0.5\n")]
        [TestCase("HIT 0.5 0.5\r\n")]
        [TestCase("\tHIT 0.5 0.5")]
        [TestCase("HIT 0 0")]
        [TestCase("HIT 1 1")]
        [TestCase("HIT -0.2 1.5")]
        [TestCase("HIT 1e-1 5E-1")]
        [TestCase("HIT +.5 .5")]
        public void TryParsePosition_AcceptsValidMessages(string message)
        {
            Assert.That(HitMessageParser.TryParsePosition(message, out Vector2 _), Is.True);
        }

        [Test]
        public void TryParsePosition_ReadsExponentNotation()
        {
            HitMessageParser.TryParsePosition("HIT 1e-1 5E-1", out Vector2 position);

            Assert.That(position.x, Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(position.y, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("HIT")]
        [TestCase("HIT 0.5")]
        [TestCase("HIT a b")]
        [TestCase("HIT 0.5 b")]
        [TestCase("HIT 0,5 0,5")]
        [TestCase("HIT NaN 0.5")]
        [TestCase("HIT 0.5 Infinity")]
        [TestCase("HIT 1e999 0.5")]
        [TestCase("HIT 0.5 0.5 RED")]
        [TestCase("HIT 0.5 0.5 0.5")]
        [TestCase("HITS 0.5 0.5")]
        [TestCase("0.5 0.5")]
        [TestCase("LOAD RED")]
        [TestCase("START")]
        public void TryParsePosition_RejectsAnythingElse(string message)
        {
            Assert.That(HitMessageParser.TryParsePosition(message, out Vector2 _), Is.False);
        }

        [Test]
        public void AHitWithAColor_IsNotAPositionOnlyHit_AndTheOtherWayAround()
        {
            Assert.That(HitMessageParser.TryParseHit("HIT 0.5 0.5", out BallHit _), Is.False);
            Assert.That(HitMessageParser.TryParsePosition("HIT 0.5 0.5 RED", out Vector2 _), Is.False);
        }

        [TestCase("LOAD RED", BallColor.Red)]
        [TestCase("LOAD BLUE", BallColor.Blue)]
        [TestCase("LOAD YELLOW", BallColor.Yellow)]
        [TestCase("LOAD GREEN", BallColor.Green)]
        [TestCase("load red", BallColor.Red)]
        [TestCase("Load Blue", BallColor.Blue)]
        [TestCase("  LOAD   yellow  ", BallColor.Yellow)]
        [TestCase("LOAD GREEN\n", BallColor.Green)]
        [TestCase("LOAD GREEN\r\n", BallColor.Green)]
        [TestCase("\tLOAD RED", BallColor.Red)]
        public void TryParseLoad_ReadsTheColor(string message, BallColor expected)
        {
            bool parsed = HitMessageParser.TryParseLoad(message, out BallColor color);

            Assert.That(parsed, Is.True);
            Assert.That(color, Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("LOAD")]
        [TestCase("LOAD PURPLE")]
        [TestCase("LOAD 2")]
        [TestCase("LOAD RED BLUE")]
        [TestCase("LOAD RED,BLUE")]
        [TestCase("LOADRED")]
        [TestCase("LOAD  ")]
        [TestCase("RED")]
        [TestCase("UNLOAD RED")]
        [TestCase("START")]
        [TestCase("HIT 0.5 0.5 RED")]
        [TestCase("HIT 0.5 0.5")]
        public void TryParseLoad_RejectsAnythingElse(string message)
        {
            Assert.That(HitMessageParser.TryParseLoad(message, out BallColor _), Is.False);
        }

        [Test]
        public void Load_IsNotAHitAndNotAStart()
        {
            Assert.That(HitMessageParser.TryParseHit("LOAD RED", out BallHit _), Is.False);
            Assert.That(HitMessageParser.TryParsePosition("LOAD RED", out Vector2 _), Is.False);
            Assert.That(HitMessageParser.IsStart("LOAD RED"), Is.False);
        }
    }
}
