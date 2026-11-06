#!/usr/bin/env python3
"""
The wall sensor program. It reads the RobiLidar-2D-02, finds every ball that crosses its scan
plane, works out where on the picture it will hit, and sends the game one message for each:

    HIT <x> <y>

The game adds the color of the ball loaded in the cannon (see PROTOCOL.md). Standard library only
(Python 3.6 or newer), so there is nothing to pip install.

    python lidar_hits.py                run for the booth, sending hits to the game
    python lidar_hits.py --dry-run      the same, but print the hits and send nothing
    python lidar_hits.py --track        show where whatever is in the scan sits on the picture
    python lidar_hits.py --config my.ini --seconds 60

It reads config.ini next to it: the size of the picture, where the sensor sits, which way it
points, and the detection settings. The comments in that file explain each one.

How it works, in the order it happens:

1. The sensor sends nothing until it is asked, so the program sends it the start command.
2. For the first few seconds it learns the empty scan: the closest reading at every angle. Keep
   the area in front of the wall clear.
3. After that, a run of neighbouring points that are new (closer than in the empty scan, or a
   reading where there was none) and that lie on the picture, or just off its edge, is a ball.
4. The ball's position is the middle of that run in the first sweep that sees it. The sensor's
   scan plane is parallel to the wall, so the ball is right above the spot it will hit. The angle
   and distance become a position in meters, and dividing by the picture's size gives x and y from
   0 to 1, with the origin at the bottom left.
5. One HIT is sent for each ball: sightings that follow within "same_ball_seconds" are the same
   ball, so a bounce back through the scan is not a second hit.

--track is for setting up. With nothing sent, hold a ball, or a hand, in the scan plane at the
corners and the middle of the picture, and the program prints the x and y it works out. They
should read about 0 and 1 at the corners. If left and right are swapped, change "angles_increase"
in the config. If the numbers are all shifted, check the sensor's x and y.

Angles are on the sensor's own scale, the one printed around it: -45 to 225, with 90 in the
middle of the scan.

The packets from the sensor are described at the top of DevTools/robilidar_probe.py.
"""

import argparse
import configparser
import math
import os
import select
import socket
import struct
import sys
import time

# ---------------------------------------------------------------------------------------------
# The sensor's packets (see DevTools/robilidar_probe.py for the layout)
# ---------------------------------------------------------------------------------------------

HEADER = b"\xFE\x5A\xA5\x55"
START_COMMAND = HEADER + b"\x00\x02\x01\x01"
# The sensor's own driver sends the same eight bytes to start and to stop, so the sensor may treat
# them as a toggle. That is why the command is sent once, and again only when nothing arrives.
STOP_COMMAND = START_COMMAND

PACKET_HEADER_SIZE = 15
LENGTH_OFFSET = 9
MIN_SUB_PACKETS = 3
MAX_SUB_PACKETS = 90
SCAN_PACKET_TYPES = (1, 2)
INFO_PACKET_TYPE = 3
MIN_INFO_SIZE = 95

# The first point's angle on the sensor's own scale. The scan covers 270 degrees from here.
SENSOR_START_ANGLE_DEG = -45.0
ANGLE_RESOLUTION_UNITS_PER_DEG = 100000.0
MIN_RANGE_MM = 10
MAX_RANGE_MM = 60000

# ---------------------------------------------------------------------------------------------
# Running
# ---------------------------------------------------------------------------------------------

UNITS_TO_METERS = {
    "feet": 0.3048,
    "inches": 0.0254,
    "meters": 1.0,
    "centimeters": 0.01,
}

DEFAULT_CONFIG = os.path.join(os.path.dirname(os.path.abspath(__file__)), "config.ini")

# After this long without a packet the start command is sent again.
SILENCE_SECONDS = 3.0
GAME_RECONNECT_SECONDS = 2.0
GAME_CONNECT_TIMEOUT_SECONDS = 0.5
HEARTBEAT_SECONDS = 30.0
TRACK_PRINT_SECONDS = 0.2


