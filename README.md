# Invaders from Planet Makey

A game for Maker Faire 2026.

Aliens from Planet Makey come at night to abduct your herd of cows. Team up and fire real
colored balls at the projected wall: hit the UFO with a ball that matches the color of its
shield to break it and save the cow. Keep as many cows as you can until the rooster crows.

## Requirements

- Unity **6000.6.4f1**, installed through Unity Hub

## Repository layout

| Path | Contents |
|------|----------|
| `PlanetMakey/` | The Unity project (3D, Universal Render Pipeline, Input System) |
| `PlanetMakey/Assets/` | Scenes, scripts, art and settings |
| `PlanetMakey/Packages/` | Package manifest |
| `PlanetMakey/ProjectSettings/` | Unity project settings |
| `PROTOCOL.md` | Messages the booth hardware sends to the game |

Anything that is not part of the Unity project, such as concept art and reference
material, lives next to `PlanetMakey/` at the repository root.

## Progress

The first step of the hit pipeline works: a mouse click stands in for a ball hit. No gameplay yet.

A box is ticked once that step has been built and tested.

**1. Hit pipeline** (week of October 5): a ball hitting the wall reaches the game

- [x] 1.1 Mouse clicks stand in for ball hits, with a color for each ball
- [ ] 1.2 A hit is traced into the scene and the object it lands on reacts
- [ ] 1.3 The game receives hits from the booth hardware over the network
- [ ] 1.4 Calibration lines up the wall sensor with the projected picture

**2. Abduction loop** (week of October 12): the core game with placeholder shapes

- [ ] 2.1 Makey flies in with a colored shield and reacts to right and wrong color hits
- [ ] 2.2 Makey abducts cows; a matching hit saves the cow

**3. Round flow** (week of October 19): a full round from dusk to dawn

- [ ] 3.1 A three-minute night with a score at dawn, then back to idle
- [ ] 3.2 Operator screen and a start signal from the booth
- [ ] 3.3 Difficulty rises through the night (optional)

**4. Booth readiness** (week of October 26): ready for a full day at the faire

- [ ] 4.1 Sound and animation for every event in the game
- [ ] 4.2 Runs unattended as a fullscreen build
- [ ] 4.3 Lights flash when a ball hits (stretch goal)
