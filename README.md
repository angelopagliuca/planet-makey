# Invaders from Planet Makey

A game for Maker Faire 2026.

Aliens from Planet Makey come at night to abduct your herd of cows. Team up and fire real
colored balls at the projected wall: hit the UFO with a ball that matches the color of its
shield to break it and save the cow. Keep as many cows as you can until the rooster crows.

## Requirements

- Unity **6000.6.4f1**, installed through Unity Hub
- Python 3.6 or newer, only for the test scripts in `DevTools/` and the wall sensor program in `Hardware/` (nothing to install with pip)

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

The hit pipeline is done: the cannon tells the game which color ball is loaded, so the wall sensor only has to report where a ball landed. A ball hit arrives from a mouse click or from the booth hardware over the network (TCP), is lined up with the projected picture, and is traced into the scene to the object it lands on. Makey the UFO flies in with a colored shield, reacts to right and wrong color hits, and abducts the cows one by one until the shield is broken. The scene has a sky that changes from day to night, a glowing shield in six colors, a glowing beam that flares out at the ground, and small lamps that come on at dusk. Small cow icons show how many cows are left, and a moon that slowly turns into the sun shows how much of the night remains. The cows hop and turn on their own, and the one Makey takes trembles, stares with big eyes and shrinks a little as she reaches the ship, while Makey plays his warp animation. A full three-minute round now runs from dusk to dawn: an operator starts it with a key, the players defend the herd, and the cows left at dawn are the score. The booth's start button can start a round by network message, and an operator panel shows the state of the round, the latest hit and the hardware link, with a way to end the night early, so dawn comes up as usual. Sound is being added: the music follows the night, and every sound effect has its own slot to fill with a clip. Next up is making the game run unattended as a fullscreen build.

A box is ticked once that step has been built and tested.

**1. Hit pipeline** (week of October 5): a ball hitting the wall reaches the game

- [x] 1.1 Mouse clicks stand in for ball hits, with a color for each ball
- [x] 1.2 A hit is traced into the scene and the object it lands on reacts
- [x] 1.3 The game receives hits from the booth hardware over the network
- [x] 1.4 Calibration lines up the wall sensor with the projected picture
- [x] 1.5 The cannon remembers the loaded ball color, and the hardware connects to the game over TCP
- [ ] 1.6 A program turns the wall sensor's scans into hits

**2. Abduction loop** (week of October 12): the core game with placeholder shapes

- [x] 2.1 Makey flies in with a colored shield and reacts to right and wrong color hits
- [x] 2.2 Makey abducts cows; a matching hit saves the cow
- [x] 2.3 Makey hovers at a different height over each cow, always on screen
- [x] 2.4 Makey leans, sways and strains as he works, with a shadow on the ground and a glowing pool under the beam
- [x] 2.5 A night that is dark but readable, with a real sky that changes from day to night
- [x] 2.6 The shield glows, and the balls come in six colors
- [x] 2.7 A polished abduction beam
- [x] 2.8 Small lamps (tractor headlights, a window) that come on at dusk and go off at dawn
- [ ] 2.9 Makey glows, so he can be seen coming and going at night (parked for now)
- [x] 2.10 Clouds drift across the sky
- [x] 2.11 The cows come alive: they hop now and then, their eyes go wide when Makey takes them, and they shrink a little as they reach the ship
- [x] 2.12 Makey plays his warp animation while he takes a cow

**3. Round flow** (week of October 19): a full round from dusk to dawn

- [x] 3.1 A three-minute night with a score at dawn, then back to idle
- [x] 3.2 Operator screen and a start signal from the booth
- [ ] 3.3 Difficulty rises through the night (optional)
- [x] 3.4 Holding the start key during the night skips to dawn
- [x] 3.5 The screen shows the cows left as small icons and the night as a moon that turns into the sun

**4. Booth readiness** (week of October 26): ready for a full day at the faire

- [ ] 4.1a The game's music fades in at dusk and out at dawn
- [ ] 4.1 A sound effect slot for every event in the game (round, Makey, the beam and the cows)
- [ ] 4.2 Runs unattended as a fullscreen build
- [ ] 4.3 Lights flash when a ball hits (stretch goal)
