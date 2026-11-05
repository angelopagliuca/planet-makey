#!/usr/bin/env python3
"""
Connects to the game over TCP and sends fake hardware messages, so the whole hit pipeline and the
calibration can be tried before the real sensor and cannon are connected. Standard library only
(Python 3.6 or newer), so there is nothing to pip install.

Start the game in Play mode first, then run one of:

    python tcp_full_tests.py                    sweep, bad, noise, duplicates and cannon
    python tcp_full_tests.py sweep bad          only those tests
    python tcp_full_tests.py sweep --cannon     LOAD the ball color, then hits with no color
    python tcp_full_tests.py calibrate --sensor lidar
    python tcp_full_tests.py sweep --sensor lidar --delay 0.05
    python tcp_full_tests.py --host 192.168.1.20 --port 9000

Tests:
    sweep       a lap around the edge of the screen, then a zig-zag over all of it (0,0 to 1,1)
    bad         malformed messages that the game must reject, and odd but valid ones
    noise       many hits scattered around a few spots, like a jittery sensor
    duplicates  the same impact reported two or three times, like a lidar seeing a ball on
                consecutive sweeps (the hardware is supposed to send only one)
    cannon      the cannon loads each color in turn (LOAD), then the wall sensor reports hits with
                no color, which the game reports with the loaded color
    calibrate   walks you through the game's F9 calibration, then checks it (asks for Enter, so it
                is only run when named)

By default each hit carries its own color. With --cannon a hit has no color, and a LOAD message
is sent first whenever the color changes, the way the real cannon and wall sensor work.

The sensor options (--sensor, --rotate, ...) change where the fake sensor says a hit happened,
the way a badly aligned real sensor would. Calibrate with a setting, then run the other tests with
the same setting, and the markers in the game should land on the true positions.
"""

import argparse
import math
import random
import socket
import sys
import time

COLORS = ["RED", "BLUE", "YELLOW", "GREEN", "PURPLE", "ORANGE"]
CORNER_NAMES = ["bottom left", "bottom right", "top right", "top left"]
DEFAULT_TESTS = ["sweep", "bad", "noise", "duplicates", "cannon"]
ALL_TESTS = DEFAULT_TESTS + ["calibrate"]


class SensorModel(object):
    """How the fake sensor reports a position: the true position, then mounted wrongly.

    Everything is done around the center of the wall: flip, scale, rotate, then shift.
    """

    def __init__(self, rotate_deg=0.0, scale_x=1.0, scale_y=1.0, offset_x=0.0, offset_y=0.0,
                 mirror_x=False, mirror_y=False):
        self.rotate_deg = rotate_deg
        self.scale_x = scale_x
        self.scale_y = scale_y
        self.offset_x = offset_x
        self.offset_y = offset_y
        self.mirror_x = mirror_x
        self.mirror_y = mirror_y

    def apply(self, x, y):
        dx = x - 0.5
        dy = y - 0.5
        if self.mirror_x:
            dx = -dx
        if self.mirror_y:
            dy = -dy
        dx *= self.scale_x
        dy *= self.scale_y

        radians = math.radians(self.rotate_deg)
        cos = math.cos(radians)
        sin = math.sin(radians)
        rotated_x = dx * cos - dy * sin
        rotated_y = dx * sin + dy * cos
        return rotated_x + 0.5 + self.offset_x, rotated_y + 0.5 + self.offset_y

    def describe(self):
        return ("rotate {:.1f} deg, scale ({:.2f}, {:.2f}), offset ({:.2f}, {:.2f}), "
                "mirror x {}, mirror y {}").format(
                    self.rotate_deg, self.scale_x, self.scale_y, self.offset_x, self.offset_y,
                    self.mirror_x, self.mirror_y)


