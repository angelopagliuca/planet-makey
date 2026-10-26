# Hardware

The booth side of the game: the wall sensor, and the program that turns what it sees into the
`HIT` messages the game reads (see [`../PROTOCOL.md`](../PROTOCOL.md)).

That program will:

1. read the sweeps from the sensor and clean them up, leaving one position per ball impact
2. read the color of the ball from the cannon
3. send `HIT <x> <y> <color>` to the game

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

## Checking the catch rate

With the sensor mounted, run `python ../DevTools/robilidar_probe.py --catch`.

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
