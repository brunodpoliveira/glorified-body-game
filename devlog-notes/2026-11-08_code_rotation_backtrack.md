# 2026-11-08: the code rotation as a backtracker, the code 8675309

Bruno, after the CODE TEST room:

```text
the correct code to rotate is 8675309; the pa call is made so soon on the demo there is no chance for the player to
grab a card and use it. the code rotation puzzle is another backtracker; so the player needs to make progress by
grabbing the card, using it to unlock some previously unlocked doors, then the code is rotated so the player needs to
go to a security office and reset the card's password to the code on the wall
```

## What changed

1. **The code is 8675309.** `CodeTerminal.code` has this default. The keypad (`KeypadUI`) shows one digit for each
   digit of the terminal's code, so the code has seven digits now. `KeypadUI.Length` is a property now, not a constant.
2. **The codes rotate after the card opens doors.** `Story/CodeRotation.cs` counts the card doors that are open.
   When `opensBeforeRotation` doors are open (2 in CODE TEST), it waits `delaySeconds` (2 s). Then it plays the PA
   call 1B and raises `rotatedFlag`. The count reads the doors, not a counter, so a load gives the same answer. A door
   that she opened stays open. Every card door that is not open yet refuses the card until the terminal enrols it.
3. **CODE TEST has six rooms now.** The card is on a table in the hall. Three card doors go from the hall: store A,
   store B and the way on. Each door has a reader. The PA switch is gone. The security post and the day room stay as
   they were. The wall of the day room shows 8 6 7 5 3 0 9.
4. **The demo's W14 wall shows 8 6 7 5 3 0 9** (`DemoV2.W14`). The demo has no rotation yet.

I read "the code on the wall" as the code of §10.2: the wall of the day room (W14), and the terminal in the security
post (W13).

## Proof

| Check | Run | Result |
|---|---|---|
| `CodeRotationTests` (7, one new: the card opens two doors, then the PA, then the third door refuses) | `20261108-142724-test-playmode` | 7 of 7 |
| The code bot, `play CodeTest Main_Menu code 300` | `20261108-143120-play` | 7 of 7 steps, triage CLEAN |

Shots: `~/MEGA/glorified-body-renders/game/code_rotation_backtrack_2026-11-08/`.

1. `code_01_pa_rotates_the_codes.png`: store B is open, its reader is green, and the PA line shows.
2. `code_02_reader_refuses.png`: the way on refuses the card. CODE ROTATED. SEE SECURITY.
3. `code_03_the_code_on_the_wall.png`: 8675309 on the day room wall.
4. `code_04_keypad.png`: seven digits on the keypad.
5. `code_07_door_open.png`: the enrolled card opens the way on.

A note on the bot shots: the cull draws only the rooms that the live camera sees. A free shot camera can show a black
room behind an open door. This is correct. The PA shot uses the live camera now.

## For the writer (the docs disagree with the ruling now)

1. DEMO_PLAN §10.2: "Four digits", "5 2 8 1", "10,000 codes". The code is 8675309 now (seven digits).
2. DEMO_PLAN §10.2 step 1 and beat 14: the PA call 1B rotates the codes at the van. The rotation now comes after the
   card opens doors. The guards' part of call 1B can stay at beat 14.
3. `game/barks.csv`: the guard's radio line in W5 says "Five. Two. Eight. One."

## OPEN for Bruno

1. Which ward doors does the card open before the rotation? PROPOSED: two side rooms that she passes locked before
   she has the card, for example W3 (patients) off W2 and W10 (research office) off W9. Then W12 refuses the card.
2. The number of doors before the rotation: 2 (PROPOSED).

## The demo build (a fault found on the way)

The demo rebuild for the W14 code showed a fault in `DemoWorld.BuildAll`. It scanned the camera sight lists before it
saved the scene. That scan saw more rooms than a scan of the saved scene: `Cam_W1B_00` saw W14 and W6. The game plays
the saved scene, so `TheCull_DrawsOnlyTheRoomsTheCameraSees_AndHers` failed after the full build. Now `BuildAll`
saves the scene and then runs `Resight` on the saved scene. The cause of the difference is not proved.

| Check | Run | Result |
|---|---|---|
| `DemoTests` and `FloorplanV2Tests`, after `BuildAll` with the fix | `20261108-145258-test-playmode` | 47 of 48 |
| `R1_ThePosterBeat_TheFlashback_UnlocksMkEdgar_AndTheAttendantFollows` alone, two runs | two runs | 2 of 2 |

OPEN: `R1_ThePosterBeat_...` failed 1 of 4 runs ("the flag goes up", 8 s after the flashback). It failed only in the
full class. This change does not touch the coda.