PRESETS = {
    "none": {},
    # A lidar bolted on by hand: a little rotated, a little too big in x, shifted off center.
    "lidar": dict(rotate_deg=4.0, scale_x=1.08, scale_y=0.94, offset_x=0.06, offset_y=-0.04),
    "mirrored": dict(mirror_x=True),
    "upside-down": dict(mirror_y=True),
    "harsh": dict(rotate_deg=25.0, scale_x=0.7, scale_y=1.3, offset_x=-0.15, offset_y=0.2),
}


class Sender(object):
    """One TCP connection to the game. Every message is one line, so a line end follows it."""

    def __init__(self, host, port, delay):
        self.address = (host, port)
        self.delay = delay
        self.count = 0
        try:
            self.sock = socket.create_connection(self.address, timeout=5)
        except OSError as error:
            raise SystemExit("Could not connect to the game at {}:{} ({}). "
                             "Is it in Play mode?".format(host, port, error))
        # Messages are tiny. Send each at once instead of waiting to fill a packet.
        self.sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)

    def send(self, payload, wait=None):
        self.sock.sendall(payload + b"\n")
        self.count += 1
        time.sleep(self.delay if wait is None else wait)

    def close(self):
        self.sock.close()


class Context(object):
    """Everything a test needs: where to send, how the sensor is mounted, and the options."""

    def __init__(self, sender, model, rng, args):
        self.sender = sender
        self.model = model
        self.rng = rng
        self.args = args
        self.expected_rejected = 0
        self.loaded = None

    def color_for(self, index):
        if self.args.color:
            return self.args.color
        return COLORS[index % len(COLORS)]

    def load(self, color):
        """Sends LOAD, as the cannon's color sensor does when a ball goes in."""
        message = "LOAD {}".format(color)
        print("  {:<26} the cannon loads a {} ball".format(message, color.lower()))
        self.sender.send(message.encode("ascii"))
        self.loaded = color

    def send_hit(self, x, y, color, noise=0.0, wait=None, note="", as_cannon=None):
        """Sends a HIT for the true position (x, y), as the fake sensor would report it.

        With the cannon on (--cannon, or as_cannon) the hit has no color, and a LOAD goes first if
        the cannon does not already hold this color.
        """
        sensor_x, sensor_y = self.model.apply(x, y)
        if noise > 0.0:
            sensor_x += self.rng.gauss(0.0, noise)
            sensor_y += self.rng.gauss(0.0, noise)

        use_cannon = self.args.cannon if as_cannon is None else as_cannon
        if use_cannon:
            if self.loaded != color:
                self.load(color)
            message = "HIT {:.4f} {:.4f}".format(sensor_x, sensor_y)
            note = "{} (ball {})".format(note, color.lower()).strip()
        else:
            message = "HIT {:.4f} {:.4f} {}".format(sensor_x, sensor_y, color)

        print("  {:<26} true ({:.2f}, {:.2f}) {}".format(message, x, y, note).rstrip())
        self.sender.send(message.encode("ascii"), wait)


# ---------------------------------------------------------------------------------------------
# sweep
# ---------------------------------------------------------------------------------------------

def perimeter_points(steps):
    """A lap around the edge of the screen, starting and ending at the bottom left."""
    corners = [(0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0), (0.0, 0.0)]
    points = []
    for (x0, y0), (x1, y1) in zip(corners, corners[1:]):
        for i in range(steps):
            t = i / float(steps)
            points.append((x0 + (x1 - x0) * t, y0 + (y1 - y0) * t))
    points.append((0.0, 0.0))
    return points


def raster_points(cells):
    """A zig-zag over the whole screen, row by row, so each step is a short move."""
    points = []
    for row in range(cells + 1):
        columns = list(range(cells + 1))
        if row % 2 == 1:
            columns.reverse()
        for column in columns:
            points.append((column / float(cells), row / float(cells)))
    return points


def run_sweep(ctx):
    print("\n== sweep: a lap around the edge, then a zig-zag over the whole screen ==")
    print("Expect: every message shows as a marker, and the rejected counter does not change.")

    index = 0
    print("Lap around the edge:")
    for x, y in perimeter_points(ctx.args.steps):
        ctx.send_hit(x, y, ctx.color_for(index))
        index += 1

    print("Zig-zag over the screen:")
    for x, y in raster_points(ctx.args.grid):
        ctx.send_hit(x, y, ctx.color_for(index))
        index += 1


