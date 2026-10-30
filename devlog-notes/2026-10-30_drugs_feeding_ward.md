# 2026-10-30: one-stat drugs, feeding, and the ward (rank 10)

The order from Bruno:

```text
the drugs: they can either fill up mk (one pill does it) or decrease short (another pill does that); hp regen is to
be disabled in all forms (includes the passive regen we have on lucy bare) and the only form to regain hp is by
feeding on fallen enemies; civilians give more hp back as usual;
2 to be built and tested as well
```

Numbers and names are PROPOSED.

## 1. The drugs: each drug does one thing

| Drug (code id) | On screen | It does |
|---|---|---|
| `Nalcon` | MK CAPSULE | MK full. Nothing else. |
| `Red` | SHORT CAPSULE | Takes half the SHORT bar away (`PsychicPowers.ShortCapsuleShare`). A short that runs ends. |
| `DFelon` | SECOND WIND PILL | Gives the second wind back (`Health.RearmSecondWind`): the next lethal hit leaves her at 1 HP. |

- No drug gives HP.
- A drug with nothing to do is refused and stays in the inventory: MK full, an empty SHORT bar, a second wind that is ready. The inventory says why ("MK is full.").
- A drug she does not carry has no stand-in. Before, any drug could replace another.
- The drug key opens the drug choice on the drug she needs most (`PsychicPowers.MostNeededDrug`). In a short, or with the bar half full, that is the SHORT capsule. Else it is the MK capsule, then the second wind pill.
- The code ids stay, because the saves use them. The names on screen are PLACEHOLDERS (design §10.5).

## 2. No HP regen, in any form

- The outfits have no HP regen. The field `hpRegenPerSecond` is gone.
- A level raises the maximum HP only. Before, it also gave the new HP points.
- The MENDING module (EASIER) now doubles the HP of a feed (`DifficultyManager.FeedScale`). Before, it doubled the HP regen.
- The debug key F7 and the bots still refill her (`PsychicStats.RefillAll`, debug only).

## 3. Feeding: her only way to get HP back