class Settings(object):
    """Everything from config.ini, with the lengths turned into meters."""

    def __init__(self, path):
        parser = configparser.ConfigParser(inline_comment_prefixes=("#", ";"), interpolation=None)
        if not parser.read(path):
            raise SystemExit("Could not read the config file {}".format(path))

        try:
            self._read(parser)
        except (configparser.Error, ValueError) as error:
            raise SystemExit("Problem in {}: {}".format(path, error))

        self._check()

    def _read(self, parser):
        units = parser.get("screen", "units", fallback="feet").strip().lower()
        if units not in UNITS_TO_METERS:
            raise ValueError("[screen] units must be one of: {}".format(", ".join(sorted(UNITS_TO_METERS))))
        self.units_name = units
        self.unit_m = UNITS_TO_METERS[units]

        self.picture_width_m = parser.getfloat("screen", "width") * self.unit_m
        self.picture_height_m = parser.getfloat("screen", "height") * self.unit_m

        self.sensor_x_m = parser.getfloat("sensor", "x") * self.unit_m
        self.sensor_y_m = parser.getfloat("sensor", "y") * self.unit_m
        self.down_angle_deg = parser.getfloat("sensor", "down_angle", fallback=90.0)

        direction = parser.get("sensor", "angles_increase", fallback="counterclockwise").strip().lower()
        if direction not in ("clockwise", "counterclockwise"):
            raise ValueError("[sensor] angles_increase must be clockwise or counterclockwise")
        self.counterclockwise = direction == "counterclockwise"

        self.margin_m = parser.getfloat("detection", "margin", fallback=1.0) * self.unit_m
        self.learn_seconds = parser.getfloat("detection", "learn_seconds", fallback=5.0)
        self.distance_margin_mm = parser.getfloat("detection", "distance_margin_mm", fallback=100.0)
        self.min_points = parser.getint("detection", "min_points", fallback=5)
        self.same_ball_seconds = parser.getfloat("detection", "same_ball_seconds", fallback=0.6)
        self.stuck_seconds = parser.getfloat("detection", "stuck_seconds", fallback=2.0)

        self.game_host = parser.get("game", "host", fallback="127.0.0.1").strip()
        self.game_port = parser.getint("game", "port", fallback=9000)
        self.lidar_host = parser.get("lidar", "host", fallback="192.168.0.10").strip()
        self.lidar_port = parser.getint("lidar", "port", fallback=2112)

    def _check(self):
        problems = []
        if self.picture_width_m <= 0 or self.picture_height_m <= 0:
            problems.append("[screen] width and height must be more than 0")
        if self.margin_m < 0:
            problems.append("[detection] margin cannot be negative")
        if self.learn_seconds < 1:
            problems.append("[detection] learn_seconds must be at least 1")
        if self.min_points < 1:
            problems.append("[detection] min_points must be at least 1")
        if self.same_ball_seconds < 0 or self.stuck_seconds <= 0:
            problems.append("[detection] same_ball_seconds and stuck_seconds must be positive")
        if not 1 <= self.game_port <= 65535 or not 1 <= self.lidar_port <= 65535:
            problems.append("the ports must be from 1 to 65535")
        if problems:
            raise SystemExit("Problem in the config file:\n  " + "\n  ".join(problems))

    def describe(self):
        def show(meters):
            return "{:.2f} {}".format(meters / self.unit_m, self.units_name)

        return [
            "picture: {} wide, {} tall".format(show(self.picture_width_m), show(self.picture_height_m)),
            "sensor: {} from the left, {} from the bottom; its {:g} mark points straight down; "
            "angles increase {} as the player sees them".format(
                show(self.sensor_x_m), show(self.sensor_y_m), self.down_angle_deg,
                "counterclockwise" if self.counterclockwise else "clockwise"),
            "a ball counts up to {} outside the picture; sightings within {:g} s are one ball".format(
                show(self.margin_m), self.same_ball_seconds),
        ]


class Sweep(object):
    """One complete turn of the sensor."""

    def __init__(self, ranges_mm, angle_step_deg):
        self.ranges_mm = ranges_mm
        self.angle_step_deg = angle_step_deg


