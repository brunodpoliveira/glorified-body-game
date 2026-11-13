# 2026-11-05: the floorplan v2 in greybox

Bruno, round 34: "you can get started on the v2 map; i'll be going to sleep, so order the coding agent to build it as well".
The map: `game/act1_map_v2.py` by the writer, and `~/MEGA/glorified/renders/game_design/act1_map_v2.png`. The table:
`game/DEMO_PLAN.md` §3.4. The beat: `game/cutscenes/07b_the_conference_room.md`, beat 22b.

## What is built

Twelve rooms in greybox, in the one world scene. `DemoV2.cs` holds their specs and their dressing. `DemoWard` and
`DemoPublic` list them after their own rooms. Each room has its boxes, its walls with the doorways of the map, its cameras
by the coverage rule (no blind spot, the exits in view), its NavMesh, a line in the map atlas, and a sign with its name by
its door. The main way (W1 to W12, G1 to U1) did not move. A test holds the rectangles of its rooms.

| Room | Plan metres (x0, z0 to x1, z1) | Doors | What is in it |
|---|---|---|---|
| W13 security post | 17, -14 to 23, -10 | W12, W10 | a desk, the terminal (a dark screen, no puzzle), the empty chair, a radio on a shelf, a key cabinet |
| W14 children's day room | -10, 9 to 0, 14 | W2 (its west end) | a TV with the sound down, a low table, a warm cup, toys on a mat, crayon drawings at 1 m. A sun, a house, a long black car, a door with a red dot and `5 2 8 1` over it, the crayon on the floor under it |
| W15 staff break room | 24, 9 to 34, 14 | W2 (its east end) | a counter with the sink and faucet, the microwave, the coffee machine, a fridge, a table, `WASH YOUR CUP.` |
| W16 cold room | 3, -23 to 8, -18 | W11 | three racks of sample boxes, the freezer leaf open, `-80 C`, the log of temperatures |
| W17 storage | 8, -22 to 13, -18 | W11 | shelves, boxes, a spare small chair of the lab |
| G9b comms closet | 34, -16 to 38, -9 | G9 | racks on two walls with green LEDs, three PCs on UPS units on the floor with the labels NET, REC, ACCESS, an amber LED each. `COMMS. AUTHORISED PERSONNEL.` on the G9 side |
| G10 cafeteria | 28, -5 to 38, 0 | G2, G5 | a hot counter with trays, four tables on legs with chairs, the microwave and the coffee machine on a side counter, two civilian hiders by the tables, a menu board |
| G11 staff break room | -2, -22 to 6, -16 | G8, G15 | the same counter as W15, a fridge, a table, lockers |
| G12 conference room | 26, -22 to 38, -16 | G5 | the long black table, nine chairs pushed back, five cards, the screen with the slide, the projector, a water jug, a flip chart, the full cup |
| G13 admissions office | 4, -9 to 12, 0 | G2, G7 | a counter, a queue barrier, six desks with a monitor, a keyboard and a chair each |
| G14 builders' storage | 26, -28 to 32, -22 | U1 | a pallet of cement bags, a ladder, a roll of sheeting, a work light, a bucket, sheeting on the wall |
| G15 staff toilets | -2, -27 to 6, -22 | G11 | three stalls with two toilets (one works), two sinks under a mirror, the faucet |

### Where the built rooms differ from the map

The v1 rooms stand in the scene in a different arrangement from the map. W8 is north of W9, not beside it. G2 runs along
the whole north wall of G5. The doors of the map are kept where a wall exists for them. Four places differ:

1. **W13's second door opens into W10, not W8.** No wall of W8 touches a room beside W12: W9 fills the space. The loop
   around W9 holds (W12, W13, W10, W9). OPEN for Bruno.
2. **W14 opens off the west end of W2** (its west wall), not off the north wall. W1B and W3 fill that wall.
3. **W15 opens off the east end of W2** (its east wall). The north end of W5 is the junction with W2 in the scene.
4. **G6 and G8 moved.** G13 needs G2 and G7, and only the old place of G6 touches both. G10 needs G2 and G5, and only the
   old place of G8 touches both. G6 (the public bathrooms) is at 28, -9 to 36, -5 now, east of the hall under the
   cafeteria. G8 (offices and records) is at 6, -22 to 14, -16, south-west of the hall beside U1, with the door to G11.
   Their contents moved with them. The nurse Civilian_B of G5 stands 2 m east of her old place (the door to G8 is at x 13).
   The fourth sheet of U1 hangs at the far end now (it hung in front of the door to G14).

