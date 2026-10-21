# Devlog notes, adjustments after step 3 (2026-10-20)

Two changes Bruno asked for after he played the training room.

## 1. The dodge is directional

Before: Space dashed forward with W held, otherwise a step back. Now (`TankController`):

- Space plus a held direction key dashes that way. W forward, S back, A left, D right. No key: a step back.
- A double tap on a direction key (two presses inside 0.3 s) is the same dodge, no Space needed.
- The step back stays the horror default. The side dodges move her along her right axis, so she keeps her facing.
- Telemetry: the `dodge` event now says `forward`, `back`, `left` or `right`.

Tuning (PROPOSED): double tap window 0.3 s. One tap is still a turn or a step, never a dodge.

## 2. The short was out of reach by hand (works as designed)

Fact check: the short starts when the SHORT bar is full (100) and a power is released. SHORT rises by 25 per
kill and by 0.6 per HP of damage taken, and the nude outfit drains it by 1 per second. The room has two dummies,
so by hand the bar tops out near 50. Not a bug: the room is too small for it. The debug keys make the test possible:

| Key | Does |
|---|---|
| F5 | Fills the SHORT bar. The next release (hold and let go of the attack button) starts a short. |
| F6 | Starts the short at once. |
| F7 | Refills HP and nalcon, gives two drugs of each type. |

`DebugCheatKeys` sits next to `DebugKillKey` (F4) on `[GameplaySystems]`. The F-keys are reserved and never
rebound. The keys go away when the game ships.

## 3. A full SHORT bar holds (rule from Bruno, 2026-10-20)

A full SHORT bar does not drain. Only two things empty it: the short itself (at its end) and a drug (any drug,
short or not). A part-full bar drains at the outfit rate (nude 1 per second, guard 0). The code already did
this (`PsychicStats.Update` skips the decay at full). The rule is now pinned by the test
`FullShort_Holds_UntilAShortOrADrug` and stated in the code comment.

## 4. The short: she keeps her legs, it lasts longer (Bruno, 2026-10-20)

- She can walk, run and dodge during a short. No powers (a release during a short does nothing). Before, the
  short locked her in place. The design text says "out of her control" about the shocks, not about her legs.
- Duration 4 s to 8 s (PROPOSED: twice). The self drain spreads over the 8 s, so it is half as fast per second.
  The total stays about half her HP. Each shock does the same damage to the others (12 every 0.4 s within 6 m).
  Over 8 s that is twice the damage to the enemies around her. If the total should stay as before, the per shock
  damage goes to 6. Bruno decides.
- A dodge during a short gives her the dodge frames. A shock that lands in them does not vanish: it waits for
  the next shock, so the total HP cost stays the same.
- The test `FullShort_TurnsTheRelease_IntoAShort_AndADrugEndsIt` now walks her 1 s during the short.
- A trap found by the bot telemetry: the first run still showed 5 HP per shock, the 4 s value. The scene builder
  opens the existing scene and keeps a present component with its old serialized numbers, so a new C# default
  never lands. `MenuLoopSetup` now removes and re-adds `ShortController` on every build, and the test
  `Short_EndsByItself_AtAboutHalfHp` pins the 8 s.

## 5. The telegraph circle moves to the HUD (Bruno, 2026-10-20)

The circle is her heads-up to dodge or to put up a defence before the shot. A fixed camera may not show the
enemy, so a circle over the enemy's head can be outside the frame. Now (`PlayerHud`, PROPOSED):

- The circle is on the HUD, over the locked aim point. That point is her chest, so it is always in the frame.
  It shrinks from 280 to 60 px over the 1 s telegraph, turns once per second, and goes from orange to red.
  One ring per shooter in its telegraph (up to four).
- An arrow at the screen edge points at a shooter outside the frame while it aims (orange) or telegraphs (red).
  So she knows which side the shot comes from before she picks the dodge direction.
- The laser line stays in the world. The world ring over the enemy's head is gone (`DummyShooter`).
- Tests: `Shooter_Aims_Telegraphs_Fires_AndHitsLucy` checks the HUD ring. New
  `Telegraph_ShowsAnEdgeArrow_WhenTheShooterIsOutsideTheFrame` puts the camera 3 m above her, looking down, so
  the dummy is outside the frame: the arrow shows while it aims, the ring and the arrow show during the telegraph,
  both go after the shot. The bot saves `hud_telegraph.png`.

## 6. Telekinesis: the second press throws the held enemy (Bruno, 2026-10-20)

- With an enemy in the air (the rise and the hold), the next attack press throws it. No charge, no nalcon: the
  lift was paid. The enemy flies at 14 m/s into the nearest other living enemy in range (12 m). On contact both
  take the telekinesis damage (40 at a full lift), and the hit enemy loses its aim (0.8 s). The thrown one drops
  to the floor where it hit. With nobody in range it flies 8 m straight ahead of her, or to a wall, and drops with
  the slam damage. Numbers PROPOSED.