def parse_packet(buf):
    """Checks one UDP packet and returns (sweep number, count, index, type, payload).

    Raises ValueError, with the reason, if the packet is not a good one.
    """
    if len(buf) < PACKET_HEADER_SIZE:
        raise ValueError("packet too short")
    if buf[:4] != HEADER:
        raise ValueError("wrong header")

    payload_size = ((buf[4] << 8) | buf[5]) - LENGTH_OFFSET
    if payload_size < 0 or len(buf) != PACKET_HEADER_SIZE + payload_size:
        raise ValueError("length does not match the packet size")

    checksum = 0
    for value in buf[7:]:
        checksum ^= value
    if checksum != buf[6]:
        raise ValueError("bad checksum")

    sweep_number = (buf[8] << 8) | buf[9]
    count = buf[10]
    index = buf[11]
    packet_type = buf[12]
    if not MIN_SUB_PACKETS <= count <= MAX_SUB_PACKETS or index >= count:
        raise ValueError("bad packet numbering ({} of {})".format(index, count))
    return sweep_number, count, index, packet_type, bytes(buf[PACKET_HEADER_SIZE:])


def decode_sweep(parts):
    """Builds a Sweep from the packets of one sweep, in packet order.

    Raises ValueError, with the reason, if the sweep cannot be decoded.
    """
    scan = b"".join(payload for kind, payload in parts if kind in SCAN_PACKET_TYPES)
    info = b"".join(payload for kind, payload in parts if kind == INFO_PACKET_TYPE)
    if len(info) < MIN_INFO_SIZE:
        raise ValueError("info block is {} bytes, expected at least {}".format(len(info), MIN_INFO_SIZE))

    count = len(scan) // 4
    if count == 0:
        raise ValueError("no scan data")
    # The first half of the scan data is the distances, in millimeters. The second half, the
    # intensities, is not used.
    ranges = struct.unpack("<{}H".format(count), scan[:2 * count])

    angle_resolution = struct.unpack(">I", info[72:76])[0]
    if angle_resolution == 0:
        raise ValueError("info block says the angle step is zero")
    return Sweep(ranges, angle_resolution / ANGLE_RESOLUTION_UNITS_PER_DEG)


class SweepAssembler(object):
    """Collects the packets of a sweep and hands back the sweep once the last one arrives."""

    def __init__(self):
        self.dropped = 0
        self._sweep_number = None
        self._count = 0
        self._parts = {}

    def add(self, sweep_number, count, index, packet_type, payload):
        """Returns a Sweep when this packet completes one, otherwise None."""
        if sweep_number != self._sweep_number or count != self._count:
            if self._parts:
                self.dropped += 1
            self._sweep_number = sweep_number
            self._count = count
            self._parts = {}

        self._parts[index] = (packet_type, payload)
        if len(self._parts) < count:
            return None

        parts = [self._parts[i] for i in range(count)]
        self._sweep_number = None
        self._parts = {}
        return decode_sweep(parts)


# ---------------------------------------------------------------------------------------------
# From a point of the scan to a place on the wall
# ---------------------------------------------------------------------------------------------

class Geometry(object):
    """Turns a point of a sweep (its number and distance) into a position on the wall.

    The wall has x to the right and y up, as the player sees it, with the origin at the bottom
    left corner of the picture. The scan plane is parallel to the wall, so the sensor's angle and
    distance are a direction and a distance on the wall.
    """

    def __init__(self, settings):
        self.settings = settings
        self._tables = {}

    def directions(self, count, step_deg):
        """Returns (cosines, sines) of the wall direction of every point in a sweep."""
        key = (count, step_deg)
        table = self._tables.get(key)
        if table is None:
            cosines = []
            sines = []
            for index in range(count):
                turn = self.sensor_angle(index, step_deg) - self.settings.down_angle_deg
                if not self.settings.counterclockwise:
                    turn = -turn
                # Straight down is -90 degrees on the wall. Turning counterclockwise from there
                # moves towards the player's right.
                direction = math.radians(-90.0 + turn)
                cosines.append(math.cos(direction))
                sines.append(math.sin(direction))
            table = (cosines, sines)
            self._tables[key] = table
        return table

    @staticmethod
    def sensor_angle(index, step_deg):
        """The angle of a point on the sensor's own printed scale."""
        return SENSOR_START_ANGLE_DEG + index * step_deg

    def near_picture(self, x_m, y_m):
        """True if the point is on the picture or within the margin of it."""
        margin = self.settings.margin_m
        return (-margin <= x_m <= self.settings.picture_width_m + margin
                and -margin <= y_m <= self.settings.picture_height_m + margin)

    def normalize(self, x_m, y_m):
        """Meters on the wall to the 0 to 1 position the game uses."""
        return x_m / self.settings.picture_width_m, y_m / self.settings.picture_height_m


