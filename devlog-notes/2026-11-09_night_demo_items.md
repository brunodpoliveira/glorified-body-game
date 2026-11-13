# 2026-11-09, night: the five demo items

Bruno, before sleep:

```text
i'll go to sleep now so i'll review this in the morning; you can get started with all 5 items and i'll review progress
when i wake up in the moirning
```

The five items: (1) the brain fight in B1, (2) the dark room in G9 and G9b, (3) the audio logs L19 and L20, (4) the
writer's backlog lines, (5) the full check before a push.

## 1. The brain fight in B1

1. B1, THE BRAIN SECTOR, and B2, THE BUILDERS' DOOR, stand east of the coda on their own map floor, THE BRAIN
   (`Editor/Demo/DemoBrain.cs`). B1 is the sector of BRAIN TEST: `BrainTestSetup.DressSector` and `Wire` place it
   from the room's own corner now, so both scenes build the same sector.
2. The U1 Crown beat ends with a cut to B1 (it cut to X1 before, with a placeholder line for B1). The fight starts on
   her first step. Its death opens the north doorway. In B2 the exit lines of cutscene 10 play, then the cut to X1.
3. The eye camera turns up to 100° from its rest (55° before): it hangs on the south wall, and it must look along the
   walls into the corners. The coverage check now turns a camera that follows her toward each point as the director
   does (`CameraCoverage.TurnedTo`): no blind spot in B1.
4. A fault found on the way: the brain's track held into the next scene when its scene went in the fight (a death
   and RETRY, a quit). The music system lives for the whole game. `BrainFight.OnDestroy` gives the music back.

Tests: `DemoTests.TheBrainSector_B1_ItsDeathOpensB2_AndB2CutsToTheCarPark` (new), `U1_TheGrab..._AndSheStandsInTheBrainSector`
(it expected the car park and no HUD: now B1, the fight, and the HUD of the fight with no HP row), the coverage test,
the room count (43), `BrainFightTests.ItsSceneGoing_InTheFight_GivesTheMusicBack` (new). `BrainFightTests` and
`KitTests` 22 of 22. The demo bot passed before the eye's wider turn.

## 2. The dark room in G9 and G9b

`Editor/Demo/DemoDark.cs` wires DEMO_PLAN §10.1 into the demo, as DARK TEST has it:

1. She enters G9: the lights of G9 and G9b go out, the ten faces of the monitor wall go black, and the three units in
   G9b beep. The sector C release waits: "WAITING FOR NET."
2. The units stand on the floor of G9b, not in their order (ACCESS, NET, REC), each with a PC on it
   (`DarkTestSetup.Unit`, now shared). A wrong unit: the fault and the countdown, 5 s.
3. NET, REC, ACCESS: the monitors come back one by one with her face on them (`UpsBank.restoreOwnLook`: a screen
   shows its own material again), then the room lights. The release works.
4. The sources of the order: the hub's units under the counter of W9, with their up-times (NET 11:52:07, REC
   11:51:40, ACCESS 11:51:02), and the IT note on the clerk's monitor in G8.
5. The demo's sun lights only the lit layer now, so the dark room can leave it.

Test: `DemoTests.TheDarkRoom_G9_...` (new). With `DarkRoomTests` and the coverage test: 7 of 7. The dark bot passes.

## 3. The audio logs L19 and L20

1. L19, the incident review: a dictaphone on the counter of W9 (DEMO_PLAN §8, round 44). L20, the calibration note: a
   recorder on a new bench in W11 (round 49). Both play when taken, as the test tape does, and stay in the inventory.
2. The words are the writer's (`game/lines/T14_incident_review.md`, `L20_calibration.md`, PROPOSED), as subtitles
   under REVIEWER and TECHNICIAN, and as the log text in the inventory.
3. The clips are placeholders (`tools/audio_log_placeholders.py`: the voice-like sound of the test tape, timed to the
   lines; no speech tool is on this machine). L19 lasts 51.5 s, L20 15.6 s. The real voices come later.
4. At its end L19 unlocks the feed (the backup of round 44): an audio log raises its flag at its end now
   (`TapePlayer`, `ItemDefinition.readFlag`).

Test: `DemoTests.TheAudioLogs_...` (new): both in their rooms with their clips, the end of L19 unlocks the feed. With
`AudioLogTests`: 7 of 7.

## 4. The writer's backlog lines

