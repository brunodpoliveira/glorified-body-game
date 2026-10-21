# Devlog notes, adjustments after Bruno's second play (2026-10-20, night)

Bruno played the manual list of 19 rows and sent back five changes, two blocked rows and two "for later" items.
This note has the fact check of the blocked rows, the changes, and the open decisions.

## 1. Row 6: no camera angle hid the dummy (CONFIRMED, a scene gap)

The manual row: walk to the far corner until the camera cuts, so the dummy leaves the frame, and watch the edge
arrow. Bruno: no camera angle cuts the dummy target. The check (a projection of the two dummies through the three
fixed cameras, 16:9 and 4:3): all three cameras look at the room centre, and both dummies sit inside every frame
at 16:9 (horizontal position 0.19 to 0.97). The batch test of the arrow used its own camera above her, so the
tests never saw the gap.

The fix is in the scene builder. `Cam_SouthWest` now sits in the south-east corner (4.6, 2.8, -4.6), looks west
along the south wall at (-3.0, 0.9, -3.4), field of view 50. From its zone (x and z from -5 to -1) Lucy is in
the frame at every point, and both dummies are outside the frame at 16:9 and at 4:3. Dummy_A is 7.8 m from the
zone centre, inside its 10 m range, so it aims at her there.

- Test: `Telegraph_EdgeArrow_ShowsFromTheRoomsOwnSouthWestCamera` (DummyShooterTests). She stands in the zone,
  the room's own camera hides the shooter, it aims, the arrow shows, then the circle.
- Bot step `edge_arrow` (after `camera_cut`, in the south-west zone): the shooter is outside the frame, the arrow
  shows, `hud_edge_arrow.png`. Bot total: 23 steps.

## 2. Row 15: nalcon under 25 was out of reach by hand (CONFIRMED by the numbers)

The manual row: fire powers until nalcon is under 25. Bruno: both dummies are too fragile. The numbers: a dummy has
50 HP, a full BLAST does 35 for 20 nalcon, and a release with no target costs nothing. Four blasts kill both dummies
for 80 nalcon in about 8 s, and the nude outfit gave back 6 per second in that time. The bar never went under
about 68.

Bruno's fix: debug keys. F8 takes 25 nalcon, F9 gives 25 (`DebugCheatKeys`, both clamp to 0 and 100). The dummies
stay at 50 HP. The passive recharge also went down (section 4).

## 3. The short starts on the press (Bruno)

Before, a full SHORT bar turned the next release into a short, and the fizzle check ran first, so a tap with a
full bar fizzled. Now (`PsychicPowers`): with a full bar, the attack press (LMB) or the defend press (RMB)
starts the short at once. That press does nothing else: no charge, no throw, no catch. A release with a full bar
still starts it (the bar can fill during a charge from damage taken), and that check now runs before the fizzle
check.

- Tests: `FullShort_StartsOnTheAttackPress_SlowWalk_AndADrugEndsIt`, `FullShort_StartsOnTheDefendPress_Too`.

## 4. The short: 16 s, a slow walk, no run, no dodge (Bruno)

- Duration 8 s to 16 s. The self drain spreads over the 16 s (1.25 HP per shock on 100 HP), the total stays
  about half her HP. Each shock does the same 12 to the others. Over 16 s that is 40 shocks, 480 potential
  damage to an enemy that stays within 6 m. If the total should stay as before, the per shock damage goes to 6.
  Bruno decides.
- `TankController.SlowWalk`: the walk and the back step at 0.6 of the normal speed (PROPOSED), no run, no dodge.
  `ShortController` switches it on at the start of the short and off at the end (also when a drug ends it).
  Bruno said "only walk slowly", so the dodge is off during the short too. One condition flips that back.
- The scene builder removes and re-adds `ShortController` on each build, so the 16 s lands in the scene. The
  test `Short_EndsByItself_AtAboutHalfHp` pins the 16 s.
- Bot step `short_and_drug`: a tap starts the short, the slow walk is on during it and off after the drug.

## 5. The passive nalcon recharge: 6 to 2 per second (Bruno: too quick)

The nude outfit recharges 2 nalcon per second now (`outfit_nude`, PROPOSED): a full BLAST costs 20, so the bar
regains one blast in 10 s and a full bar in 50 s. The guard outfit stays at 0. The HP recharge stays at 1 per
second, Bruno said nothing about it. Test: `Nalcon_Recharges_Nude_NotGuard` pins the 2 per second.

