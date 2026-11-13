# The code rotation, in isolation (2026-11-08)

This is Bruno's puzzle of `DEMO_PLAN.md` §10.2 (CANON, round 52: "the details stand as written, and the game agent builds it"). I built it in a test room first. His words of the night:

```text
i won't buy puppetmaster now; so let's focus on what still needs to be built + tested in isolation
ok i'll be going to sleep now. add what can be tested in isolation after this round of testing is done
```

## What works

1. **The card door with a code.** `LockedDoor` has `rotatedFlag`, `enrolledFlag` and a `reader`. After the rotation flag goes up, the reader refuses the card: "CODE ROTATED. SEE SECURITY." and two bleats. The reader stays red until the terminal enrols the card. Then it turns green, and the card opens the door.
2. **The terminal.** `CodeTerminal` says "PRESENT CARD. ENTER SITE CODE." without the card. With the card, after the rotation, the Interact key opens the keypad.
3. **The keypad.** `KeypadUI` stops the world as a log does. W and S change the digit, A and D move between digits, and the number keys type. E enters the code, and the Inventory key or Escape closes it. A wrong code says "CODE NOT RECOGNISED. CARD NOT ENROLLED." and stays open, at no cost (CANON round 40). The right code says "CARD ENROLLED." and closes after 1.2 s. The power keys do nothing while it is open.
4. **CODE TEST room** (`Editor/Demo/CodeTestSetup.cs`, the TEST ROOMS column). The hall has the card on a table, a switch that plays the PA call 1B, and the coded door to a closet. The security post has the terminal. The day room has 5 2 8 1 on the wall.
5. **A speaker for a switch line.** `FlagSwitch.doneSpeaker`: the PA line shows "PA:". With no speaker, the subtitles print their fallback label, "VOICE". Round 50 made VOICE the label of the core's voice, so the fallback label now clashes. OPEN for the writer or Bruno: a new fallback label.

The PA line comes from the writer's `T5_pa.md`. All other words on the screens are PROPOSED.

## Proof

| Check | Result |
|---|---|
| `CodeRotationTests` (6) and `InteractionTests` (11, the `Interactor` changed), run `20261108-021926-test-playmode-92813` | 17 of 17 |
| `CodeRotationTests` after the rebuild, run `20261108-022540-test-playmode-95107` | 6 of 6 |
| Bot `code`, run `20261108-022616-play-95454` | pass, 5 of 5 |

My first test run had two faults, both in the test. One compared colours exactly. The other was a dial loop that read the cursor as its own limit. The first bot shots showed no Interact message, because the shot took only the canvases it named. It takes every overlay now.

Shots: `~/MEGA/glorified/renders/game/code_rotation_2026-11-08/`.

## Not yet in the demo

W12 has the card door, W13 the terminal screen, and W14 the code on the wall. The demo still needs four things, about 0.5 day:

1. The PA call 1B in the W9 beat. It raises the rotation flag.
2. `CodeLock` on the W12 door.
3. `Terminal` in W13.
4. The guard's radio line in W5 after the call (`barks.csv`), the second source of the code.