Three props of v1 rooms moved for the NavMesh agent (0.5 m), so the enemies can follow her through the new doors:

- W11: the six small chairs stand 1.8 m from the south wall, in two groups of three. They stood 1.2 m from it, and
  left no way to the doors of W16 and W17 behind them.
- G9: the desk is 3.4 m wide (it was 4 m). It left 1 m to the east wall, where the door to G9b is.
- G12: the chairs stand 0.3 m from the table, so the aisles along the walls are 1.5 m. At 0.45 m the aisles were 1.35 m
  and the bake dropped them, and the east half of the room had no way in.

`DemoV2.NavMeshReport` (batch: `tools/unity.sh exec GlorifiedBody.Editor.Demo.DemoV2.NavMeshReport`) prints for every
room the floor points with mesh under them and how many a path from G5 reaches. It found the G12 fault.

### Two faults found on the way, both older than this work

1. **A hole in a wall when its builder is far.** Two rooms share a wall line and one of them builds the piece
   (`WallLedger`, 2026-11-02). The cull turned off the renderers of every room that is not hers or next door, the shared
   piece with its builder. A room then had a hole where a non-neighbour built its wall: the north wall of W9 (built by
   W7), the north-west corner of G13 (built by G1), the north wall of G12 (built by G9 and G9b). The old shots of W9 show
   the black hole. CONFIRMED. Fix: the cull keeps the wall and lintel pieces drawn (`RoomTracker.Cull`, one condition).
   Floors and props are culled as before, and the culling test holds.
2. **Every sign stood inside its wall.** A wall is 0.3 m thick, on the room line. The signs, the mirrors, the window of
   W9, the pinboard, the whiteboard, the card reader and the donor wall stood 0.02 to 0.05 m off the line, inside the
   slab. A sign showed only where its wall piece was culled (fault 1). The shot of G13 shows the ADMISSIONS sign hidden
   on a drawn wall. CONFIRMED. Fix: every wall-mounted thing stands 0.17 m off the line (its face 0.02 m in front of the
   wall). The L4 whiteboard item moved 0.02 m with its board.

### The fixtures (round 30, `Fixture.cs`)

The Interact key, a placeholder sound, a small move of the prop, no effect on the game:

| Fixture | Where | What happens |
|---|---|---|
| Microwave | W15, G10, G11 | the window glows 12 s with the lunch box inside, the hum, then the ding |
| Coffee machine | W15, G10, G11 | the pump, the coffee rises in the cup over 3 s, the cup leaves while she drinks, it comes back empty |
| Toilet | W4, W6, G6, G15 | the flush, the water drops and comes back. In G6 the hider answers "Occupied." once |
| Faucet | W4 (under the long mirror), W6, G6, G15, W15, G11 | the stream shows and the water runs until she leaves the room, or until a second press |

The sounds come from `tools/fixture_placeholders.py` (numpy, 0 cost), in `Resources/Audio/Sfx/`.

### The conference room beat (cutscene 07b, first pass, 0 art)

The cards are flat props with a `NameCard`. The Interact key on COLTON or CHURCH reads the name. On a card face down it
reads "The card lies face down." On DE HOMEN-CRISTO the first time, it plays the flashback. The flag `demo_g12_card` goes
up at that first press, so the second press reads the card only.

The flashback is `Resources/Cutscenes/cutscene_07b_the_conference_room.asset`: nine shots, 13.5 s, hard cuts.