# ---------------------------------------------------------------------------------------------
# Finding balls
# ---------------------------------------------------------------------------------------------

class Background(object):
    """What the sensor sees with nothing thrown: the closest distance at each angle.

    The closest reading is used, not the typical one, because the edge of a nearby object can
    flicker between near and far from sweep to sweep. A point is None where the sensor never got
    a reading, which is common when the scan plane runs along the wall with nothing in front.
    """

    def __init__(self, sweeps):
        count = len(sweeps[0].ranges_mm)
        sweeps = [s for s in sweeps if len(s.ranges_mm) == count]
        self.ranges_mm = []
        for index in range(count):
            values = [s.ranges_mm[index] for s in sweeps
                      if MIN_RANGE_MM < s.ranges_mm[index] < MAX_RANGE_MM]
            self.ranges_mm.append(min(values) if values else None)

    def no_reading_count(self):
        return sum(1 for r in self.ranges_mm if r is None)

    def adopt(self, sweep, clusters):
        """Makes what is in the clusters part of the scene, so it stops counting as new."""
        for cluster in clusters:
            for index, _x, _y in cluster:
                self.ranges_mm[index] = sweep.ranges_mm[index]


def find_clusters(sweep, background, geometry, only_picture):
    """Returns the runs of neighbouring new points, each a list of (index, x_m, y_m).

    A point is new when it is closer than the closest background reading by more than the
    distance margin, or when it has a reading where the background never had one. With
    only_picture, points off the picture (beyond the margin) are left out. Runs shorter than
    min_points are noise.
    """
    settings = geometry.settings
    ranges = sweep.ranges_mm
    count = min(len(ranges), len(background.ranges_mm))
    cosines, sines = geometry.directions(len(ranges), sweep.angle_step_deg)

    clusters = []
    run = []
    for index in range(count + 1):
        point = None
        if index < count and MIN_RANGE_MM < ranges[index] < MAX_RANGE_MM:
            usual = background.ranges_mm[index]
            if usual is None or ranges[index] < usual - settings.distance_margin_mm:
                meters = ranges[index] / 1000.0
                x_m = settings.sensor_x_m + meters * cosines[index]
                y_m = settings.sensor_y_m + meters * sines[index]
                if not only_picture or geometry.near_picture(x_m, y_m):
                    point = (index, x_m, y_m)

        if point is not None:
            run.append(point)
            continue

        if len(run) >= settings.min_points:
            clusters.append(run)
        run = []
    return clusters


class Position(object):
    """Where a cluster is: on the wall, and as the sensor sees it."""

    def __init__(self, cluster, sweep, geometry):
        settings = geometry.settings
        self.points = len(cluster)
        self.x_m = sum(p[1] for p in cluster) / self.points
        self.y_m = sum(p[2] for p in cluster) / self.points
        self.x, self.y = geometry.normalize(self.x_m, self.y_m)
        middle = sum(p[0] for p in cluster) / float(self.points)
        self.sensor_angle_deg = geometry.sensor_angle(middle, sweep.angle_step_deg)
        self.distance_m = math.hypot(self.x_m - settings.sensor_x_m, self.y_m - settings.sensor_y_m)

    def describe(self):
        return "x {:.3f}  y {:.3f}   (sensor angle {:.1f}, {:.2f} m from the sensor, {} points)".format(
            self.x, self.y, self.sensor_angle_deg, self.distance_m, self.points)


class Detector(object):
    """Turns sweeps into hits: one for each ball."""

    def __init__(self, settings, geometry, background):
        self._settings = settings
        self._geometry = geometry
        self._background = background
        self._last_seen = None
        self._first_seen = None
        self.stuck_notices = 0

    def feed(self, sweep, now):
        """Returns the Position of a new ball if this sweep is the first to see one, else None."""
        clusters = find_clusters(sweep, self._background, self._geometry, only_picture=True)
        if not clusters:
            return None

        is_new_ball = self._last_seen is None or now - self._last_seen >= self._settings.same_ball_seconds
        self._last_seen = now

        if is_new_ball:
            self._first_seen = now
            biggest = max(clusters, key=len)
            return Position(biggest, sweep, self._geometry)

        # Seen again within the time of one ball. If it just stays there it is not a ball.
        if now - self._first_seen >= self._settings.stuck_seconds:
            self._background.adopt(sweep, clusters)
            self._last_seen = None
            self._first_seen = None
            self.stuck_notices += 1
            print("{}  something stayed in view for over {:g} s, treating it as part of the scene".format(
                clock(), self._settings.stuck_seconds))
        return None


