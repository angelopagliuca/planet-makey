#!/usr/bin/env python3
"""
Talks to the real RobiLidar-2D-02 over UDP and prints what it sees, to check that the sensor
works before the booth program is written. Standard library only (Python 3.6 or newer), so
there is nothing to pip install.

First give the PC's Ethernet adapter a fixed address in the sensor's subnet, for example
192.168.0.11 with mask 255.255.255.0 and no gateway (the sensor itself is 192.168.0.10), and
check that `ping 192.168.0.10` works. Details are in Hardware/README.md.

    python robilidar_probe.py                       run until Ctrl+C
    python robilidar_probe.py --seconds 10          run for ten seconds, then print a summary
    python robilidar_probe.py --host 192.168.0.10 --port 2112
    python robilidar_probe.py --hexdump 3           also show the first three raw packets
    python robilidar_probe.py --catch               count how many thrown balls the sensor sees

Catch mode: it first learns what the scan looks like with nothing in it (keep the area clear for
five seconds), then reports every new object that appears and goes away as an impact. Press
Enter after each ball has landed and it says whether that throw was caught or missed. Ctrl+C
prints the totals, including how many sweeps each impact was seen in: an impact seen in only one
sweep is close to being missed. Run it for a minute with no throws first; any impact reported
then is a false alarm. Tune with --margin and --min-points.

Normal mode: once a second it prints how many sweeps arrived, how many points were valid, and the nearest
thing the sensor sees with its angle. Wave a hand in front of the sensor and those numbers
should follow it. Angles are as the vendor driver assumes: 270 degrees, from -135 to +135, with
0 straight ahead. Which way is ahead on the real unit is something to check with this tool.

Packet layout, from the vendor's ROS driver (all numbers big endian unless stated):

    0-3    header FE 5A A5 55
    4-5    payload size plus 9
    6      checksum: XOR of bytes 7 up to the end of the packet
    7      type
    8-9    sweep number
    10     number of packets this sweep is split into (3 to 90)
    11     index of this packet in the sweep
    12     packet type: 1 or 2 = scan data, 3 = scan info
    13-14  payload length (the vendor driver ignores it, so does this tool)
    15-    payload

The scan data packets of one sweep, joined in order, hold N distances as little endian 16 bit
millimeters (a value of 1 means "no reading"), followed by N intensities in the same format.
The info packets, joined, hold the angle step at bytes 72-75 (units of 1/100000 degree), the
sample count at 76-77 and the time of one sweep at 91-94 (units of 0.1 microsecond).
"""

import argparse
import queue
import socket
import struct
import sys
import threading
import time

DEFAULT_HOST = "192.168.0.10"
DEFAULT_PORT = 2112

HEADER = b"\xFE\x5A\xA5\x55"
START_COMMAND = HEADER + b"\x00\x02\x01\x01"
# The vendor driver sends the same eight bytes to start and to stop, so the sensor may treat
# them as a toggle. That is why this tool sends the command once, and only repeats it when
# nothing arrives.
STOP_COMMAND = START_COMMAND

PACKET_HEADER_SIZE = 15
LENGTH_OFFSET = 9
MIN_SUB_PACKETS = 3
MAX_SUB_PACKETS = 90
SCAN_PACKET_TYPES = (1, 2)
INFO_PACKET_TYPE = 3
MIN_INFO_SIZE = 95

START_ANGLE_DEG = -135.0
ANGLE_RESOLUTION_UNITS_PER_DEG = 100000.0
SCAN_TIME_UNITS_PER_SECOND = 10000000.0
NO_READING_MM = 1
MIN_RANGE_MM = 10
MAX_RANGE_MM = 60000

REPORT_EVERY_SECONDS = 1.0
SILENCE_WARNING_SECONDS = 3.0