# ---------------------------------------------------------------------------------------------
# bad
# ---------------------------------------------------------------------------------------------

def bad_messages(rng):
    """(payload, the game should reject it, what it is). Mirrors HitMessageParser in the game.

    Every payload is sent as one line. The random bytes leave out the line end, which would split
    them into several lines and make the count unpredictable.
    """
    garbage = bytes(b for b in (rng.getrandbits(8) for _ in range(32)) if b != 0x0A)
    return [
        # Must be rejected.
        (b"HELLO", True, "not a HIT"),
        (b"HIT", True, "no fields"),
        (b"HIT 0.5", True, "missing y"),
        (b"HIT 0.5 0.5 RED EXTRA", True, "one field too many"),
        (b"HIT 0.5 b", True, "y is not a number (no color)"),
        (b"LOAD", True, "LOAD with no color"),
        (b"LOAD PURPLE", True, "LOAD with an unknown color"),
        (b"LOAD 2", True, "LOAD with the color as a number"),
        (b"LOAD RED BLUE", True, "LOAD with two colors"),
        (b"LOADRED", True, "no space after LOAD"),
        (b"START 1", True, "START with something after it"),
        (b"STARTX", True, "not START"),
        (b"HIT a b RED", True, "x and y are not numbers"),
        (b"HIT 0.5 b RED", True, "y is not a number"),
        (b"HIT 0.5 0.5 PURPLE", True, "unknown color"),
        (b"HIT 0.5 0.5 2", True, "color given as a number"),
        (b"HIT 0.5 0.5 RED,BLUE", True, "two colors"),
        (b"HIT NaN 0.5 RED", True, "x is NaN"),
        (b"HIT 0.5 Infinity RED", True, "y is infinity"),
        (b"HIT 1e999 0.5 RED", True, "x is too big for a float"),
        (b"HIT 0,5 0,5 RED", True, "comma as decimal point"),
        (b"HIT,0.5,0.5,RED", True, "commas instead of spaces"),
        (b"hit0.5 0.5 RED", True, "no space after HIT"),
        (b"HIT 0.5 0.5 RED\x00", True, "stray zero byte"),
        (b"HIT 0.5 0.5 R\xc3\x89D", True, "non-ASCII letter in the color"),
        (b'{"x": 0.5, "y": 0.5, "color": "red"}', True, "JSON instead of text"),
        (b"A" * 5000, True, "5000 characters of junk in one line"),
        (garbage, True, "random bytes"),
        # Valid, so they must NOT be counted as rejected.
        (b"", False, "valid: a blank line, ignored and not counted"),
        (b"   ", False, "valid: only spaces, ignored and not counted"),
        (b"HIT 0.5 0.5 RED\nHIT 0.2 0.2 BLUE", False, "valid: two lines in one write, both read"),
        (b"HIT 0.5 0.5", False, "valid: no color (the loaded ball's; dropped if none)"),
        (b"hit 0.25 0.75", False, "valid: no color, lower case"),
        (b"LOAD RED", False, "valid: loads a red ball"),
        (b"load blue", False, "valid: lower case"),
        (b"  LOAD   Yellow  ", False, "valid: extra spaces"),
        (b"LOAD GREEN", False, "valid: loads a green ball (the cannon ends up green)"),
        (b"hit 0.5 0.5 red", False, "valid: lower case"),
        (b"  HIT   0.25    0.75   Blue  \r", False, "valid: extra spaces and a CRLF line end"),
        (b"HIT 1e-1 5E-1 GREEN", False, "valid: exponent notation -> (0.1, 0.5)"),
        (b"HIT +.5 .5 yellow", False, "valid: sign and no leading zero -> (0.5, 0.5)"),
        (b"HIT 0 0 RED", False, "valid: bottom left corner"),
        (b"HIT 1 1 RED", False, "valid: top right corner"),
        # Valid but off the picture: accepted, not rejected, and the game reports a miss.
        (b"HIT -0.2 0.5 RED", False, "valid, off the picture: left of the screen"),
        (b"HIT 1.5 0.5 BLUE", False, "valid, off the picture: right of the screen"),
        (b"HIT 0.5 -1 YELLOW", False, "valid, off the picture: below the screen"),
        (b"HIT 0.5 99999 GREEN", False, "valid, off the picture: far above the screen"),
    ]


