# 2026-11-06, night: the last hit, the white smoke, and two faults the shots showed

Bruno:

```text
one question: i'm planning so that any hit that would kill the player will first degrade them to 1 hp and then the next one triggers second wind or kills them. reason: this makes impossible for any enemy to wipe out the player in one hit. is this how hp works rn? if not, make it so
yes, the 0.5s window is a good idea
```

```text
change the smoke from green to white to make this more verosimilar
both soldiers and fireman fire the same grenades, although in the soldier's case it is explicitly a knock-out gas; gameplay wise it is the same thing
```

## What changed

| Piece | What it does now |
|---|---|
| The last hit (`Health`) | A hit that would kill her from above 1 HP leaves her at 1 HP, with 0.5 s of invulnerability (`LastHpInvulnerableSeconds`). The next hit takes the second wind or kills her. Only her health has this rule. `Health.Kill()` goes past the rule, for a scripted death |
| The smoke | The gas cloud is white. The ring on the floor is still green |
| X1 | She started inside a car. The layout changed, and a test walks every way (`TheCoda_EveryWayIsOpenOnTheFloor`) |

The answer to your question: before this, no. A hit bigger than her HP killed her at once, unless the second wind was ready.

## Fault 1: a stop of the world threw a falling body into the sky

I saw this in the last shot of the tutorial bot. A dead guard hung in front of the camera. The log showed why.

| Frame | Time | Hips height | Fastest bone |
|---|---|---|---|
| 22 | 159.449 | 0.28 m | 3.4 m/s |
| 23 | 159.449 | 0.29 m | 96.3 m/s |
| Later | 161.96 | 149 m | 47 m/s up, after 70 m/s |

The cause: `GameManager` wrote `Time.fixedDeltaTime = 0` on every stop (the pause, the inventory, a log, a cutscene, the game over). It came from Warspite on 2026-10-20. Unity clamps this to 0.0001 s and runs the rest of the step at that size. A body in contact with the floor then pushes out at its depth over 0.0001 s.

So every body that touched the floor at the moment of a stop could fly. **The line was in every build since step 1**, and the ragdolls since 2026-10-24. The window is short: the bot opened the drug list as the body landed.

The fix: a stop writes only the time scale. Unity does not run the physics at time scale 0. The test `AStopOfTheWorld_WhileABodyLandsOnTheFloor_DoesNotThrowIt` failed first (the step was 0.0001 s). Now the hips stay under 0.35 m.

## Fault 2: the shaft of G16 read as a thin line

The sides of the floor boxes had the floor's colour. From above, the far side looked like more floor. The sides of the pit are dark now.

## Fault 3: a soldier hid in the open

The cover test failed in some runs: a power reached a soldier who counted as hidden. A walk to a spot could end short of it, and he crouched and counted as hidden where he stood. He now walks to the spot first, and only there he crouches and counts as hidden. I did not measure his distance in a failed run, so this cause is my best reading. The test passed in 4 of 4 runs after the change.

## Fault 4: the fire door was behind the lens

In the tutorial's fire door step, the door stood behind the camera and her head filled the frame. That broke your exit rule. A new camera, `Cam_Gym_FireDoor`, shows the door and the lever from the south.

## Fewer tests, and fewer whole runs

Bruno:

```text
these test suites are taking too long imho; each now runs for close to an hour
yes, removal of rows 1 through 4
```

| Change | Result |
|---|---|
| In a round, I run only the test classes of the changed code and the bots of that feature | 3 to 8 minutes, not 50 |
| The whole suite and all the bots run once, before a push or overnight | `CLAUDE.md`, rule 10 |
| The tutorial walkthrough test is gone | The tutorial bot plays the same steps, so it must run before a push |
| `LookTestTests.cs` is gone (22 tests) | The demo's camera checks stay in `DemoTests`. The test of `FixedCamera.shadowDistance` went with it |

The suite now has 394 tests and should take about 37 minutes. I did not run the whole suite after the removals.

## Recorded, not built (your annotations)

- The firemen fire CO2 grenades at the fire, with the same effect as the soldiers' knock-out gas. The writer writes the logs (`03_GAME_DESIGN.md` §29).
- The computer room: a fire she lights to remove an obstacle, halon, the door seals, a pass-out game over. A new room, G17. The writer writes the logs and the signs.

## Proof

| Check | Result |
|---|---|
| Whole PlayMode suite on `1c7d030`, run `20261106-205438-test-playmode-551026` | 416 of 417 |
| The failure: `TheFireteam_Bounds` | Both elements moved in 37 % of the frames, and the limit is 35 %. See below |
| `TutorialTests` and that test, 4 runs after the camera | 9 of 9 each time. Both elements moved in 2 %, 0 %, 11 % and 5 % |
| Bots after the last change | tutorial `20261106-214824`, demo `20261106-215155`: pass, triage clean |
| Cover test, 4 runs, and the cover bot | pass |
| The other bots and EditMode | Last run on `ad77cef`, before the fixes of this note |