# Catch mode. A point counts as new when it is this much closer than it is on the empty scene.
CATCH_MARGIN_MM = 100
CATCH_MIN_POINTS = 5
CATCH_LEARN_SECONDS = 5.0
# An impact ends when nothing new has been seen for this long.
CATCH_QUIET_SECONDS = 0.3
# A throw (Enter) is matched to an impact that started up to this long before it.
CATCH_MATCH_SECONDS = 3.0
# Something that stays in view this long is not a ball, and is ignored until it leaves.
CATCH_MAX_IMPACT_SECONDS = 2.0


class Sweep(object):
    """One complete turn of the sensor."""

    def __init__(self, ranges_mm, intensities, angle_step_deg, scan_time_s, sample_count):
        self.ranges_mm = ranges_mm
        self.intensities = intensities
        self.angle_step_deg = angle_step_deg
        self.scan_time_s = scan_time_s
        self.sample_count = sample_count

    def angle_of(self, index):
        return START_ANGLE_DEG + index * self.angle_step_deg

    def valid_count(self):
        return sum(1 for r in self.ranges_mm if MIN_RANGE_MM < r < MAX_RANGE_MM)

    def nearest(self):
        """Returns (distance in meters, angle in degrees) of the closest valid point, or None."""
        best_index = None
        best_range = MAX_RANGE_MM
        for index, r in enumerate(self.ranges_mm):
            if MIN_RANGE_MM < r < best_range:
                best_range = r
                best_index = index
        if best_index is None:
            return None
        return best_range / 1000.0, self.angle_of(best_index)


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
        raise ValueError("info block is {} bytes, expected at least {}".format(
            len(info), MIN_INFO_SIZE))

    count = len(scan) // 4
    if count == 0:
        raise ValueError("no scan data")
    ranges = struct.unpack("<{}H".format(count), scan[:2 * count])
    intensities = struct.unpack("<{}H".format(count), scan[2 * count:4 * count])

    angle_resolution = struct.unpack(">I", info[72:76])[0]
    if angle_resolution == 0:
        raise ValueError("info block says the angle step is zero")
    sample_count = struct.unpack(">H", info[76:78])[0]
    scan_time = struct.unpack(">I", info[91:95])[0]
    return Sweep(ranges, intensities,
                 angle_resolution / ANGLE_RESOLUTION_UNITS_PER_DEG,
                 scan_time / SCAN_TIME_UNITS_PER_SECOND,
                 sample_count)


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


class Stats(object):
    def __init__(self):
        self.packets = 0
        self.sweeps = 0
        self.bad = 0
        self.foreign = 0
        self.last_reason = ""
        self.window_sweeps = 0
        self.last_sweep = None
        self.last_packet_time = None

    def note_bad(self, reason):
        self.bad += 1
        if reason != self.last_reason:
            print("  ignored a bad packet: {}".format(reason))
            self.last_reason = reason


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


def find_clusters(sweep, background, margin_mm, min_points):
    """Returns (first index, last index) for each run of neighbouring new points.

    A point is new when it is closer than the closest background reading by more than margin_mm,
    or when it has a reading where the background never had one. Runs shorter than min_points
    are noise.
    """
    ranges = sweep.ranges_mm
    clusters = []
    start = None
    for index in range(len(ranges) + 1):
        is_new = False
        if index < len(ranges) and index < len(background.ranges_mm):
            if MIN_RANGE_MM < ranges[index] < MAX_RANGE_MM:
                usual = background.ranges_mm[index]
                is_new = usual is None or ranges[index] < usual - margin_mm
        if is_new:
            if start is None:
                start = index
        elif start is not None:
            if index - start >= min_points:
                clusters.append((start, index - 1))
            start = None
    return clusters


def cluster_position(sweep, cluster):
    """Returns (point count, distance in meters, angle in degrees) of a cluster."""
    first, last = cluster
    points = last - first + 1
    distance_mm = sum(sweep.ranges_mm[first:last + 1]) / float(points)
    return points, distance_mm / 1000.0, sweep.angle_of((first + last) / 2.0)