# ---------------------------------------------------------------------------------------------
# The game
# ---------------------------------------------------------------------------------------------

class GameLink(object):
    """The TCP connection to the game. Reconnects by itself, so the game can be restarted."""

    def __init__(self, host, port):
        self._address = (host, port)
        self._sock = None
        self._last_try = None
        self._complained = False

    @property
    def connected(self):
        return self._sock is not None

    def maintain(self, now):
        """Notices when the game closed the connection, and tries again every couple of seconds."""
        if self._sock is not None and not self._alive():
            print("{}  the game closed the connection".format(clock()))
            self._drop()

        if self._sock is None and (self._last_try is None or now - self._last_try >= GAME_RECONNECT_SECONDS):
            self._connect(now)

    def send(self, text, now):
        """Sends one message as a line. Returns True if it went out."""
        # Twice, so that a connection the game closed a moment ago is replaced and the hit still
        # goes out.
        for _ in range(2):
            if self._sock is None and not self._connect(now):
                return False
            if not self._alive():
                self._drop()
                continue
            try:
                self._sock.sendall(text.encode("ascii") + b"\n")
                return True
            except OSError:
                self._drop()
        return False

    def close(self):
        self._drop()

    def _connect(self, now):
        self._last_try = now
        try:
            sock = socket.create_connection(self._address, timeout=GAME_CONNECT_TIMEOUT_SECONDS)
        except OSError as error:
            if not self._complained:
                print("{}  cannot reach the game at {}:{} ({}); trying again every {:g} s".format(
                    clock(), self._address[0], self._address[1], error, GAME_RECONNECT_SECONDS))
                self._complained = True
            return False

        # Messages are tiny. Send each at once instead of waiting to fill a packet.
        sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        sock.settimeout(1.0)
        self._sock = sock
        self._complained = False
        print("{}  connected to the game at {}:{}".format(clock(), self._address[0], self._address[1]))
        return True

    def _alive(self):
        # The game never sends anything, so a readable socket that gives no bytes was closed.
        try:
            readable, _, _ = select.select([self._sock], [], [], 0)
            if readable and not self._sock.recv(4096):
                return False
        except OSError:
            return False
        return True

    def _drop(self):
        if self._sock is not None:
            try:
                self._sock.close()
            except OSError:
                pass
        self._sock = None


# ---------------------------------------------------------------------------------------------
# Main loop
# ---------------------------------------------------------------------------------------------

def clock():
    return time.strftime("%H:%M:%S")