- The hold grew from 0.8 s to 1.5 s: that is the window for the second press. No press: the slam, as before.
- `TelekinesisLift.Launch`, `PsychicPowers.TryLaunch`, the press that throws does not start a charge.
- Test: `Telekinesis_SecondPress_ThrowsTheHeldEnemy_IntoTheOther`.

## 7. FIRE: only a full charge ignites (Bruno, 2026-10-20)

- Before, any charge above 20 percent lit the burn, scaled by the charge. Now a full charge (the circle closed)
  lights the burn (6 per second for 4 s) and does a 5 hit. A part charge is a heat hit: 10 times the charge, no
  burn (PROPOSED, see section 8).
- Placeholder look for the burn, so it is visible by eye: the victim turns orange and small flames rise off it
  for the 4 s (`BurningStatus`). The final effect comes with the art.
- Test: `Fire_Ignites_OnlyAtFullCharge_WithAVisibleBurn`.

## 8. Bruno's decisions (2026-10-20, evening) and what they changed

1. A part charge of FIRE = option (c), the flash: a heat hit (10 times the charge) and the shooter loses its
   aim for 1 s times the charge (`staggerSeconds` on the FIRE asset). No burn. Test: `Fire_PartCharge_FlashesTheShooter`.
   The two other options stay in the record: (a) heat only, (b) heat that adds up to a burn.
2. The defensive telekinesis: (c) first, then all three. Built now, (c) the bullet catch: with TELEKINESIS
   selected and no enemy in the air, the attack press catches a bullet within 2.5 m of her chest. It stops,
   turns blue and flies back at its shooter at 1.5 times the speed with twice the damage (30). The shooter takes
   it and loses its aim. Costs 10 nalcon. A timing move: the telegraph says when the bullet comes. An early
   press counts for 0.2 s (a press buffer), so a press just before the bullet arrives still catches it. Numbers
   PROPOSED. Test: `Telekinesis_CatchesABullet_AndSendsItBack`. The first batch run of it failed: the test
   tapped RMB every frame, and the virtual pad reads that as one long press, so the one press came too early.
   The buffer and a release between the taps fixed it.
   Later: (b) grab and throw props with the Warspite catch-and-throw port, when the look test rooms get props.
   (a) the floor slab shield: Bruno's idea, a "defensive full charge". Needs rooms with loose floor.
4. LMB attacks, RMB defends (Bruno: "more intuitive", and the auto-lock makes "no target" rare). A new action
   `Defend` on Mouse1, in the remap list (14 rows now). TELEKINESIS: RMB tap = the bullet catch. RMB hold = the
   floor shield later (the defensive full charge). LMB with an enemy in the air = the throw, as before. BLAST
   and FIRE have no defensive form yet (OPEN).
   The same scene trap again: `InputManager` keeps its binding list serialized in `Main_Menu.unity`, so the new
   action never reached the remap list (the bot saw 13 rows). `InputManager.Awake` now appends every code
   default the serialized list lacks.
3. The held enemy turns with her (Bruno: "it would be fun"). While it hangs it keeps its offset from her, turned
   by her yaw change: she turns with A and D and the enemy swings around her. The throw aims by her facing: the
   nearest living enemy inside 25 degrees of her forward, else the nearest other enemy in range, else straight
   ahead. The slam lands where it hangs. Test: `Telekinesis_HeldEnemy_TurnsWithHer`.

## 9. The bar warnings (Bruno, 2026-10-20)

- Low HP (25 percent or under), low NALCON (25 percent or under), high SHORT (75 percent or over). Thresholds
  PROPOSED.
- Each warning sits right of its bar: an icon shape (a heart, a drop, a bolt), an exclamation mark and a word
  ("! LOW", "! LOW", "! HIGH"). The icon stays, the text blinks. Shape plus word, so a colorblind player reads
  it without the colour.
- One series of three beeps when a warning first triggers (Bruno: like DOOM 2016), 0.15 s apart, a short sine
  with the bar's own pitch (880 Hz for HP, 620 Hz for nalcon, 1180 Hz for SHORT, placeholder). No repeat while
  it stays on. It beeps again only after it clears and triggers again. Two triggers at once queue up. Volume
  follows the master and SFX settings.
- Test: `Hud_Warns_LowHp_LowNalcon_HighShort_WithIconsAndBeeps`. The bot saves `hud_warnings.png`.

## 10. The second wind (Bruno, 2026-10-20)

