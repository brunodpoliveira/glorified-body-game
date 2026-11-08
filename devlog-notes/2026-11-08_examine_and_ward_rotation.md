# 2026-11-08: the common interaction, and the code rotation in the ward

Bruno:

```text
and also how are with the common interactions? lucy can interact  with objects normally (pressing e) and she'll comment
about it. there should be placeholder lines in cases of cupboards already cleaned up of items (nothing else important
here or nothing of note here something like that)
1: w3,w10
2: correct
3: add The test tape in KIT TEST, so you can hear a fight pause; then when manual testing is done we can add l19 and
l20 in the demo
```

## 1. The test tape in KIT TEST

The test tape lies beside her start (`Tape_Kit`). Take it, walk east, and the soldiers' fight pauses it. The T key or
the inventory plays it again. `KitTests.TheTapeOnTheFloor_PlaysWhenTaken_AndTheSoldiersFightPausesIt` takes it with
the Interact key. Commit `a8edeff`. L19 and L20 go into the demo after the manual test.

## 2. The common interaction

1. `Interaction/Examinable.cs`: the Interact key on a thing shows her comment in the message box. There is no
   question and no voice.
2. A container holds hidden pickups. While one is left, the key asks "There is the MK capsule inside. Take it?".
   When all are gone, it shows "Nothing else of note here."
3. A broken prop has no comment. A broken container keeps its items.
4. The focus rule: a comment has the lowest priority (`Interactable.Priority`). A log on a desk, a door or a switch
   in range gets the key first.
5. Every demo prop gets a comment from `DemoRoom.Prop`. Nobody places them by hand. The line comes by the prop's
   type ("Desk_Nurse" is a Desk). The structure (walls, handrails, stairs) gets none. `DemoRoom.Container` makes a
   container with items.
6. The lines are PLACEHOLDERS: "A desk. Nothing of note here.", "A cabinet. Nothing of note in it." The writer can
   replace any of them by type in `the-glorified-body/game/examine.csv` (two columns: `type,line`). The next build
   reads the file.

P7 (Lucy speaks once in act 1): the comment is text in the message box, with no speaker label and no voice. The
writer decides the words.

CODE TEST: the shelf of store A holds an MK capsule, and the shelf of store B is empty. `ExamineTests` has 3 tests:

1. The card on the hall table keeps the key. Then the table shows its comment.
2. The shelf in store A asks for the capsule. Then it says that nothing else is there.
3. The shelf in store B says that nothing is there.

Other test rooms built with `DemoRoom` (DOOR, DARK, BRAIN, the tutorial) get comments at their next build.

## 3. The code rotation in the ward

1. The ward sister's card now also opens W3 (from W2) and W10 (from W9, and from W13). Before she has the card, these
   doors say "It is locked. It needs the ward sister's card."
2. After the card opens two of these doors, the PA call 1B rotates the codes (`Code_Rotation` in W12).
3. The stairwell door of W12 rotates the codes at the first try (`LockedDoor.rotatesOnTry`). So the player cannot
   pass the puzzle when she goes to the stairs first. This is PROPOSED.
4. W13 has the terminal (code 8675309). W14 has the code on the wall.

There is no soft lock: W13 is open from W12 at all times.

## Proof

| Check | Result |
|---|---|
| `KitTests` | 6 of 6 |
| `ExamineTests`, `CodeRotationTests` (8: one new, the way on rotates the codes at the first try) | 11 of 11 |
| `InteractionTests` with the focus rule | 11 of 11 |
| The code bot (a new step: the table comment) | 8 of 8 |
| `DemoTests`, `FloorplanV2Tests` and the classes above, run `20261108-153652` | 71 of 73 |
| The 2 failures after the fixes below | 2 of 2 |
| The demo bot, `play Demo Main_Menu demo 900` | pass, triage CLEAN |

`DemoTests` has 3 new tests: the rotation in the ward, the stairs first, and a comment on every prop (227 props). The
2 failures:

1. `ADoorway_ChangesTheRoom_...` walked Lucy through the W3 doorway, which is a card door now. It uses the W4 doorway.
2. The new rotation test did not wait for the end of the W1 wake. The wake stops time, and the PA waits on game time.

## OPEN for Bruno

1. The rotation at the first try of the stairwell door (item 3 above).
2. The hint over a prop says "E look at the desk". It shows near every prop. Keep it, or show no hint for a comment?

Shots: `~/MEGA/glorified-body-renders/game/examine_and_ward_rotation_2026-11-08/` (`code_01_table_comment.png`: the
comment in the message box).
