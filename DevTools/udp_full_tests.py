#!/usr/bin/env python3
"""
Sends fake sensor messages to the game over UDP, so the whole hit pipeline and the calibration
can be tried before the real sensor arrives. Standard library only (Python 3.6 or newer), so
there is nothing to pip install.

Start the game in Play mode first, then run one of:

    python udp_full_tests.py                    sweep, bad, noise and duplicates
    python udp_full_tests.py sweep bad          only those tests
    python udp_full_tests.py calibrate --sensor lidar
    python udp_full_tests.py sweep --sensor lidar --delay 0.05
    python udp_full_tests.py --host 192.168.1.20 --port 9000

Tests:
    sweep       a lap around the edge of the screen, then a zig-zag over all of it (0,0 to 1,1)
    bad         malformed messages that the game must reject, and odd but valid ones
    noise       many hits scattered around a few spots, like a jittery sensor
    duplicates  the same impact reported two or three times, like a lidar seeing a ball on
                consecutive sweeps (the hardware is supposed to send only one)
    calibrate   walks you through the game's F9 calibration, then checks it (asks for Enter, so it
                is only run when named)

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

COLORS = ["RED", "BLUE", "YELLOW", "GREEN"]
CORNER_NAMES = ["bottom left", "bottom right", "top right", "top left"]
DEFAULT_TESTS = ["sweep", "bad", "noise", "duplicates"]
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
    def __init__(self, host, port, delay):
        self.address = (host, port)
        self.delay = delay
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.count = 0

    def send(self, payload, wait=None):
        self.sock.sendto(payload, self.address)
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

    def color_for(self, index):
        if self.args.color:
            return self.args.color
        return COLORS[index % len(COLORS)]

    def send_hit(self, x, y, color, noise=0.0, wait=None, note=""):
        """Sends a HIT for the true position (x, y), as the fake sensor would report it."""
        sensor_x, sensor_y = self.model.apply(x, y)
        if noise > 0.0:
            sensor_x += self.rng.gauss(0.0, noise)
            sensor_y += self.rng.gauss(0.0, noise)

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
    """(payload, the game should reject it, what it is). Mirrors HitMessageParser in the game."""
    garbage = bytes(rng.getrandbits(8) for _ in range(32))
    return [
        # Must be rejected.
        (b"", True, "empty packet"),
        (b"   ", True, "only spaces"),
        (b"HELLO", True, "not a HIT"),
        (b"HIT", True, "no fields"),
        (b"HIT 0.5", True, "missing y and color"),
        (b"HIT 0.5 0.5", True, "missing color"),
        (b"HIT 0.5 0.5 RED EXTRA", True, "one field too many"),
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
        (b"HIT 0.5 0.5 RED\nHIT 0.2 0.2 BLUE", True, "two messages in one packet"),
        (b"A" * 5000, True, "5000 characters of junk"),
        (garbage, True, "32 random bytes"),
        # Valid, so they must NOT be counted as rejected.
        (b"hit 0.5 0.5 red", False, "valid: lower case"),
        (b"  HIT   0.25    0.75   Blue  \r\n", False, "valid: extra spaces and a line end"),
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
    print("marked REJECT. Messages marked ok are counted as hits (some land off the picture).")

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
        description="Send fake sensor messages to the game over UDP.",
        epilog="Tests: " + ", ".join(ALL_TESTS) + ". With none named: " + ", ".join(DEFAULT_TESTS) + ".")
    parser.add_argument("tests", nargs="*", metavar="test", help="which tests to run")

    network = parser.add_argument_group("where to send")
    network.add_argument("--host", default="127.0.0.1", help="game PC address (default 127.0.0.1)")
    network.add_argument("--port", type=int, default=9000, help="game UDP port (default 9000)")
    network.add_argument("--delay", type=float, default=0.15,
                         help="seconds between messages (default 0.15)")

    path = parser.add_argument_group("what to send")
    path.add_argument("--color", type=str.upper, choices=COLORS,
                      help="use one color for every hit (default: cycle through all four)")
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

    print("Sending to {}:{}, {} s between messages".format(args.host, args.port, args.delay))
    print("Fake sensor: {}".format(model.describe()))
    print("Tests: {}".format(", ".join(tests)))

    try:
        for name in tests:
            RUNNERS[name](ctx)
    except KeyboardInterrupt:
        print("\nStopped.")
    finally:
        sender.close()

    print("\nDone. {} messages sent.".format(sender.count))
    if ctx.expected_rejected:
        print("The game's 'rejected' counter should read {} more than before the run.".format(
            ctx.expected_rejected))
    return 0


if __name__ == "__main__":
    sys.exit(main())
