using NUnit.Framework;
using PlanetMakey.Core;
using PlanetMakey.Hits;

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
    }
}
