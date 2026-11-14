# 2026-11-14: the shortcut doors S1 and S3

Bruno: "build them" (the proposals of rounds 147 and 148, `2026-11-14_proposals_147_148.md` §3). S2 is the lift of
the foyer (built the same day).

## What the game builds

Each shortcut is a steel fire door, shut at the start, with one push bar on the far side. She opens it after the
puzzle, and the way back is short. No kick breaks it. The enemies never open it. `DemoV2.ShortcutS1` and
`DemoV2.ShortcutS3` give the door to both rooms of the doorway (the first room builds it).

| Id | Door | Bar | Rule |
|---|---|---|---|
| S1 | `FireDoor_G12_G17`, in the wall at x 38, z -20.4 | G17 side | The bar works only after the boxes burn (`demo_g17_burned`). During the horn the server room stays sealed, so the halon trap stays a trap. |
| S3 | `FireDoor_W6_W14`, in the wall at z 9, x -1.6 | W14 side | No flag. From the code on the W14 TV to the lockers, then W5, W9 and the terminal of W13. |

Changes for the places:

1. W6 is 1 m longer to the north (z 2 to 9). Its north wall is now the south wall of W14 (a 1 m gap was between them).
   The lockers, the uniform locker, the L3 card and the sign moved 1 m north with the wall.
2. The leaf of S3 slides west, behind the lockers (to the east it showed in W5). The bolt stands east of the doorway, at x -0.45 (with the doorway at x -1 it stood past the corner, in W5). The L3 card hangs on the front of the last locker.
3. The flip chart of G12 moved to the north-east corner. It stood in front of S1.

All places are PROPOSED.

## Proof

1. The demo rebuilt in batch (`DemoWorld.BuildAll`, then `MapAtlasSetup.BuildAtlas`): CLEAN.
2. The first run of DemoTests and FloorplanV2Tests found three failures. The S3 bar stood past the corner, in W5 (the
   doorway moved from x -1 to x -1.6). The map table of FloorplanV2Tests had no W6 door for W14 (added). The G10 tree
   blink test failed once and passed in the rerun (no change in G10: a flaky test, on watch).
3. The rerun: 8 of 8. `TheShortcuts_S1AndS3_...` (both doors shut at the start, one bar on the far side, S1 does
   nothing before `demo_g17_burned`, the bar opens it, the way through is clear), the fire door test (11 doors, the
   levers in their rooms, the leaves in the wall), every exit in its camera (0 of 121 out of view), FloorplanV2Tests.
4. The demo bot (`play Demo Main_Menu demo 900`): CLEAN.