class Impact(object):
    """Something new that appeared in the scan and went away again: a thrown ball, hopefully."""

    def __init__(self, number, start, points, distance_m, angle_deg):
        self.number = number
        self.start = start
        self.last_seen = start
        self.sweeps = 1
        self.points = points
        self.distance_m = distance_m
        self.angle_deg = angle_deg
        self.matched = False


class CatchTracker(object):
    """Finds impacts in the sweeps, and checks each throw (Enter) against them."""

    def __init__(self, args):
        self._learn_seconds = args.learn
        self._margin_mm = args.margin
        self._min_points = args.min_points
        self._quiet_seconds = args.quiet
        self._match_seconds = args.match
        self._learning = []
        self._learn_started = None
        self._background = None
        self._open = None
        self._stuck = False
        self.impacts = []
        self.throws = 0
        self.caught = 0

    def feed(self, sweep, now):
        if self._background is None:
            self._learn(sweep, now)
            return

        clusters = find_clusters(sweep, self._background, self._margin_mm, self._min_points)
        if not clusters:
            self._stuck = False
            if self._open is not None and now - self._open.last_seen >= self._quiet_seconds:
                self._close_open()
            return
        if self._stuck:
            return

        if self._open is not None and now - self._open.last_seen >= self._quiet_seconds:
            self._close_open()
        if self._open is None:
            biggest = max(clusters, key=lambda c: c[1] - c[0])
            points, distance_m, angle_deg = cluster_position(sweep, biggest)
            self._open = Impact(len(self.impacts) + 1, now, points, distance_m, angle_deg)
            self.impacts.append(self._open)
            return

        self._open.sweeps += 1
        self._open.last_seen = now
        if now - self._open.start > CATCH_MAX_IMPACT_SECONDS:
            print("  something has stayed in view for over {:.0f} s, ignoring it until it "
                  "leaves".format(CATCH_MAX_IMPACT_SECONDS))
            self._close_open()
            self._stuck = True

    def throw(self, now):
        """Call when Enter is pressed after a ball has landed."""
        if self._background is None:
            print("  still learning the empty scene, that throw was not counted")
            return
        self.throws += 1
        waiting = [i for i in self.impacts
                   if not i.matched and now - self._match_seconds <= i.start <= now]
        if not waiting:
            print("throw {}: MISSED, nothing seen in the last {:.0f} s".format(
                self.throws, self._match_seconds))
            return
        impact = min(waiting, key=lambda i: i.start)
        impact.matched = True
        self.caught += 1
        print("throw {}: caught (impact {})".format(self.throws, impact.number))

    def finish(self):
        if self._open is not None:
            self._close_open()
        if self._background is None:
            print("catch mode never finished learning the empty scene")
            return
        extra = sum(1 for i in self.impacts if not i.matched)
        if self.throws:
            print("catch summary: {} throws, {} caught, {} missed ({:.0f}% caught); "
                  "{} impacts with no throw".format(
                      self.throws, self.caught, self.throws - self.caught,
                      100.0 * self.caught / self.throws, extra))
        else:
            print("catch summary: no throws counted, {} impacts seen".format(len(self.impacts)))
        one = sum(1 for i in self.impacts if i.sweeps == 1)
        two = sum(1 for i in self.impacts if i.sweeps == 2)
        more = sum(1 for i in self.impacts if i.sweeps >= 3)
        print("sweeps per impact: 1 sweep: {}, 2 sweeps: {}, 3 or more: {}".format(one, two, more))
        if one:
            print("an impact seen in only 1 sweep is close to being missed")

    def _learn(self, sweep, now):
        if self._learn_started is None:
            self._learn_started = now
            print("learning the empty scene for {:.0f} s, keep the area clear".format(
                self._learn_seconds))
        self._learning.append(sweep)
        if now - self._learn_started < self._learn_seconds:
            return
        self._background = Background(self._learning)
        print("learned from {} sweeps; {} of {} points never gave a reading".format(
            len(self._learning), self._background.no_reading_count(),
            len(self._background.ranges_mm)))
        print("ready: throw, then press Enter after each ball has landed (Ctrl+C to finish)")
        self._learning = []

    def _close_open(self):
        impact = self._open
        self._open = None
        print("impact {} at {}: seen in {} sweep{}, {} points at {:.2f} m, {:+.1f} deg".format(
            impact.number, time.strftime("%H:%M:%S", time.localtime(impact.start)),
            impact.sweeps, "" if impact.sweeps == 1 else "s", impact.points,
            impact.distance_m, impact.angle_deg))


