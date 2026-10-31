# 2026-10-31, night: the player build and the demo in greybox

Bruno:

```text
Start the player build, then create the floor 2 and 1 of the demo I greybox
```

## 1. The player build (the sweep's rank 16)

- `tools/unity.sh build release|dev [linux|windows]` makes a player in `builds/<kind>_<target>/TheGlorifiedBody`
  (`Editor/PlayerBuild.cs`). `builds/` is out of git and out of MEGA.
- A release has the menu, the training room, the tutorial and the 22 demo rooms. It has no test room, no look room
  and no debug key.
- A dev build has every scene of the build list. It is a development build.
- The dev flag `GameBuild.Dev` is true in the editor, in a development build, or with `-gbDev` on the command line.
  When it is false, the debug keys (F1, F2 skip, F4, F5 to F9) do nothing, and the main menu does not show the
  TEST ROOMS column.
- The build mode has a 2 h limit (`GB_BUILD_TIMEOUT`). The first release build stopped at the 30 min batch limit
  during the shader compile, and the triage said CLEAN. Now the triage marks a run DIRTY when the log has no
  "Exiting batchmode successfully" line.
- Tests: `PlayerBuildTests` (EditMode, 2: the release list has no test room; the dev list has all scenes),
  `PlayerBuildFlagTests` (PlayMode, 2: no debug key and no test column when the flag is false).
- RELEASE BUILD RESULT: see the end of this note.

## 2. The demo, floors 2 and 1, in greybox

The rooms come from the demo plan (`the-glorified-body/game/DEMO_PLAN.md`), one scene a room, in
`Assets/_Game/Scenes/Demo/`. Build them with:

```text
tools/unity.sh exec GlorifiedBody.Editor.Demo.DemoWard.BuildAll      (floor 2: W1 to W12)
tools/unity.sh exec GlorifiedBody.Editor.Demo.DemoPublic.BuildAll    (floor 1: G1 to G9, U1)
tools/unity.sh exec GlorifiedBody.Editor.MapAtlasSetup.BuildAtlas    (after a room changes)
```

The main menu has a DEMO (GREYBOX) button under START. START still loads the training room.

### The kit (`Editor/Demo/DemoRoom.cs`)

- Each room is a box in plan metres (x east, z north), so the map shows the rooms side by side.
- The builder makes the walls with a gap at each door, a lintel, a short stub corridor with the exit at its end,
  and the entry from the other room.
- A closed door with a flag (the fire doors at code silver) shuts when the flag goes up. A short dead end lies
  behind it.
- The helpers place the props, the figures (`EnemySpawn` markers), the items, the scans, the signs, the beats,
  the lockers, the CRT walls, the locked doors and the switches.
- The cameras: each cell of 9 m or less tries camera poses at its corners, at its edge middles, and 2 m past an
  inner edge, with two aims and three lenses (62, 72, 84 degrees). It keeps the pose that sees the most floor by
  the coverage rule (`CameraCoverage.Sees`, now shared with the test). A cell that misses more than 3 % splits in
  two, twice at most.

### Floor 2, THE WARD (12 rooms)

The way: W1, W2, W5, W7, W8, W9, W11, back to W9, W12, down the stairs.

| Room | What is in it |
|---|---|
| W1 RECOVERY | The start: the feed locks, the SHORT bar at 88, cutscene 01 (Stevens on the speaker), then the scientists and cutscene 02 |
| W2 NORTH CORRIDOR | 3 lab workers with pistols, log L9 (it unlocks the feed), a sign |
| W3 PATIENT ROOMS | A patient ("Is mine done?"), log L2, scan C8 |
| W4 SHOWER ROOM | The nurse who hides in a stall, the mirror |
| W5 SOUTH CORRIDOR | 2 guards, scan C5, the code silver PA (cutscene 03): the fire doors shut |
| W6 STAFF LOCKERS | The uniform locker, log L3, a save machine, a poster |
| W7 THERAPY GYM | The CRT wall with the tape lines (cutscene 04), scan C1 |
| W8 INDUCTION SUITE | The induction machine, the technician who hides, a sign, scan C9 |
| W9 NURSES' STATION | Marta (her fate flags, scan C2), cutscene 05, the window |
| W10 RESEARCH OFFICE | Log L7 |
| W11 MAPPING LAB | The chairs, the whiteboard (L4), scan C4, the ward sister with the stair card, 2 pistols, cutscene 06 |
| W12 STAIRWELL | The locked door (the stair card), the reader, scan C3 |

### Floor 1, THE PUBLIC FLOOR (10 rooms)

The way: G1, G2, G5, G9, back to G5, U1.