The rows of `game/barks.csv` (2026-11-07, PROPOSED) now replace these placeholders in the code, word for word:

| Where | Line |
|---|---|
| The ward sister gives the card (`CarrierTalk.LineGive`) | "Here. Take it. Now go, and you go round the chairs." |
| G10, the first hider's whisper | "Keep your head down. Don't look at her. Don't look." |
| G10, the second hider's whisper | "She can see us. She's looking right at us." |
| The armed lab worker starts the care, drags, does CPR | "Hey. Hey, it's me. Look at me, it's me." / "I don't know how to do this. I'm doing it. I'm doing it." / "Thirty and two. Thirty and two. I did the course." |
| The shock, a colleague dies / a gruesome death: the soldier | "Man down. Man down, hold the line." / "Control, she took him apart. She took him apart." |
| The same: the guard | "Oh no. Oh no, oh no." / "Dispatch, what did she do to him? What did she do?" |
| The same: the armed lab worker | "No. No, he's from my floor. He's from my floor." / "Oh God. It's on the wall. It's all on the wall." |
| A lab worker sees the feed | "That's... Computer, she's feeding. Advise. Please advise." |
| A nurse sees the feed | "Sweetheart, no. No, no. Not like that." |
| A soldier fires the knock-out gas (`DummyShooter.SoldierGasLines`) | the three `soldier_gas_*` rows |

Still PLACEHOLDERS, with no row from the writer yet: the hiders' lines when she finds them, the guard's retreat line,
the fighters' feed lines. The rows for G18 and G19 (round 60) wait for those rooms. The lab worker's retreat line was
the build's already, and the writer kept it. The seven death screens came in with the four items (2026-11-09).

## 5. The full check before a push

The whole PlayMode suite (468 tests, 44 min) and 27 bot runs (65 min). First result: 465 of 468 tests, 25 of 27 bots.
`TheFrame_Profile` is explicit, so the suite skips it. The other failures, each checked:

| Failure | Verdict | Fix |
|---|---|---|
| `DemoTests.TheCull_...`: `Cam_G5_02` saw G14 in play, not in the editor | A FAULT IN THE SCAN. One ray hits the corner where the south wall of G12 meets the east wall of G14, both at 20.19 m. The physics gives the hits in no fixed order, so the editor took one wall and the game took the other | `RoomSight`: at a tie, the wall of the room the ray is in wins, else the lower room id. `Resight` again |
| The ai bot: the lift of `Soldier_A` dropped at once, and seven steps after it failed | CONFIRMED, A REAL FAULT. A bullet on an invulnerable Lucy (a dodge, the second wind, the test rooms) still raised a hit, and a hit on her drops what she holds (2026-11-07). The melee strike already checked it | `EnemyProjectile`, `EnemyRocket`, `EnemyGrenade`: no hit on an invulnerable Lucy. New test `PowersAndBarsTests.Telekinesis_ABulletOnHerInADodge_KeepsTheFloater` |
| The cover bot: `barricade_breaks` | A FAULT IN THE BOT. The soldier hid behind `Barricade_1`, but `Barricade_3` stood on her line and took the blasts. The bot waited for the wrong one | The bot breaks the barricade on her line |
| `ArmedCivilianTests.TheCarrier_DropsTheCard_...`: a limb 0.16 m from the card (the rule: 0.19 m) | A REAL FAULT, small. The card shows 1.2 s after the death, and the body still moves after that | `ItemCarrier.SettleMargin`: the spot keeps 0.1 m more room than the rule |

Also fixed: the demo bot put Lucy on the W9 counter for its shots (since 2026-11-08 at least). `FreePoint` now skips the
top of a counter or a desk.

The second run, after the fixes: `DemoTests`, `PowersAndBarsTests`, `ArmedCivilianTests` and `EnemyAiTests`, 135 of
136. The ai, cover, reactions and civilians bots pass.

`DemoTests.TheFireteam_SheHides_...` failed once in that second run: the pair walked together in 3414 of 9947
frames, and the test allows a quarter. The code under it did not change tonight. Two runs after it passed (160 and 214
frames together). It is flaky: OPEN. The demo bot passes with the new placement.

Shots: `~/MEGA/glorified/renders/game/night_demo_items_2026-11-09/` (B1 from the eye, B2, G9 and G9b dark, G8, W9
with Lucy on the floor, W11, and `w9_before_on_the_counter.png`).
