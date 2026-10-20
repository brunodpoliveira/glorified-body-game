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

RUNS (final, the short change): `runs/20261020-203940-exec-321139` (scene rebuild, CLEAN),
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