def run(settings, args):
    geometry = Geometry(settings)
    sending = not (args.dry_run or args.track)
    link = GameLink(settings.game_host, settings.game_port) if sending else None

    lidar = (settings.lidar_host, settings.lidar_port)
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind(("", 0))
    sock.settimeout(0.25)

    print("RobiLidar wall sensor program")
    for line in settings.describe():
        print("  " + line)
    if args.track:
        print("  TRACK mode: nothing is sent to the game")
    elif args.dry_run:
        print("  DRY RUN: hits are printed and nothing is sent to the game")
    else:
        print("  hits go to the game at {}:{}".format(settings.game_host, settings.game_port))

    assembler = SweepAssembler()
    learning = []
    learn_started = None
    detector = None
    background = None

    packets = 0
    bad_packets = 0
    sweeps = 0
    hits = 0
    hits_sent = 0
    window_sweeps = 0
    last_packet = None
    silent_notice = False
    last_bad_reason = ""
    last_track_print = 0.0

    started = time.time()
    last_command = started
    last_heartbeat = started
    sock.sendto(START_COMMAND, lidar)
    print("{}  asked the sensor at {}:{} to start".format(clock(), lidar[0], lidar[1]))

    try:
        while True:
            now = time.time()
            if args.seconds and now - started >= args.seconds:
                break

            if link is not None:
                link.maintain(now)

            # Keep the sensor sending: if it goes quiet, ask it to start again.
            quiet_since = last_packet if last_packet is not None else started
            if now - quiet_since >= SILENCE_SECONDS and now - last_command >= SILENCE_SECONDS:
                if not silent_notice:
                    print("{}  no data from the sensor, asking it to start again".format(clock()))
                    silent_notice = True
                sock.sendto(START_COMMAND, lidar)
                last_command = now

            if sending and detector is not None and now - last_heartbeat >= HEARTBEAT_SECONDS:
                print("{}  running: {:.1f} sweeps a second, {} hits sent, game {}".format(
                    clock(), window_sweeps / (now - last_heartbeat), hits_sent,
                    "connected" if link.connected else "NOT connected"))
                window_sweeps = 0
                last_heartbeat = now

            try:
                data, address = sock.recvfrom(4096)
            except socket.timeout:
                continue
            if address[0] != settings.lidar_host:
                continue

            last_packet = time.time()
            packets += 1
            if silent_notice:
                print("{}  the sensor is sending again".format(clock()))
                silent_notice = False

            try:
                sweep = assembler.add(*parse_packet(data))
            except ValueError as error:
                bad_packets += 1
                if str(error) != last_bad_reason:
                    print("{}  ignored a bad packet: {}".format(clock(), error))
                    last_bad_reason = str(error)
                continue
            if sweep is None:
                continue

            sweeps += 1
            window_sweeps += 1

            if background is None:
                if learn_started is None:
                    learn_started = last_packet
                    print("{}  learning the empty scan for {:g} s, keep the area in front of the wall "
                          "clear".format(clock(), settings.learn_seconds))
                learning.append(sweep)
                if last_packet - learn_started >= settings.learn_seconds:
                    background = Background(learning)
                    learning = []
                    detector = Detector(settings, geometry, background)
                    last_heartbeat = last_packet
                    window_sweeps = 0
                    print("{}  ready ({} of {} points never gave a reading)".format(
                        clock(), background.no_reading_count(), len(background.ranges_mm)))
                continue

            if args.track:
                if last_packet - last_track_print >= TRACK_PRINT_SECONDS:
                    clusters = find_clusters(sweep, background, geometry, only_picture=False)
                    if clusters:
                        position = Position(max(clusters, key=len), sweep, geometry)
                        inside = 0.0 <= position.x <= 1.0 and 0.0 <= position.y <= 1.0
                        print("{}  {}  {}".format(
                            clock(), position.describe(), "on the picture" if inside else "OFF the picture"))
                        last_track_print = last_packet
                continue

            position = detector.feed(sweep, last_packet)
            if position is None:
                continue

            hits += 1
            message = "HIT {:.4f} {:.4f}".format(position.x, position.y)
            if link is None:
                result = "dry run, not sent"
            elif link.send(message, last_packet):
                hits_sent += 1
                result = "sent"
            else:
                result = "NOT SENT, the game is not connected"
            print("{}  hit {}: {}   {}   {}".format(clock(), hits, message, position.describe(), result))
    except KeyboardInterrupt:
        print()
    finally:
        if packets:
            sock.sendto(STOP_COMMAND, lidar)
        sock.close()
        if link is not None:
            link.close()

    print("done: {} sweeps, {} bad packets, {} hits{}".format(
        sweeps, bad_packets, hits, "" if link is None else ", {} sent to the game".format(hits_sent)))
    if sweeps == 0:
        print("no data from the sensor. Check the cable and power, that the PC adapter is 192.168.0.x, "
              "and that `ping {}` works (see Hardware/README.md).".format(settings.lidar_host))
        return 1
    return 0


def main():
    parser = argparse.ArgumentParser(
        description="Read the wall sensor and send a HIT to the game for every ball.")
    parser.add_argument("--config", default=DEFAULT_CONFIG,
                        help="the settings file (default: config.ini next to this program)")
    parser.add_argument("--dry-run", action="store_true",
                        help="print the hits and send nothing to the game")
    parser.add_argument("--track", action="store_true",
                        help="print where whatever is in the scan sits on the picture, for setting up")
    parser.add_argument("--seconds", type=float, default=0,
                        help="stop after this long (default: run until Ctrl+C)")
    args = parser.parse_args()
    return run(Settings(args.config), args)


if __name__ == "__main__":
    sys.exit(main())