| Shot | Seconds | What |
|---|---|---|
| F0 | 2.5 | black. Gravel under tyres, slow, then a heavy car door |
| F1 | 0.8 | a text card (the gravel drive, the sill of the long black car) |
| F2 | 2.0 | black. The metronome starts, and runs 4 s under F3 and the line |
| F3 | 0.8 | a text card (the white line, two pairs of small feet) |
| | 1.2 | black. "Encore." |
| F4 | 0.8 | a text card (the small hand around the rod). Three ticks, then the blow at 2.35 s |
| | 2.2 | black. "Un. Deux. Trois." |
| F5 | 1.6 | black. "Elle ne pleure pas." |
| | 1.6 | black. "Pas elle. L'autre." |

A cutscene shot carries a sound now (`CutsceneAsset.Shot.sound`). It starts with the shot and runs under the next ones. A
skip stops it. The French lines are subtitles with no voice and no speaker name in the asset. The subtitle system puts its
fallback name VOICE in front of them (OPEN point 3). After the flashback the SHORT bar rises by 10 of 100 (PROPOSED).

In U1, "Encore." is among the voices of the night when the flag is up. The voices of cutscene 09 are not built, so a
sequence under a flag gate (`Gate_Encore`) says the one line 2.5 s into the Crown beat, in the same trigger.

## What is not built

- No puzzle logic. The terminal of W13, the code on the wall of W14 and the three PCs of G9b wait for the picks of Bruno
  (`PUZZLES.md` §2 and §2B). The W12 card door works as before.
- The hum of the projector, the three lights of the Crown after the flashback, the white gear on the slide (a plain light
  square holds its place).
- The hiders of G10 shelter beside the tables, not under them. The capsule of a figure cannot sit inside the collider of
  a table top.
- The lines of the writer for the two hiders of G10 (PLACEHOLDER lines in `DemoV2.G10`).

## PROPOSED numbers

- The SHORT bar rises 10 of 100 after the flashback (the writer: "a tenth of the bar").
- The microwave runs 12 s. The coffee fills in 3 s, she drinks 2 s. The flush lasts 3 s.
- The room sizes above (about 2 m a map unit, as the v1 rooms).

Not done: the labels NET, REC, ACCESS on the PCs of G9b are 0.26 m plates and read only from close up. The hum of the
projector in G12 is not built.

## Open for Bruno

1. The second door of W13: W10 (built), or a change to the ward so it can be W8.
2. G6 and G8 moved to make room for G13 and G10. Keep, or another arrangement.
3. The French lines of the flashback show as "VOICE: Encore." (the subtitle rule: a speaker always). The writer asked for
   no speaker name. One line of code changes it if you want it.
4. The two hiders of G10 beside the tables, not under them.

## Proof

| Check | Result |
|---|---|
| `FloorplanV2Tests` (4). Run `20261105-054826-test-playmode-189863` | 4 of 4 |
| `DemoTests` (22). Run `20261105-054938-test-playmode-190166`. 0 of 6168 floor points out of frame, 0 exits out of view | 22 of 22 |
| EditMode (61). Run `20261105-055752-test-editmode-191405` | 60 of 61 (see below) |
| Bot `play Demo Main_Menu demo 900`, run `20261105-055626-play-191062`: 36 rooms, 31 figures, 114 cameras, one shot each | pass, 156 steps, triage clean |
| `DemoV2.NavMeshReport`: every new room has mesh under its floor and a path from G5. G12: 148 of 148 points. G8: 107 of 107 | as expected |
| The shots of the twelve rooms, G6 and G8, from every camera, checked at full size | 31 files |

The one EditMode failure is `DeathScreenContentTests`. It counts the rows of `game/death_screens.csv` in the writers' room.
It expects 7 rows and the file has 6. This work did not touch it.

The four v2 tests: the twelve rooms with the doors of the map, a camera each, their sign, a NavMesh way in, the main way
unmoved. The head card and the flashback. The 16 fixtures. "Encore." in U1. In the editor report the ward rooms show 0
reached from G5: the stairwell card door stands in W12, as before.

Shots: `~/MEGA/glorified/renders/game/demo/floorplan_v2_2026-11-05/` (`demo_cam_<room>_<n>.png`). The run
`20261105-052931-play-186308` holds the same shots from before the two wall fixes, with the holes and the hidden signs.

Limits: only the suites above ran, not the whole PlayMode suite. Nobody played the rooms by hand. The sounds and the cards
of the flashback are placeholders. The flashback test skips the cutscene after its first second.
