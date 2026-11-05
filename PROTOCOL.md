# Hardware protocol

How the booth hardware talks to the game: which ball is loaded in the cannon, where a ball hit the
wall, and when the start button is pressed.

Status: draft. The game reads `LOAD`, `HIT` and `START` messages. Details can still change.

## Transport

- **TCP.** The game listens on port **9000** of the PC that runs it (the port can be changed in
  the game), and each piece of hardware connects to it. Several devices can be connected at once, or
  everything can go through one connection.
- One message per line of plain ASCII text, ended by a line end (`\n`, or `\r\n`). **The line end
  is required**: the game acts on a message when it sees it. Blank lines are ignored, and a line
  longer than 256 characters is thrown away.
- Fields are separated by spaces. Numbers use `.` as the decimal point.
- The game never replies.
- If the connection drops, connect again. The game closes every connection when it stops, so
  hardware that stays on between games must retry (every second or two is fine).

## Messages

### LOAD

Sent by the cannon's color sensor when a ball is loaded. The verb and the color can be written in
upper or lower case.

```
LOAD <color>
```

| Field | Meaning |
|-------|---------|
| `color` | Color of the ball: `RED`, `BLUE`, `YELLOW`, `GREEN`, `PURPLE` or `ORANGE` |

The game keeps this color until the next `LOAD`. Firing the ball does not clear it, so a hit that
is reported a moment late, or twice, still has a color. Before the first `LOAD` the cannon is empty,
and a `HIT` with no color is ignored.

### HIT

Send one `HIT` for every ball that hits the wall, as soon as the impact is detected. `HIT` can be
written in upper or lower case.

```
HIT <x> <y>
HIT <x> <y> <color>
```

| Field | Meaning |
|-------|---------|
| `x` | Horizontal position, from `0` (left edge) to `1` (right edge) |
| `y` | Vertical position, from `0` (bottom edge) to `1` (top edge) |
| `color` | Optional. Color of the ball: `RED`, `BLUE`, `YELLOW`, `GREEN`, `PURPLE` or `ORANGE` |

Left, right, bottom and top are as seen by a player facing the wall.

Without a color, the game reports the hit with the color of the ball loaded in the cannon (see
`LOAD`). This is how the wall sensor normally reports. With a color, that color is used instead and
the cannon is not touched.

Examples:

```
HIT 0.42 0.77
HIT 0.42 0.77 RED
```

The positions do not have to line up exactly with the projected picture. The game has a
calibration step that corrects for offset, scale and skew. What matters is that the same spot
on the wall always gives the same `x` and `y`.

### START

```
START
```

Send this when the physical start button is pressed. It starts a round, the same as the operator's
Space key. `START` can be written in upper or lower case, and nothing else may be on the line.

The game only acts on it while it is waiting between rounds. A `START` that arrives during a round,
or while Makey is still flying off after an aborted round, is ignored, so a button that bounces or
is pressed twice does no harm.

## Rules

- Send exactly one `HIT` per impact. If the sensor sees the same ball several times, pick one.
- Messages the game does not understand are ignored. The game shows how many it has ignored and
  the text of the latest message, which helps when checking what the hardware actually sends.

## Try it without hardware

Any tool that can open a TCP connection and send a line works. In Python:

```python
import socket

with socket.create_connection(("127.0.0.1", 9000)) as sock:
    sock.sendall(b"LOAD RED\n")
    sock.sendall(b"HIT 0.42 0.77\n")
    sock.sendall(b"START\n")
```

Replace `127.0.0.1` with the address of the game PC when connecting from another device. The game
may need to be allowed through the firewall on the first run.

`DevTools/tcp_full_tests.py` sends whole test runs: hits around the screen, bad messages, a
jittery sensor, and calibration.