def run_bad(ctx):
    print("\n== bad: messages the game must reject, and odd ones it must accept ==")
    print("Expect: no Console errors. The overlay line 'rejected' goes up only for the messages")
    print("marked REJECT. Messages marked ok are not rejected: most are hits (some land off the")
    print("picture), LOAD messages change the cannon, and blank lines are ignored. A hit with no")
    print("color is dropped, and counted under 'no ball', while the cannon is empty.")

    rejected = 0
    for payload, should_reject, what in bad_messages(ctx.rng):
        shown = repr(payload)
        if len(shown) > 52:
            shown = shown[:49] + "..."
        verdict = "REJECT" if should_reject else "ok"
        print("  {:<6} {:<54} {}".format(verdict, shown, what))
        ctx.sender.send(payload)
        if should_reject:
            rejected += 1

    ctx.expected_rejected += rejected
    print("The 'rejected' counter should have gone up by exactly {}.".format(rejected))


# ---------------------------------------------------------------------------------------------
# noise
# ---------------------------------------------------------------------------------------------

def run_noise(ctx):
    sigma = ctx.args.noise
    spots = [(0.2, 0.2), (0.8, 0.2), (0.5, 0.5), (0.8, 0.8), (0.2, 0.8)]
    print("\n== noise: {} hits around each of {} spots, jitter {} of the screen ==".format(
        ctx.args.noise_hits, len(spots), sigma))
    print("Expect: small clusters around each spot. On a 2.4 m wide wall, 0.004 is about 1 cm.")

    index = 0
    for x, y in spots:
        print("Spot ({:.2f}, {:.2f}):".format(x, y))
        for _ in range(ctx.args.noise_hits):
            ctx.send_hit(x, y, ctx.color_for(index), noise=sigma)
            index += 1


# ---------------------------------------------------------------------------------------------
# duplicates
# ---------------------------------------------------------------------------------------------

def run_duplicates(ctx):
    repeats = ctx.args.repeats
    gap = ctx.args.repeat_gap
    print("\n== duplicates: one impact reported {} times, {} s apart ==".format(repeats, gap))
    print("A lidar that sweeps 30 times a second can see one ball on two or three sweeps. The game")
    print("counts every message as a separate ball, so the hardware must send only ONE per impact.")
    print("Expect: {} markers stacked on each spot. If you see them with the real sensor, the".format(
        repeats))
    print("hardware side is sending duplicates and needs to merge them (see PROTOCOL.md).")

    index = 0
    for x, y in [(0.25, 0.25), (0.5, 0.5), (0.75, 0.75)]:
        print("Impact at ({:.2f}, {:.2f}):".format(x, y))
        color = ctx.color_for(index)
        for repeat in range(repeats):
            last = repeat == repeats - 1
            ctx.send_hit(x, y, color, wait=ctx.args.delay if last else gap)
        index += 1


# ---------------------------------------------------------------------------------------------
# cannon
# ---------------------------------------------------------------------------------------------

def run_cannon(ctx):
    print("\n== cannon: LOAD each color, then hits with no color ==")
    print("The cannon's color sensor sends LOAD when a ball goes in, and the wall sensor then sends")
    print("HIT x y with no color. The game reports the hit with the loaded color, and the color stays")
    print("until the next LOAD.")
    print("Expect: the Cannon row shows each color in turn, and both markers after a LOAD take it.")

    spots = [(0.25, 0.25), (0.75, 0.25), (0.75, 0.75), (0.25, 0.75)]
    for index, (x, y) in enumerate(spots):
        color = ctx.color_for(index)
        print("Ball {}:".format(index + 1))
        ctx.send_hit(x, y, color, as_cannon=True)
        ctx.send_hit(x + 0.05, y + 0.05, color, as_cannon=True, note="same ball, no new LOAD")


