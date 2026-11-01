# Bipolar Bear Remastered

A 3D remaster of **Bipolar Bear**, a 2014 side-scrolling platformer ("BiPolar Bear" by Team Team). A polar bear
crosses the Arctic while its mood changes every few seconds, and each mood gives it a different ability. The game
plays exactly like the original (2D physics, the same level, the same side view), with every visual and sound
remade: low-poly 3D models, new backgrounds and HUD, particle effects, music and sound effects.

Unity **6000.5.10f1** (built-in render pipeline). Open the folder in Unity Hub; the scenes are
`Assets/Scenes/MainMenu.unity` and `Assets/Scenes/GameScene.unity`.

## How to play

| Key | Action |
|---|---|
| A / D | walk |
| W | jump |
| Space | the current mood's ability: **punch** (aggressive), **roar** (hysteric), **super jump** (ecstatic) |
| Enter | take a pill: the next mood's ability on Ctrl until the mood changes |
| Left Ctrl | the pill's ability |
| Esc | back to the menu (on the menu: quit) |

- A **punch** knocks eskimos away (and protects the bear from their spears while it lasts).
- A **roar** stuns eskimos for a few seconds.
- The level picks one of punch / roar as the bear's weakness: use it too often and the bear falls asleep (roar)
  or starts crying (punch) for five seconds, unable to move.
- Crystals, spikes, volcanoes, eskimos and their spears are deadly; the level restarts after the bear's death.

## Building and testing

The PowerShell tools run Unity in batch mode (close the Editor first). Each ends with one line `OK: ...` or
`FAIL: ...`.

```powershell
powershell -File tools\unity_run.ps1 -Method GameBuild.Windows -Expect "BB build: Succeeded"   # -> Builds\Windows
powershell -File tools\test_run.ps1                       # build + the six automated play-throughs
powershell -File tools\test_run.ps1 -NoBuild -Modes roar  # one run on the last build
```

`test_run.ps1` starts the built game with `-bear-test` (handled by `Assets/Scripts/GameTestRun.cs`, which does
nothing in normal play) and checks the logs and screenshots it writes to `Logs/test`:

- **normal**: the bear stands with its feet on the ground, its sounds, the menu / level music, the crystal glow and
  volcano smoke, a blizzard, the jump animation in step with the jump, how the bear speeds up, stops, steers in the air and how high it jumps, how fast the eskimos walk, stopping at a wall, picking up a pill, the
  head stopping under a platform, the characters in front of a platform
- **walk**: the bear walks into the first danger and dies with its death animation
- **eskimo**: a stunned eskimo (stars), then its spear thrust kills the bear
- **roar**: the sound wave starts at the bear's mouth; a roar to the left disappears too
- **punch**: no sliding while asleep; a pill punch knocks an eskimo away (it plays its death); the reach resets
- **relocate**: sleep waits until the bear has landed; dying on a hazard or an eskimo shows the impact and puts the
  bear on the ground in front of it

`tools/unity_run.ps1 -Method <Class.Method>` runs any static editor method; `Assets/Editor/GameBuild.cs` is the
build (menu **Bipolar Bear > Build Windows**).

## How it is put together

- **2D game, 3D look**: the gameplay is 2D physics as in the original (`BearMotor` for the bear,
  Rigidbody2D eskimos, box colliders). Every object carries a 3D model (`Model3D` child) fitted to where the old
  sprite was; the level camera is orthographic, looking down 8 degrees.
- **Depth**: everything the bear meets stands on one middle line (z 0); the ground and platforms are boxes centred on
  it. A second camera (`CharacterCamera`, layer **Characters**) draws the bear, the eskimos and their effects over
  everything else, so they never look hidden behind a platform.
- **Animation**: the models are rigged with clips (Idle, Run, Jump, Punch, Roar, Sleep, Cry, Death; eskimo Walk,
  SpearThrust, HitHead, Dizzy, Death). The jump clip is started at its take-off and its flight fitted to each jump's.
- **Scripts** (`Assets/Scripts`): `PlayerController` (bear: moods, abilities, pills, penalties, death), `BearMotor` (its movement and collisions),
  `CameraFollow`,
  `EnemyLogic` (eskimo: patrol, spear attack, stun, death), `GameLogic` (death and restart), `GrowlBehavior` (the
  roar), `MenuLogic`, `LegacyGUI*` (the HUD: Unity 4 GUITexture / GUIText stand-ins), effects (`MoodFx`, `FxFloat`,
  `ImpactBurst`, `Glow`, `Blizzard`, `StunStars`, `SpearHold`, `Spin`), `GameNames` (scene, animation, layer and
  object names in one place), `FrameRate` (the game runs at 60 fps, as it was tuned for).
- `Assets/Shaders/SnowTop.shader`: the platforms' snow tops show their colour regardless of the light.

## Credits and licences

- Original game: Bipolar Bear (2014), Team Team.
- Models, textures, pictures, music and sound effects of the remaster were made with a local AI asset pipeline.
- This repository: see `LICENSE`.
