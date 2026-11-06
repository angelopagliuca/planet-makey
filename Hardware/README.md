# Hardware

The booth side of the game: the wall sensor, and the program that turns what it sees into the
messages the game reads (see [`../PROTOCOL.md`](../PROTOCOL.md)). The game listens on TCP, and
the hardware connects to it.

Two devices talk to the game, and they do not talk to each other:

- **The cannon's color sensor** sends `LOAD <color>` when a ball is loaded. The game remembers
  the color until the next one.
- **The wall sensor program** reads the sweeps from the lidar, cleans them up so each ball impact
  is one position, and sends `HIT <x> <y>`. The game gives the hit the loaded ball's color.

The game does the calibration, so positions only have to be consistent. They still have to be
in the `0` to `1` range the protocol describes, so a rough conversion from sensor distance to
that range is needed.

## The sensor

RobiLidar-2D-02 from [Robify](https://robify.com/product/robilidar-2d-02/): a 2D lidar over
Ethernet. It measures a distance every 0.15 degrees across 270 degrees, which is one sweep of
1801 points.

| | |
|---|---|
| Power | 9 to 28 V DC, under 2 W. Takes about 10 s to start after power-on |
| Range | 0.01 to 60 m by the driver's limits (25 m at 70% reflectivity per the product page) |
| Speed | 50 ms per sweep, about 20 sweeps per second (the product page says 33 ms typical) |
| Address | `192.168.0.10`, UDP port `2112` |

## Connecting it

1. Power the sensor and plug its Ethernet cable into the PC.
2. Give the PC's Ethernet adapter a fixed address on the same subnet, for example `192.168.0.11`
   with mask `255.255.255.0` and no gateway. In an administrator PowerShell:

   ```powershell
   Set-NetIPInterface -InterfaceAlias "Ethernet" -Dhcp Disabled
   New-NetIPAddress -InterfaceAlias "Ethernet" -IPAddress 192.168.0.11 -PrefixLength 24
   ```

3. Check that it answers: `ping 192.168.0.10`.
4. Run `python ../DevTools/robilidar_probe.py`. It should report about 20 sweeps a second and
   the nearest thing the sensor sees. Wave a hand in front of the sensor and the numbers follow.

To give the adapter back its automatic address:

```powershell
Remove-NetIPAddress -InterfaceAlias "Ethernet" -IPAddress 192.168.0.11 -Confirm:$false; Set-NetIPInterface -InterfaceAlias "Ethernet" -Dhcp Enabled
```

## The wall sensor program

`Lidar/lidar_hits.py` reads the sensor and sends `HIT <x> <y>` to the game over TCP for every ball
that crosses its scan plane. Its settings are in `Lidar/config.ini`, and the comments there explain
each one. The ones to set for a booth:

- the **size of the projected picture** (for example 8 by 4 feet)
- **where the sensor sits**, measured from the picture's bottom left corner (for example the middle
  of the top edge: 4 feet across and 4 feet up)
- **which sensor angle points straight down the wall**: the number printed around the sensor
  that points away from the top edge, normally 90

The scan plane is parallel to the wall, so a ball in the plane is right above the spot it will hit.
The program turns the sensor's angle and distance into a position in feet, and dividing by the
picture's size gives `x` and `y` from 0 to 1. The game's calibration (F9) corrects what the
measurements get wrong, so a few centimeters (an inch or two) is plenty.

Set it up once:

1. Fill in `Lidar/config.ini`, and mount the sensor with its 90 mark pointing down the wall.
2. Run `python Lidar/lidar_hits.py --track`, keep the wall clear for the first 5 seconds, then hold
   a ball in the scan plane at the picture's four corners and its middle. It prints the `x` and
   `y` it works out: about 0 and 1 at the corners, 0.5 and 0.5 in the middle. If left and right
   are swapped, change `angles_increase` in the config. If everything is shifted, check the
   sensor's `x` and `y`.
3. Run `python Lidar/lidar_hits.py --dry-run` and throw a few balls. Each one prints one hit, and
   nothing is sent to the game.

Then, to run it for the booth, start the game and run `python Lidar/lidar_hits.py`. It connects to
the game by itself, and reconnects if the game is restarted. Calibrate with F9 in the game once the
sensor is mounted. It learns the empty scan when it starts, so keep the area in front of the wall
clear for the first 5 seconds, and restart it if something in the booth is moved.

Things it does on its own: a ball that bounces back through the scan counts once, something
that stays in the scan for 2 seconds is treated as part of the scene, and if the sensor stops
sending it asks it to start again. It sends a hit as soon as the first sweep sees the ball, so the
position is where the ball was first seen.

## Checking the catch rate

With the sensor mounted, run `python ../DevTools/robilidar_probe.py --catch`. This counts throws
and catches without the game.

1. Keep the area clear for the first 5 seconds while it learns the empty scene.
2. Leave it alone for a minute. Any impact it reports now is a false alarm.
3. Throw balls at the wall and press Enter after each one has landed. It says whether the
   throw was caught or missed, and Ctrl+C prints the totals.

The totals also show how many sweeps each impact was seen in. Impacts seen in only one sweep are
the ones that a slightly faster throw would miss.

## How the sensor talks

All UDP, between the sensor at `192.168.0.10:2112` and any local port on the PC.

- **Start:** send these 8 bytes: `FE 5A A5 55 00 02 01 01`. The sensor then streams sweeps back
  to the sender's address. Nothing arrives until this is sent.
- **Stop:** the vendor's driver sends the same 8 bytes. It may be a toggle, so send the command
  once, and again only if nothing arrives.
- **Packets:** each sweep arrives as 7 packets of about 1400 bytes, with a header, a sweep
  number, the packet's place in the sweep and an XOR checksum. The scan data is a list of
  distances in millimeters (16 bit, little endian; `1` means no reading) followed by the same
  number of intensities. One packet carries the scan info (angle step, sample count, sweep
  time).
- **Angles:** the vendor's driver assumes the sweep runs from -135 to +135 degrees with 0
  straight ahead. Which way is ahead on the unit itself still has to be checked.

The exact byte layout is written out at the top of
[`../DevTools/robilidar_probe.py`](../DevTools/robilidar_probe.py), which is a working decoder
to copy from.

## Vendor documents

- Download page: <https://robify.com/download/>
- The manual and the ROS driver source are linked from there. The driver is only useful as a
  reference for the packet format, which is already written down above.