def start_throw_listener():
    """Returns a queue that gets the time of every Enter press on the keyboard."""
    presses = queue.Queue()

    def listen():
        while sys.stdin.readline():
            presses.put(time.time())

    threading.Thread(target=listen, daemon=True).start()
    return presses


def describe_first_sweep(sweep):
    expected = int(round(270.0 / sweep.angle_step_deg)) + 1
    frequency = 1.0 / sweep.scan_time_s if sweep.scan_time_s > 0 else 0.0
    print("first sweep: {} points, step {:.3f} deg, sensor says {} samples, "
          "scan time {:.1f} ms ({:.1f} Hz)".format(
              len(sweep.ranges_mm), sweep.angle_step_deg, sweep.sample_count,
              sweep.scan_time_s * 1000.0, frequency))
    if len(sweep.ranges_mm) != expected:
        print("  note: 270 degrees at this step would be {} points, not {}".format(
            expected, len(sweep.ranges_mm)))


def report_line(stats, elapsed, window):
    sweep = stats.last_sweep
    if sweep is None:
        return "[{:4.0f}s] no complete sweep yet".format(elapsed)
    nearest = sweep.nearest()
    if nearest is None:
        nearest_text = "nothing in range"
    else:
        nearest_text = "nearest {:.3f} m at {:+.1f} deg".format(nearest[0], nearest[1])
    return "[{:4.0f}s] {:5.1f} sweeps/s | {} points, {} valid | {} | bad {}".format(
        elapsed, stats.window_sweeps / window, len(sweep.ranges_mm), sweep.valid_count(),
        nearest_text, stats.bad)