| Room | What is in it |
|---|---|
| G1 LANDING | A save machine |
| G2 CONCOURSE | Planters, columns, 4 soldiers, "SO THAT HUMANITY ENDURES." |
| G3 DONOR WALL | The donor wall, the plaque, scan C6 |
| G4 CHAPEL | The pews, the verse, a nurse who prays on (never found), log L8 |
| G5 RECEPTION | The desk, soldiers, a pistol, civilians, the sheeting over the Sector C door |
| G6 BATHROOMS | The clerk who hides ("Occupied.") |
| G7 FAMILY ROOMS | The looping CRT (the orientation program), 2 people who hide |
| G8 OFFICES | 2 clerks |
| G9 SECURITY | The monitor wall, scan C7, log L5, the release switch (Sector C), a save machine |
| U1 SECTOR C | The end: the grab tears the uniform off, a card for B1, cutscene 10, the bulletin, the title |

The cutscenes play as subtitles from the writers' files (`game/cutscenes/01` to `10`). The orientation program
speaks as "THE PROGRAM" (the act 1 rule: Kyoko is not named). The signs do not use the company name.

## 3. The proof

- `DemoTests` (PlayMode, 4 of 4 pass, run `20261031-135225-test-playmode-993460`):
  - every room loads with Lucy, every figure's prefab is there, its cameras frame its floor (W1 to G5: 0 points
    out; G6: 1 of 121; U1: 6 of 496; the limit is 4 %), and every exit leads to a room and an entry that exist;
  - the main way travels through 15 doors from W1 to U1, and she stands at the right door each time;
  - W1 starts the story: the feed locked, the SHORT bar over 80 %, Stevens on the speaker;
  - U1 ends it: the grab takes the uniform, the bulletin shows, then the main menu.
- The demo bot (`DemoPlaythrough`, `tools/unity.sh play Demo_W1 Main_Menu demo 900`, run
  `20261031-135337-play-993894`): 75 of 75 steps, one shot for each of the 53 cameras.
- Shots: `~/MEGA/glorified-body-renders/game/demo_greybox_2026-10-31/` (`demo_cam_<room>_<n>.png`).

I looked at all 53 shots. The first set had walls at grey 0.12, the same tone as the dark background, and the floors
seemed to float. The walls are at 0.22 now.

## 4. Open for Bruno

1. G5 RECEPTION has 12 cameras (16 x 16 m, the desk and the columns hide her from a corner). Is that many cuts
   right, or do we accept a few hidden spots?
2. START still loads the training room. Does START go to the demo (W1) now, or after the tutorial?
3. The stair card is on the ward sister in W11. The player must kill her to get it. Is that the intent, or does she
   drop it when she flees?
4. The release build: see below.

## 5. The release build result

- `tools/unity.sh build release linux`, run `20261031-135440-build-994267`: Succeeded, 25 scenes, 508 MB, 27 min.
  Output: `builds/release_linux/TheGlorifiedBody.x86_64`.
- One build warning: the stripped `System.Windows.Forms.dll` is referenced but not supported on Linux. No game code
  uses it (a vendor reference). It is harmless unless a vendor path calls it.
- Smoke test: the player started in batch mode on the null graphics device and ran 40 s with no error and no
  exception in its log. This PC has no virtual display, so a test with pictures needs Bruno: start
  `builds/release_linux/TheGlorifiedBody.x86_64`, check the menu has no TEST ROOMS column, press DEMO (GREYBOX).

## 6. His answers

```text
1: once I do a playthrough I'll let you know, but I prefer no hiding/blind spots
2: start goes to the demo
3: that is one possible solution, but TBD an alternative and a bit out of way solution that'll allow Lucy to progress without her death
Training room is for the players too
```

- NO BLIND SPOTS: a camera cell that misses any floor point splits in two, four times at most. `DemoTests` now
  fails on one point out of frame (it allowed 4 %). All 22 rooms: 0 points out. The cost is in the camera count:
  G5 has 16 cameras, U1 has 13, G6 has 5. Small cells give near-overhead views (`u1_11`, `u1_12`). Bruno judges
  them in his playthrough.
- START loads the demo (W1). TRAINING ROOM is a button under TUTORIAL, in the release too. The DEMO (GREYBOX)
  button is gone. Test: `PlayerBuildFlagTests.Start_LoadsTheDemo_TrainingRoom_LoadsTheTrainingRoom`.
- The stair card: TBD. Killing the ward sister stays one way. A second way, a bit out of the way, must let Lucy go
  on without her death. Not built yet.
- Proof: `DemoTests` and `PlayerBuildFlagTests` 7 of 7 (run `20261031-151629-test-playmode-1005431`),
  `PlayerBuildTests` 2 of 2, the demo bot 87 of 87 (run `20261031-151750-play-1005895`, 65 shots, the shot folder
  has the new set).