**The open failure.** Over the 9 runs since the cover change, the share was 2, 5, 6, 12, 37, 2, 0, 11 and 5 %. So 1 run of 9 went over the limit. In that run the team made 5 bounds, not the usual 9 to 13. I think that one soldier stalls and holds his element in the move, but I did not prove it. I did not move the limit. Before the cover change, the last whole run gave 2 %, so the cover change can be part of the cause.

Shots, checked at full size: `~/MEGA/glorified/renders/game/tutorial_greybox_2026-11-06/` (`tutorial_27`, `28`, `34`), `~/MEGA/glorified/renders/game/demo/coda_2026-11-06/` (`demo_cam_g16_00`).

Nobody played any of it by hand. Manual steps 76 to 78 in `2026-11-03_manual_tests.md`.

## Later the same night: the pistol hold

Bruno:

```text
i also noticed that the scientist with the gun is holding the weapon wrong in some pics, take a look at it
Will these fixes hold when we replace the models? the plan is to replace them with higher poly-count models soon
```

Verdict: confirmed, and wider than the report. Every pistol had the fault, also the guards. A rifle has a real pose since 2026-10-31. A pistol only sat at the wrist in the vendor's arm pose, so the hand stood upright and the forearm stood 56 degrees off the barrel.

`EnemyGun.PistolHold`: at the aim the grip is ahead of the chest, and the forearm lies along the barrel. At rest he holds a low ready. The right hand lies on the grip. A lab worker's left hand cups it from under. A shield guard keeps one hand. The test `ThePistol_LiesInBothHands_TheForearmAlongTheAim` failed first at 56 degrees, and it gives 24 to 29 degrees now.

Proof: `ArmedCivilianTests` 10 of 10 in two runs. `EnemyAiTests`, `EnemyMoraleTests`, `EdgarTests`, `HitReactionTests`, `DummyShooterTests`, `FlankingTests`: 54 of 54, run `20261106-221338-test-playmode-572529`. Bot `holds` in LAB TEST and AI TEST: pass. Shots: `~/MEGA/glorified/renders/game/holds_pistol_2026-11-06/`. `TheCarrier_DropsTheCard` failed once in the first build of the pose by 1.4 cm, and passed in the two runs after. The whole suite did not run on this commit.

Not checked: the knife hold. The T19 rigs have no finger bones, so the fingers stay straight on the grip.

With new models: the numbers follow the arm's length and the bind pose, but this is not tested on another model. New models with finger bones need a finger curl, which does not exist. Run the `holds` bot after the first import.

## Later again: the baked guard (T27) as a rehearsal of the model swap

Bruno:

```text
/home/pc/MEGA/glorified/renders/T27_baked_characters/guard
has the files of a baked character; check if our fixes hold
```

The rehearsal: `T27BakedCharacters.RehearseSwap` makes the guard, the soldier and the lab worker wear `t27_guard` (8,999 triangles, the same 23 bones, no finger bones) in the catalog. Then `BuildEnemyPrefabs` and the real tests run on it. Back: `git checkout` the catalog, then `BuildEnemyPrefabs`.

Verdict: **one fix did not hold.** The knee fix of 2026-10-31 failed: knees went up to 78 degrees past the straight leg. The pistol hand was also turned the wrong way. The old models gave 0 degrees.

Cause: in Unity the bind pose of `t27_guard` faces away from the root's forward. A Humanoid clip hides this in play, but the ragdoll hinges and the hand frames were built on the root's forward. The T27 export script does turn the figure, as our T19 export does, so a later step reverses it. The T27 import sets `bakeAxisConversion` and the T19 import does not. I did not prove that this is the step.

Fix: `HumanoidRagdoll.BodyFront` reads the facing from the thighs and `EnemyGun.FindHandFrame` reads it from the mesh. A model that faces back now works. A model whose bind pose faces sideways does not (not seen so far).

| Check | Baked guard | Old models |
|---|---|---|
| Worst knee past the straight leg, before | 78.6 degrees | 0 |
| Worst knee, after | 0.8 degrees | 0.0 degrees |
| Figures logged as facing away | 9 of 12 kinds | 0 |
| Forearm to the aim | 22 degrees | 25 degrees |
| `RagdollTests`, `ArmedCivilianTests`, `GoreTests`, `EnemyAiTests`, after | 52 of 52 | 52 of 52 |
| Bots `holds` (2 rooms), `reactions`, after | pass | pass |
| The wider set before the fix: those plus `EdgarTests`, `HitReactionTests`, `EnemyMoraleTests`, `FlankingTests`, `CivilianTests`, `SaveGoreTests`, `WardTests` | 113 of 114, the knee test | |
| `CharacterImportTests`, bot `cpr` (camera behind a box in its side shot), before the fix | pass | |

`TheKnees` stopped at its first bad knee, 10.04 degrees, and hid the 78. It now logs the worst knee.

Not tested on the baked mesh: the whole suite, the demo bot, Lucy (`t27_lucy`), the cost of the limb cut in milliseconds, and the wider test set after the fix. The T27 gloves are modelled half closed, so this file needs no finger curl.

Shots: `~/MEGA/glorified/renders/game/t27_guard_rehearsal_2026-11-06/`.