| Part | Behaviour (numbers PROPOSED) |
|---|---|
| Who | Every dead enemy and civilian (`FeedableBody`, put on every figure by the factory). The training dummies vanish at death, so they give nothing. |
| How | The Interact key on the body asks "Feed on the body?". At full HP there is no question, only "She is not hurt.". |
| The feed | She crouches at the body (the vendor clip `Crouch_Idle`, the animator's `Feeding`) and stands still. The HP flows into her over 2.5 s (`Feeding`). |
| How much | A civilian 40, an armed civilian 30, the power armour 30, a soldier or a guard 20. MENDING doubles it. This follows the exp: civilians give most. |
| Once | Each body gives its HP once. A hit on her stops the feed: she keeps what she took, and the body keeps the rest for a second feed. The feed also stops at her full HP. |
| Care | No carer drags a body that she feeds on (`CorpseCare.MarkTended`). |
| Saves | The HP taken from each body is saved with the body (`BodyState.feedTaken`). |

## 4. Rank 10: a figure that stays, a scan on a person, a fate

| Part | Behaviour |
|---|---|
| The patient (`patient` kind, `PatientBrain`) | She sits up in her bed and never walks, runs, begs or tends a body. When Lucy comes within 2.5 m she says her line once (PLACEHOLDER: "Is mine done? Are they done with mine yet?"). A death she sees makes her shake hard in the bed. A kill drops her as a ragdoll. |
| The scan on a person | `EnemySpawn.scanImage` / `scanClip` / `scanCaption` put a Scannable on the figure. It reads the chest bone, so it follows the body. |
| The fate (`FateFlag`) | `EnemySpawn.fate` ("marta") gives three story flags: `marta_killed` at her death, `marta_spared` when Lucy leaves the room while Marta lives, `marta_scanned` at the scan. The saves keep them. |
| The game remembers | In the room a FlagGate shows a red mark on the wall when `marta_killed` is up. The writer decides the real consequences later. |
| WARD TEST room | `WardTestSetup.BuildWardTest`, the WARD TEST button: the hall of the KIT TEST room, the patient's bed, Marta (a nurse with the scan and the fate), a lab worker and a guard, the mark, the door west to TRAVEL B. |

## 5. His answers, and the second part (same day)

```text
1: they are fine, i'll take a look once i do a manual pass
2: we'll do a crouch + sound + blood effects for now. we can refine it later
3: they should react to it; yes; have placeholder lines, the writer will refine them later
4; later, anootate it
```

| Part | Behaviour (numbers PROPOSED) |
|---|---|
| Blood | During a feed, blood sprays from the chest of the body every 0.6 s, with a wet sound (`Feeding.Bleed`). The gore driver plays the spray without the hit bus, so no other system takes it as a hit. The Gore option applies (Off: no spray). |
| Witnesses | A figure that sees a feed (the sight rule of a death) takes it as a gruesome death: +2 fear, then a shock or a retreat, and a line (`EnemyMorale.SeeFeed`). The lines are PLACEHOLDERS for the writer: the fighters "SHE'S FEEDING ON HIM!", the civilians "Oh God. She's eating him.". A civilian runs. |
| Tutorial | A new step "feed" after the capsule. A dead lab worker lies in the door strip, south of the broken barricade, where the west camera sees it. The step starts with half her HP. Without the vendor prefabs (a clean clone) the step ends by itself. The tutorial has 23 steps now. |
| The armour knee | FOR LATER (his ruling): a comment at `RagdollTests.TheKnees_BendOnlyLikeKnees_InEveryDeath`, and the Proof section. |

Proof of the second part:

| Check | Run | Result |
|---|---|---|
| EditMode | `runs/20261030-021024-test-editmode-638721` | 56/56 |
| PlayMode, every suite | `runs/20261030-021052-test-playmode-639086` | 273/273 (the armour knee passed this time) |
| Bots: structural, look, tutorial, ai, cover, civilians, reactions, lab, ward | the same chain | 9/9 PASS, triage CLEAN |
| Ward bot, record mode (the blood) | `runs/20261030-024204-record-647725` | 5/5 steps |
| After two tutorial drive fixes: TutorialTests, the tutorial bot, its record | `runs/20261030-031355-record-653205` and the runs before it | 9/9, PASS, 25/25 steps |

Two faults of the tutorial drive, found in the record runs and fixed:

- The walk north at x -1.2 brushed the bar bases (x -1.05). In a slow record run Lucy slid into the lane between the bars
  and never reached the capsule. The waypoints are at x -1.6 now.
- The first feed point (1.7, 11.7) put the feed under the gym camera. The point is (2.9, 11.4) now.

The record limit for the tutorial is 900 s now (23 steps; the 500 s run ended at step 17).

Clips: `~/MEGA/glorified-body-renders/game/ward/` (the feed with blood: `clips/ward_04_feed.mp4`, `shots/ward_04_feed_sheet.png`)
and `~/MEGA/glorified-body-renders/game/tutorial/2026-10-30/` (`tut_14_capsule`, `tut_15_feed`, `tut_22_short`).

## 6. The tutorial

- The capsule step lowers MK to 30 first, so the MK capsule has work. Its text says that MK fills.
- The short step gives a SHORT capsule. Its text names it.

## Proof

| Check | Run | Result |
|---|---|---|
| Targeted PlayMode: Ward, PowersAndBars, Tutorial, Experience, Difficulty | `runs/20261030-003556-test-playmode-613477` | 62/62 |
| EditMode | `runs/20261030-004915-test-editmode-617532` | 56/56 |
| PlayMode, every suite (final) | `runs/20261030-012508-test-playmode-628251` | 270/272: the two failures below |
| PlayMode after the fixes: InteractionTests, PowersAndBarsTests | `runs/20261030-014929-test-playmode-633521` and the run before it | 10/10, 34/34 |
| Bots: structural, look, tutorial, ai, cover, civilians, reactions, lab, ward | the chain after the first full run, the structural bot again in `runs/20261030-012410-play-627815` | 9/9 PASS, triage CLEAN |
| Ward bot, record mode | `runs/20261030-004711-record-616708` | 5/5 steps |

New tests:

- `WardTests` (9): the markers, the patient stays and speaks once, the scan reads Marta, Marta's death and the mark through a save, leaving spares her, the feed by rank, a hit stops the feed, no feed at full HP, the feed through a save.
- `PowersAndBarsTests`: `EachDrug_DoesOneThing_AndADrugWithNothingToDo_IsKept`, `NoHp_ComesBack_ByItself_InAnyOutfit`.
- Changed to the new rules: the second wind test (only the pill rearms it), the short test (the SHORT capsule ends it and gives no HP), the full-bar test (half the bar), the level test (no HP), the MENDING test (the feed), two tutorial tests.

The first full run found two places with the old drug rule: the inventory test and the short of the structural bot.
Both used a drug that has nothing to do now. Both use the SHORT capsule now. The final full run found two more:

- `FullShort_Holds_UntilAShortOrADrug` read 49.99 for 50. The part-full bar drains in the frames of the press. The
  tolerance is 0.2 now (34/34 after).
- `RagdollTests.TheKnees_BendOnlyLikeKnees_InEveryDeath` FAILS on and off. The 220 kg power armour bends a knee 25 to
  58 degrees backward in its fire death. It failed in 3 of the last 6 runs. Today's work does not touch the ragdoll.
  The test passed in the first full run of today and on 10-29. NOT FIXED: see Open.

Measured: the lab worker gave 40.0 HP in one feed. A hit after 1.0 s stopped a feed at 16.0 HP, and the body kept 24.0.

Shots and clips for Bruno: `~/MEGA/glorified-body-renders/game/ward/`

| File | It shows |
|---|---|
| `shots/ward_01_patient_bed.png`, `clips/ward_01_patient.mp4` | the patient sits up in her bed, Lucy comes near, the line |
| `shots/ward_02_scan_marta.png` | the scan of Marta (the placeholder picture and caption) |
| `shots/ward_02_marta_mark.png`, `clips/ward_03_marta.mp4` | Marta dies, and the mark shows on the wall |
| `shots/ward_03_feed.png`, `clips/ward_04_feed.mp4` | Lucy crouches at a dead lab worker: HP 30 to 70 |

## Open for Bruno

1. The numbers: the HP of each rank, the feed time of 2.5 s, half the SHORT bar for the SHORT capsule.
2. The names on screen: SHORT CAPSULE and SECOND WIND PILL are placeholders.
3. The look of the feed: now only a crouch and a sound. Blood, a sound of its own, or something else? What is "feeding" in the story?
4. Should the witnesses react to a feed (fear, a scream)? Now they do not.
5. A feed step in the tutorial needs a body. The training dummies vanish at death. Should the gym get one body?
6. The armour's knee (the fault of 10-27, Bruno: "the lower leg bends inwards") still comes back in some fire deaths.
   Fix it next, or later? The power armour is not in the demo.