# ---------------------------------------------------------------------------------------------
# calibrate
# ---------------------------------------------------------------------------------------------

def run_calibrate(ctx):
    inset = ctx.args.inset
    targets = [(inset, inset), (1.0 - inset, inset), (1.0 - inset, 1.0 - inset), (inset, 1.0 - inset)]

    print("\n== calibrate: the game's F9 calibration with a fake sensor ==")
    print("Sensor: {}".format(ctx.model.describe()))
    print("Hits per target: {}, jitter {}, inset {}".format(
        ctx.args.hits_per_target, ctx.args.noise, inset))
    print("Make sure the game's CalibrationMode uses the same inset and Hits per target.")
    print("")
    print("1. In the game (Play mode), press F9. The first target appears at the bottom left.")
    wait_for_enter("   Press Enter here to send the four target hits...")

    index = 0
    for name, (x, y) in zip(CORNER_NAMES, targets):
        print("Target {}:".format(name))
        for _ in range(ctx.args.hits_per_target):
            ctx.send_hit(x, y, ctx.color_for(index), noise=ctx.args.noise)
            index += 1

    print("")
    print("2. The game should say 'Calibration saved' (or 'failed' if the points were bad).")
    print("   Failing on --sensor mirrored or upside-down would be a bug; they must work.")
    wait_for_enter("   Press Enter to send a check sweep through the same fake sensor...")

    print("Check: the markers should now trace the edge of the screen and then zig-zag over it.")
    index = 0
    for x, y in perimeter_points(ctx.args.steps) + raster_points(ctx.args.grid):
        ctx.send_hit(x, y, ctx.color_for(index), noise=ctx.args.noise)
        index += 1


def wait_for_enter(prompt):
    try:
        input(prompt)
    except EOFError:
        print("(no keyboard, continuing)")


# ---------------------------------------------------------------------------------------------
# main
# ---------------------------------------------------------------------------------------------

RUNNERS = {
    "sweep": run_sweep,
    "bad": run_bad,
    "noise": run_noise,
    "duplicates": run_duplicates,
    "cannon": run_cannon,
    "calibrate": run_calibrate,
}


def build_model(args):
    settings = dict(PRESETS[args.sensor])
    overrides = {
        "rotate_deg": args.rotate,
        "scale_x": args.scale_x,
        "scale_y": args.scale_y,
        "offset_x": args.offset_x,
        "offset_y": args.offset_y,
    }
    for key, value in overrides.items():
        if value is not None:
            settings[key] = value
    if args.mirror_x:
        settings["mirror_x"] = True
    if args.mirror_y:
        settings["mirror_y"] = True
    return SensorModel(**settings)


