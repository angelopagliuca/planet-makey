# Invaders from Planet Makey

A game for Maker Faire 2026.

Aliens from Planet Makey come at night to abduct your herd of cows. Team up and fire real
colored balls at the projected wall: hit the UFO with a ball that matches the color of its
shield to break it and save the cow. Keep as many cows as you can until the rooster crows.

## Requirements

- Unity **6000.6.4f1**, installed through Unity Hub
- Python 3.6 or newer, only for the test scripts in `DevTools/` (nothing to install with pip)

## Repository layout

| Path | Contents |
|------|----------|
| `PlanetMakey/` | The Unity project (3D, Universal Render Pipeline, Input System) |
| `PlanetMakey/Assets/` | Scenes, scripts, art and settings |
| `PlanetMakey/Packages/` | Package manifest |
| `PlanetMakey/ProjectSettings/` | Unity project settings |
| `PROTOCOL.md` | Messages the booth hardware sends to the game |
| `Hardware/` | The booth side: wall sensor notes and the program that sends hits to the game |
| `DevTools/` | Test scripts: fake the booth hardware for the game, and check the real sensor |

Anything that is not part of the Unity project, such as concept art and reference
material, lives next to `PlanetMakey/` at the repository root.

## Farm art packs

The farm environment uses purchased 3D asset packs, which their licenses do not let us
redistribute, so they are not in this repository. They go in `PlanetMakey/Assets/_External/`
(`SimpleFarm` and `Holotna`), which git ignores. Without them the farm scenes open with
missing references, but the game's code still compiles and runs. To see the full scene, buy the
packs from the Unity Asset Store and import them into that folder:

- [Simple Farm - Cartoon Assets](https://assetstore.unity.com/packages/3d/environments/industrial/simple-farm-cartoon-assets-47003) (`SimpleFarm`)
- [Farm - Stylized Fantasy Environment](https://assetstore.unity.com/packages/3d/environments/fantasy/farm-stylized-fantasy-environment-310161) (`Holotna`)

## Progress

The hit pipeline is done: a ball hit arrives from a mouse click or from the booth hardware over the network, is lined up with the projected picture, and is traced into the scene to the object it lands on. Makey the UFO flies in with a colored shield, reacts to right and wrong color hits, and abducts the cows one by one until the shield is broken. Next up is a full three-minute round.

A box is ticked once that step has been built and tested.

**1. Hit pipeline** (week of October 5): a ball hitting the wall reaches the game

- [x] 1.1 Mouse clicks stand in for ball hits, with a color for each ball
- [x] 1.2 A hit is traced into the scene and the object it lands on reacts
- [x] 1.3 The game receives hits from the booth hardware over the network
- [x] 1.4 Calibration lines up the wall sensor with the projected picture

**2. Abduction loop** (week of October 12): the core game with placeholder shapes

- [x] 2.1 Makey flies in with a colored shield and reacts to right and wrong color hits
- [x] 2.2 Makey abducts cows; a matching hit saves the cow

**3. Round flow** (week of October 19): a full round from dusk to dawn

- [ ] 3.1 A three-minute night with a score at dawn, then back to idle
- [ ] 3.2 Operator screen and a start signal from the booth
- [ ] 3.3 Difficulty rises through the night (optional)

**4. Booth readiness** (week of October 26): ready for a full day at the faire

- [ ] 4.1 Sound and animation for every event in the game
- [ ] 4.2 Runs unattended as a fullscreen build
- [ ] 4.3 Lights flash when a ball hits (stretch goal)
