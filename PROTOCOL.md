# Hardware protocol

How the booth hardware tells the game that a ball hit the wall.

Status: draft. The game side is not built yet, so details can still change.

## Transport

- **UDP**, sent to the PC that runs the game, port **9000** (the port can be changed in the game).
- One message per packet, as plain ASCII text.
- Fields are separated by single spaces. A trailing newline is fine.
- Numbers use `.` as the decimal point.
- The game never replies.

## Messages

### HIT

Send one `HIT` for every ball that hits the wall, as soon as the impact is detected.

```
HIT <x> <y> <color>
```

| Field | Meaning |
|-------|---------|
| `x` | Horizontal position, from `0` (left edge) to `1` (right edge) |
| `y` | Vertical position, from `0` (bottom edge) to `1` (top edge) |
| `color` | Color of the ball: `RED`, `BLUE`, `YELLOW` or `GREEN` (upper or lower case) |

Left, right, bottom and top are as seen by a player facing the wall.

Example:

```
HIT 0.42 0.77 RED
```

The positions do not have to line up exactly with the projected picture. The game has a
calibration step that corrects for offset, scale and skew. What matters is that the same spot
on the wall always gives the same `x` and `y`.

### START (planned)

```
START
```

Sent when a physical start button is pressed. Not needed yet.

## Rules

- Send exactly one `HIT` per impact. If the sensor sees the same ball several times, pick one.
- Messages the game does not understand are ignored.

## Try it without hardware

Any tool that can send a UDP packet works. In Python:

```python
import socket

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.sendto(b"HIT 0.42 0.77 RED", ("127.0.0.1", 9000))
```

Replace `127.0.0.1` with the address of the game PC when sending from another device.