def run(args):
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind(("", 0))
    sock.settimeout(0.25)
    target = (args.host, args.port)

    stats = Stats()
    assembler = SweepAssembler()
    hexdumps_left = args.hexdump
    sent_commands = 0
    tracker = CatchTracker(args) if args.catch else None
    throws = start_throw_listener() if args.catch else None

    started = time.time()
    last_command_time = started
    last_report = started
    silence_reported = False

    if not args.listen:
        sock.sendto(START_COMMAND, target)
        sent_commands = 1
        print("sent the start command to {}:{}, waiting for scan data".format(*target))
    else:
        print("listening only, no command sent")

    try:
        while True:
            now = time.time()
            if args.seconds and now - started >= args.seconds:
                break

            if stats.packets == 0 and now - last_command_time >= args.wait:
                if args.listen or sent_commands >= 2:
                    print("no data from the sensor after {:.0f} s.".format(now - started))
                    print("Check: the PC adapter is 192.168.0.x (not the sensor's .10), "
                          "`ping {}` works, the sensor has been on for more than 10 s, and "
                          "Windows Firewall allows this Python to receive UDP.".format(args.host))
                    return 1
                print("no data yet, sending the command once more in case it toggles")
                sock.sendto(START_COMMAND, target)
                sent_commands += 1
                last_command_time = now

            if now - last_report >= REPORT_EVERY_SECONDS:
                if stats.packets and not args.catch:
                    print(report_line(stats, now - started, now - last_report))
                stats.window_sweeps = 0
                last_report = now

            if (stats.last_packet_time is not None and not silence_reported
                    and now - stats.last_packet_time >= SILENCE_WARNING_SECONDS):
                print("  no packets for {:.0f} s".format(now - stats.last_packet_time))
                silence_reported = True

            if throws is not None:
                while not throws.empty():
                    tracker.throw(throws.get())

            try:
                data, address = sock.recvfrom(4096)
            except socket.timeout:
                continue

            if address[0] != args.host and not args.listen:
                stats.foreign += 1
                continue

            stats.packets += 1
            stats.last_packet_time = time.time()
            silence_reported = False

            if hexdumps_left > 0:
                hexdumps_left -= 1
                print("  packet from {}:{} ({} bytes): {}".format(
                    address[0], address[1], len(data), data[:32].hex()))

            try:
                sweep = assembler.add(*parse_packet(data))
            except ValueError as error:
                stats.note_bad(str(error))
                continue
            if sweep is None:
                continue

            if stats.sweeps == 0:
                describe_first_sweep(sweep)
            stats.sweeps += 1
            stats.window_sweeps += 1
            stats.last_sweep = sweep
            if tracker is not None:
                tracker.feed(sweep, stats.last_packet_time)
    except KeyboardInterrupt:
        print()
    finally:
        if stats.packets and not args.listen:
            sock.sendto(STOP_COMMAND, target)
        sock.close()

    elapsed = time.time() - started
    if tracker is not None:
        tracker.finish()
    print("summary: {} sweeps in {:.1f} s, {} packets, {} bad, {} sweeps dropped, "
          "{} from other senders".format(
              stats.sweeps, elapsed, stats.packets, stats.bad, assembler.dropped,
              stats.foreign))
    if stats.sweeps == 0:
        print("packets arrived but no complete sweep could be built; try --hexdump 3")
        return 1
    print("the sensor works")
    return 0


def main():
    parser = argparse.ArgumentParser(
        description="Check the RobiLidar-2D-02 by reading its scan packets over UDP.")
    parser.add_argument("--host", default=DEFAULT_HOST, help="sensor address (default %(default)s)")
    parser.add_argument("--port", type=int, default=DEFAULT_PORT,
                        help="sensor UDP port (default %(default)s)")
    parser.add_argument("--seconds", type=float, default=0,
                        help="stop after this long (default: run until Ctrl+C)")
    parser.add_argument("--wait", type=float, default=3.0,
                        help="seconds to wait for the first packet before resending the "
                             "command, and again before giving up (default %(default)s)")
    parser.add_argument("--hexdump", type=int, default=0, metavar="N",
                        help="print the first N raw packets, for debugging")
    parser.add_argument("--listen", action="store_true",
                        help="send no command, only listen on a random local port")
    parser.add_argument("--catch", action="store_true",
                        help="count how many thrown balls the sensor sees (press Enter after "
                             "each ball has landed)")
    parser.add_argument("--learn", type=float, default=CATCH_LEARN_SECONDS,
                        help="catch mode: seconds spent learning the empty scene "
                             "(default %(default)s)")
    parser.add_argument("--margin", type=int, default=CATCH_MARGIN_MM, metavar="MM",
                        help="catch mode: how much closer than the empty scene a point must be "
                             "to count as new (default %(default)s)")
    parser.add_argument("--min-points", type=int, default=CATCH_MIN_POINTS, metavar="N",
                        help="catch mode: neighbouring new points needed to count as an object "
                             "(default %(default)s)")
    parser.add_argument("--quiet", type=float, default=CATCH_QUIET_SECONDS,
                        help="catch mode: seconds without anything new that end an impact "
                             "(default %(default)s)")
    parser.add_argument("--match", type=float, default=CATCH_MATCH_SECONDS,
                        help="catch mode: how long before pressing Enter an impact may have "
                             "started and still match that throw (default %(default)s)")
    args = parser.parse_args()
    return run(args)


if __name__ == "__main__":
    sys.exit(main())