- The first hit that would kill her leaves her at 1 HP and invulnerable for 2.5 s. Once. The HUD flashes
  "SECOND WIND". After that a lethal hit kills her. A drug refills her but does not give the second wind back.
  The second wind pill (a rare item, later) calls `Health.RearmSecondWind`.
- The code came from Warspite (`Health`): it was off for Lucy and the drug re-armed it. Now on, 2.5 s, and
  `ReviveToFull` leaves it used. The death tests switch it off first, they test the death flow.
- Test: `SecondWind_LeavesHerAt1Hp_Invulnerable_Once_AndADrugDoesNotRearmIt`.

## How to test the drug by hand

1. Start the training room. Press F7: two drugs of each type, HP and nalcon full.
2. Press F6: the short starts. The SHORT text blinks, the shocks hurt her, she can walk.
3. Press Q: the short ends at once, HP and nalcon refill, the SHORT bar is empty, one drug is gone.
4. The slow way: F5 fills the SHORT bar. Hold LMB on a target and let go: the release starts the short. Q ends it.
5. Check the other way: F6, then wait 8 s without Q. The short ends by itself at about half her HP.

## Proof

- Four new PlayMode tests. Three in `TankControlTests`: Space plus TurnRight dashes right, a double tap on
  Forward dashes forward, one tap does not dodge. One in `PowersAndBarsTests`: the debug keys sit on
  `[GameplaySystems]`, F6 starts the short, F5 fills the bar, F7 refills. Plus the full-SHORT rule test from
  section 3. PlayMode total: 35.
- The scenes were rebuilt from code (`MenuLoopSetup.BuildMenuLoopScenes`), so `TrainingRoom.unity` carries the
  new component.
- The how-to-play panel got three rows (the two dodge forms, the debug keys) and grew from 760 to 900 px, so the
  rows fit. The bot now shows the panel and saves `how_to_play_ui.png` (step `how_to_play_ui`, bot total: 21).
- Found by that shot, an older bug: the PREMISE text had a layout height of 0 and overlapped the CONTROLS header
  and the first row. Fixed: the text supplies its own height (`LayoutElement.preferredHeight = -1`).
- Run folders: see the `RUNS` line at the end.

## Files

`Assets/_Game/Scripts/Player/TankController.cs`, `Assets/_Game/Scripts/Debug/DebugCheatKeys.cs` (new),
`Assets/_Game/Scripts/Editor/MenuLoopSetup.cs`, `Assets/_Game/Scripts/UI/HowToPlayUI.cs`,
`Assets/_Game/Tests/PlayMode/TankControlTests.cs`, `README.md`.

RUNS (final, sections 6 to 10): `runs/20261020-220238-exec-348564` (scene and asset rebuild, CLEAN),
`runs/20261020-222534-test-playmode-355360` (43/43, CLEAN), `runs/20261020-222832-play-355359` (bot 22/22,
CLEAN, `hud_warnings.png` checked at full size: the heart, the drop and the bolt with their words). Earlier
runs of the same code: `runs/20261020-221325-test-playmode-350980` (42/43, the catch test tapped every frame),
`runs/20261020-222035-play-352527` (21/22, the remap list had 13 rows), `runs/20261020-222204-test-playmode-354288`
(42/43, the nalcon tolerance).
RUNS (the HUD telegraph): `runs/20261020-204702-test-playmode-323984` (36/36, CLEAN),
`runs/20261020-204936-play-323983` (bot 21/21, CLEAN, `hud_telegraph.png` checked at full size: the ring over her,
the laser from the shooter).
RUNS (the short change): `runs/20261020-203940-exec-321139` (scene rebuild, CLEAN),
`runs/20261020-204017-test-playmode-321674` (35/35, CLEAN), `runs/20261020-204224-play-321138` (bot 21/21,
CLEAN, telemetry 2.5 HP per shock, 87 to 77 over four shocks).
RUNS (the SHORT rule): `runs/20261020-202445-test-playmode-316605` (35/35 with the full-SHORT rule test, CLEAN),
`runs/20261020-195148-test-playmode-308964` (34/34, CLEAN), `runs/20261020-195347-play-308963`
(bot 21/21, CLEAN, the how-to-play shot checked at full size). Scene rebuild: `runs/20261020-194154-exec-305209`
(CLEAN). Earlier passes: `runs/20261020-193625-compile-303047`, `runs/20261020-193725-test-playmode-303799`
(33/33), `runs/20261020-193914-play-303046` (20/20), `runs/20261020-194238-test-playmode-305831` (34/34),
`runs/20261020-194420-play-305208` (20/20), `runs/20261020-194747-test-playmode-307375` (34/34),
`runs/20261020-194946-play-307374` (21/21, the shot that showed the premise overlap).
SHOTS: `~/glorified-body-renders/game/adjustments1/` (the 14 bot shots, `how_to_play_ui.png` is the new one).
