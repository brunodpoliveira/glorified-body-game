# 2026-11-05: exits in view, the feed by a cutscene, the demo ends by itself, the soldier with a shield

Bruno, after his playthrough, with three screenshots:

```text
no it should not; also what is the deal with this scannable guard? he won't attack and keeps turning its back on me?

also another rule that should be added: although i get good visuals on the room i also need to see the exits; in some rooms the
doors/exits are in a blind spot, like here
also it needs to exit after the demo is complete
also also the unlocking oif the eating/feeding should be a cutscene; i missed the log note so i had to do the entire demo without
healing
```

And: "shield dude also needs to have a soldier variant".

"No it should not" answers my question: a floater left behind at a wall does not drop. No change.

## The fact-check

| Report | Verdict | Evidence |
|---|---|---|
| The guard with a scan does not attack, and turns his back | WORKS AS DESIGNED | `DemoTests.TheGuardWithTheScan_FightsLikeTheOther_AndCowersOnlyByFear`: with no fear he aims at her. At fear 3 he runs and cowers, and his back turns to her wherever she stands (facing dot -1.00, twice). This is the fear rule (design §11.5, 2026-10-24): a guard retreats at 3, a gruesome death he sees adds 2, a feed he sees adds 2. The scan has no part in it: the HUD shows "F scan" because this guard carries the clue C5 |
| Exits in a blind spot | CONFIRMED | 26 of 48 exits were out of view of the camera of the floor in front of them |
| The demo does not exit | CONFIRMED BUT DIFFERENT | It exited only on E, and no hint said so. After 45 s with no key the card still stood |
| The feed stays locked if she misses the log | CONFIRMED, by the code | L9 lay on the floor of W2, and only reading it opened the feed |

## The exit rule

`CameraCoverage.SeesExit`: the doorway is in frame from foot to head height and edge to edge, not edge on (at least 3 % of the frame), and
nothing hides it. It applies to the camera of the floor 0.7 m in front of the doorway. `DemoRoom.BestPose` counts an exit before any floor
point, and a cell that misses an exit splits. `Room.exits` holds the doorways.

| Check | Result |
|---|---|
| Exits out of view | 26 of 48 before, 0 of 48 now |
| Floor points out of frame | 0 of 5020 |
| Cameras | More in some rooms: W1B 3, W2 4, W8 5, W9 3 |

A fault from this change, found in the shots: W1B split down the line she walks, and one camera stood in the bay, with the screen between it
and her. The talk now has its own camera (`Cam_W1B_talk`, on the west wall). The screen is 1.8 m now. The talk test checks that nothing
hides her or him from the camera.

## The feed by a cutscene

Script: `game/cutscenes/02b_the_chart.md` (the writer, PROPOSED). An FMV slot: a dark card with six lines until a clip exists.

1. It plays when the three of W2 are dead (`W2_Chart`), 1.5 s after the last one falls.
2. It plays in W5 if she runs past them (`W5_Chart`, a line wall to wall). The way on goes through W5, so she cannot miss it.
3. It plays once. `ScriptStep.StopIfFlag` stops the first beat if the second one ran.
4. A skip still opens the feed and gives the chart. The chart goes to her items, and it no longer lies on the floor.

## The demo end

The card shows "E  continue" when it has typed all its lines, and it closes by itself after 12 s (PROPOSED). The title follows. Run: 22.7 s
from the card to the title with no key.

## The soldier with a shield

Kind `soldier_shield`: the soldier model, the shield, the pistol, the rank of a soldier. So he flanks and never runs, and a guard with a shield
comes head-on and retreats. He replaces the guard in the second pair of G2 (`Soldier_Pair2_B`). After a vendor import, run `BuildEnemyPrefabs`.

## Proof

| Check | Result |
|---|---|
| `DemoTests`, `ScriptTests`, `SaveTests`, `MapTests`, `WardTests`, `EnemyAiTests`, `RoomSeamTests`, run `20261105-032329-test-playmode-169989` | 83 of 83 |
| `DemoTests` after the talk camera, run `20261105-034055-test-playmode-175449` | 21 of 21 |
| Bot `play Demo Main_Menu demo 900`, run `20261105-034722-play-176369`, with the bulletin steps | pass, triage clean |

Limits: the whole PlayMode suite did not run. Nobody played the new camera cuts by hand. The test for the soldier with a shield checks the rank,
the style, the pistol and the front of the shield, but not a fight.

Shots: `~/MEGA/glorified-body-renders/game/demo/opening_2026-11-05c/`.

## Open for Bruno

1. The CROWN line of cutscene 02b says it in plain words ("FEED ON ONE TO HEAL"). The writer recommends it, and it is built. The options are in
   the script's note.
2. Answered: a guard who cowers needs a clearer sign, and the writer writes it ("yes, the writer'll take care of it"). The game plays the lines when she comes near.

## The cowering figure: the lines and the drop (later, same day)

Bruno: "cowering figure drop its weapon. you can add the lines now".

1. A figure that cowers says one line when she comes within 4 m (`SoldierBrain.CowerLine`), 6 s between lines (PROPOSED). The lines are the
   writer's (`game/barks.csv`, `cower_guard_*`, `cower_labworker_*`): line 1 first, then at random with no repeat until all have played.
2. When it reaches its hiding place it drops its weapon (`EnemyGun.Drop`) and its shield. It keeps them while it runs. The weapon falls with
   physics and stays on the floor, on the Debris layer, so it blocks no one. The animator goes to its unarmed set.
3. A shield guard who cowers has no front that stops a power now.
4. The retreat lines lost their capitals, and "FALL BACK" is gone (the writer): a guard says "That's not a patient. That's not a patient!".

A load brings a living figure back with its weapon, by the rule of 2026-10-26: only the dead are saved.

| Check | Result |
|---|---|
| `DemoTests.AFigureThatCowers_SaysItGaveUp_WhenSheComesNear_OnlyThen` (a guard and a knife lab worker) | pass. The weapons lie at y 0.01 and 0.00, about 0.8 m from them |
| `RagdollTests.TheShieldGuard_DropsGunAndShield_WhenHeCowers`, `DemoTests.TheGuardWithTheScan...`, `ArmedCivilianTests`, run `20261105-130954-test-playmode-210969` | 12 of 12 |

Limits: the whole PlayMode suite and the bot did not run. In the shots the knife lies beside the crouched lab worker, but the guard's camera
does not show his gun: the test measures it. Nobody saw the unarmed crouch up close.

Shots: `~/MEGA/glorified-body-renders/game/demo/cower_2026-11-05/`.