def parse_args(argv):
    parser = argparse.ArgumentParser(
        description="Connect to the game over TCP and send fake hardware messages.",
        epilog="Tests: " + ", ".join(ALL_TESTS) + ". With none named: " + ", ".join(DEFAULT_TESTS) + ".")
    parser.add_argument("tests", nargs="*", metavar="test", help="which tests to run")

    network = parser.add_argument_group("where to send")
    network.add_argument("--host", default="127.0.0.1", help="game PC address (default 127.0.0.1)")
    network.add_argument("--port", type=int, default=9000, help="game TCP port (default 9000)")
    network.add_argument("--delay", type=float, default=0.15,
                         help="seconds between messages (default 0.15)")

    path = parser.add_argument_group("what to send")
    path.add_argument("--color", type=str.upper, choices=COLORS,
                      help="use one color for every hit (default: cycle through all six)")
    path.add_argument("--cannon", action="store_true",
                      help="send hits with no color, with a LOAD before each change of color "
                           "(default: every hit carries its color)")
    path.add_argument("--steps", type=int, default=10,
                      help="points per side in the lap around the edge (default 10)")
    path.add_argument("--grid", type=int, default=10,
                      help="cells per side in the zig-zag (default 10, so 11 x 11 points)")
    path.add_argument("--seed", type=int, default=1, help="random seed, for repeatable runs")

    sensor = parser.add_argument_group("the fake sensor (how it is mounted)")
    sensor.add_argument("--sensor", choices=sorted(PRESETS), default="none",
                        help="a ready-made mounting (default none)")
    sensor.add_argument("--rotate", type=float, help="rotation in degrees, around the center")
    sensor.add_argument("--scale-x", type=float, help="horizontal scale, 1 is exact")
    sensor.add_argument("--scale-y", type=float, help="vertical scale, 1 is exact")
    sensor.add_argument("--offset-x", type=float, help="horizontal shift, as a fraction of the wall")
    sensor.add_argument("--offset-y", type=float, help="vertical shift, as a fraction of the wall")
    sensor.add_argument("--mirror-x", action="store_true", help="left and right swapped")
    sensor.add_argument("--mirror-y", action="store_true", help="top and bottom swapped")

    extra = parser.add_argument_group("noise, duplicates and calibrate")
    extra.add_argument("--noise", type=float, default=0.004,
                       help="jitter on every hit in noise and calibrate (default 0.004, about 1 cm "
                            "on a 2.4 m wall; use 0 for none)")
    extra.add_argument("--noise-hits", type=int, default=15, help="hits per spot in noise (default 15)")
    extra.add_argument("--repeats", type=int, default=3, help="messages per impact in duplicates (default 3)")
    extra.add_argument("--repeat-gap", type=float, default=0.033,
                       help="seconds between repeats, one sweep of a 30 Hz lidar (default 0.033)")
    extra.add_argument("--inset", type=float, default=0.1,
                       help="how far calibration targets sit inside the corners (default 0.1)")
    extra.add_argument("--hits-per-target", type=int, default=1,
                       help="hits sent per calibration target (default 1)")

    args = parser.parse_args(argv)

    for name in args.tests:
        if name not in ALL_TESTS:
            parser.error("unknown test '{}'. Choose from: {}".format(name, ", ".join(ALL_TESTS)))
    if args.steps < 1 or args.grid < 1:
        parser.error("--steps and --grid must be at least 1")
    if args.delay < 0 or args.repeat_gap < 0:
        parser.error("--delay and --repeat-gap cannot be negative")
    if args.repeats < 1 or args.noise_hits < 1 or args.hits_per_target < 1:
        parser.error("--repeats, --noise-hits and --hits-per-target must be at least 1")
    if not 0.0 <= args.inset < 0.5:
        parser.error("--inset must be from 0 up to (not including) 0.5")
    return args


def main(argv=None):
    args = parse_args(sys.argv[1:] if argv is None else argv)
    tests = args.tests or DEFAULT_TESTS

    model = build_model(args)
    rng = random.Random(args.seed)
    sender = Sender(args.host, args.port, args.delay)
    ctx = Context(sender, model, rng, args)

    print("Connected to {}:{}, {} s between messages".format(args.host, args.port, args.delay))
    print("Fake sensor: {}".format(model.describe()))
    print("Hits carry: {}".format("no color (LOAD first)" if args.cannon else "their own color"))
    print("Tests: {}".format(", ".join(tests)))

    try:
        for name in tests:
            RUNNERS[name](ctx)
    except KeyboardInterrupt:
        print("\nStopped.")
    except OSError as error:
        print("\nLost the connection to the game: {}".format(error))
    finally:
        sender.close()

    print("\nDone. {} messages sent.".format(sender.count))
    if ctx.expected_rejected:
        print("The game's 'rejected' counter should read {} more than before the run.".format(
            ctx.expected_rejected))
    return 0


if __name__ == "__main__":
    sys.exit(main())