## 6. Open decisions for Bruno

1. Drugs, one stat each (Bruno: "tbc"). The refill of HP and nalcon plus the end of the short is the canon rule
   of `03_GAME_DESIGN.md` §2.8, not a debug shortcut. Bruno's new rule: a drug recuperates one stat, health, psi
   (nalcon) or less SHORT, never two at once. The three drug types are the Galerians power drugs (Nalcon, Red,
   D-Felon), so the map from a drug to a stat is his call. Proposal: three drugs by stat, HEALTH, NALCON and a
   SHORT-down drug (Delmetor in Galerians), and the power-drug names go. About one hour of work once decided.
   F7 keeps the full refill for the debug checks.
2. Difficulty in percentages (Bruno, for later): easy 100 percent, normal 150 percent, hard 200 percent as the
   presets, and the player can customise the difficulty. The present presets are the shooter slots (one, two,
   all). The percent is a scale on the enemy numbers (damage, HP, maybe the telegraph time), with the custom
   panel on top of the modifiers panel. Not started.
3. The HP recharge of the nude outfit (1 per second, a full bar in 100 s): too quick as well, or fine?
4. The dummies stay at 50 HP. Tougher dummies (or a dummy that stands back up) are one line each if wanted.

## The manual rows that changed

| # | Do | Expect |
|---|---|---|
| 6 | Walk to the south-west corner until the camera cuts. Wait for the dummy's aim. | The new camera looks along the south wall, both dummies are out of the frame. An orange arrow at the screen edge points at the dummy while it aims, red during the telegraph. The ring shows over her. |
| 12 | Press F7, F6, then Q. | F6 starts the short, you walk slowly, Shift does nothing, Space does nothing. Q ends it at once, bars refill, one drug gone, the normal walk is back. |
| 13 | Press F5, then press LMB (or RMB). | The short starts the moment you press. No circle. |
| 15 | Press F8 four times. | A drop icon with "! LOW" at 25. Three beeps once. F9 gives 25 back. |
| 20 | Press F6 and wait 16 s without Q. | The short ends by itself at about half her HP. |

## Proof

- PlayMode tests: 45 (43 plus the room camera arrow test and the defend press test, and the full-SHORT test was
  rewritten for the press and the slow walk). EditMode: 27 cases. The bot: 23 steps.
- The offline type check (Unity's Roslyn over `Assets/_Game/Scripts` and `Tests`, the gore driver left out) ran
  clean before the batch chain.
- Run folders: see the `RUNS` line at the end.

## Files

`Assets/_Game/Scripts/Editor/MenuLoopSetup.cs` (the camera), `Assets/_Game/Scripts/Player/PsychicPowers.cs`,
`Assets/_Game/Scripts/Player/ShortController.cs`, `Assets/_Game/Scripts/Player/TankController.cs`,
`Assets/_Game/Scripts/Player/OutfitProfile.cs`, `Assets/_Game/Scripts/Editor/CreatePowerAssets.cs`,
`Assets/_Game/Scripts/Debug/DebugCheatKeys.cs`, `Assets/_Game/Scripts/UI/HowToPlayUI.cs`,
`Assets/_Game/Scripts/Automation/StructuralPlaythrough.cs`, `Assets/_Game/Tests/PlayMode/PowersAndBarsTests.cs`,
`Assets/_Game/Tests/PlayMode/DummyShooterTests.cs`, `README.md`, `CLAUDE.md`.

RUNS: `runs/20261020-235543-exec-383660` (scene and asset rebuild, CLEAN: the scene carries the camera at
(4.6, 2.8, -4.6), the 16 s short and the 2 per second recharge), `runs/20261020-235628-test-playmode-385373`
(45/45, CLEAN), `runs/20261020-235930-play-383659` (bot 23/23, CLEAN, `edge_arrow`: hidden=True arrows=1,
`short_and_drug`: slow walk on during the short and off after the drug).
SHOTS: `~/glorified-body-renders/game/adjustments2/` (17 bot shots, `hud_edge_arrow.png` and `cam_southwest.png`
are the new views, `how_to_play_ui.png` has the F8 / F9 row).
