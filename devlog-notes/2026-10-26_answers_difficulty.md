# 2026-10-26, day: Bruno's five answers, the new difficulty, no "nalcon" on screen

Bruno's answers to the five open points of the night (`2026-10-26_seams_part2.md`):

```text
1: yes
2: drop at once
3: full hp and calm
4: yes
5: we'll rebuild it next: difficulty has 3 presets: 100,150,200% (not easy, normal or hard); or the player can
customize it by activating/deactiving modules in two sections: harder, easier;
also, some mentions of nalcon are still present; to be replaced as well
all tests passed
```

For each answer that changes the game: a test that failed first (Bug Fact-Check: CONFIRMED), then the change.

## 1. The three test rooms go into git

AI TEST, COVER TEST and CIVILIAN TEST left `.gitignore` with their NavMesh files. Their enemies are spawn markers
(`2026-10-26_room_kit.md`), so the scenes hold no vendor data. The vendor guard before a commit no longer names them.

## 2. A short drops the floater at once

`ShortController.Begin` releases the enemy that she holds in the air (`TelekinesisLift.Release`, no damage, the same
drop as the hit of another enemy). The world freezes for 2.5 s at the start of the short, so the body falls when the
world runs again.

Test: `PowersAndBarsTests.Telekinesis_TheShortsStart_DropsTheFloater_AtOnce`. Before the change: "she lets go at the
start of the short. Expected: null. But was: Dummy_B".

## 3. A hurt soldier after a load or a return: full HP and calm

The game did this already: a save keeps only the dead, and a load or a return builds the room again. Two kit tests now
hold the rule: a hurt and alerted guard is at full HP and calm after a load, and after a trip to TRAVEL B and back.
Both passed before any change.

## 4. A log or a scan stops the world

A new game state, `Reading`, as the inventory: the time stops, the world sounds pause, the music goes down, and only
Interact, Back and Scan pass. The page of a log (after the pickup) and the picture or the clip of a scan use it. The
clip plays on real time, and its sound passes the pause. The short's frozen start waits for it, as for the pause.

Tests: three checks in `InteractionTests` (the page, the picture, the clip). Before the change: "the page stops the
world. Expected: 0. But was: 1" (the same for the picture and the clip).

## 5. The difficulty, rebuilt

The total is the base 100 % plus the percent of each active module. A preset is a set of modules. A set that matches
no preset is CUSTOM. A new game starts at 150 % (PROPOSED: it is the old NORMAL, the setting of all your tests).

| Preset | Modules | What it means |
|---|---|---|
| 100 % | none (the base) | One enemy fires at a time. 1.5 s from the laser to the attack. 2 s rest. |
| 150 % | CROSSFIRE +25, QUICK TRIGGER +25 | Two enemies fire at a time. 1.0 s from the laser to the attack. |
| 200 % | FIRING SQUAD +50, HAIR TRIGGER +35, HARD HITTERS +15 | Every enemy fires. 0.8 s. Enemy attacks do 1.5x damage. |

The modules (numbers PROPOSED). Modules of one kind never run together: an easier telegraph turns a harder one off.

| Section | Module | Effect | Percent |
|---|---|---|---|
| Harder | CROSSFIRE | Two enemies fire at a time. | +25 |
| Harder | FIRING SQUAD | Every enemy fires at the same time. | +50 |
| Harder | QUICK TRIGGER | 1.0 s from the laser to the attack. | +25 |
| Harder | HAIR TRIGGER | 0.8 s from the laser to the attack. | +35 |
| Harder | HARD HITTERS | Enemy attacks do 1.5x damage. | +15 |
| Harder | FRAGILE | Her maximum HP is halved. | +20 |
| Harder | ONE-HIT WONDER | Any hit kills her. No second wind. | +80 |
| Harder | SCARCITY | MK recharges at half speed. | +15 |
| Harder | SHORT FUSE | The SHORT bar fills 1.5x as fast. | +15 |
| Easier | SLOW TRIGGER | 2.0 s from the laser to the attack. | -20 |
| Easier | LONG REST | Enemies rest 4 s after an attack. | -15 |
| Easier | THICK SKIN | Enemy attacks do half damage. | -25 |
| Easier | STEADY MIND | The SHORT bar fills half as fast. | -20 |
| Easier | DEEP WELL | MK recharges twice as fast. | -15 |
| Easier | MENDING | HP recharges twice as fast (outfits with an HP recharge). | -10 |

The kinds: the guns (CROSSFIRE, FIRING SQUAD), the telegraph (QUICK, HAIR, SLOW TRIGGER), the damage (HARD HITTERS,
THICK SKIN), MK (SCARCITY, DEEP WELL), SHORT (SHORT FUSE, STEADY MIND).

The screen: the main menu's DIFFICULTY button and the pause menu open the same screen. The total and PRESET or
CUSTOM on top, the three preset buttons, then two columns, HARDER and EASIER. The row under the pointer or the
keyboard shows its effect at the bottom. The main menu button names the choice: "DIFFICULTY 150%" or
"DIFFICULTY CUSTOM 135%". The inline panel of the main menu is gone.

Code: `DifficultyModuleDefinition` (section, exclusive group, presets), `DifficultyManager` (`SetPreset(int)`,
`ActivePresetPercent`, the numbers, `EnemyDamage`), `CreateDifficultyModules` (16 assets, the old 7 deleted),
`DifficultyMenuUI`. The enemy damage on her goes through `DifficultyManager.EnemyDamage` at its six places (bullet,
melee, grenade, rocket, gas). `PsychicStats` reads the HP, MK and SHORT numbers, and a change in the middle of a game
applies at once. Tests and bots run at 150 %: in batch mode the manager neither reads nor writes your saved choice.

## 6. "nalcon" on screen

The drug is the MK CAPSULE on screen now (the HUD line, the inventory, "Take the MK capsule?"). PROPOSED name: the
writer can give it its own. The code keeps its ids (`DrugType.Nalcon`, the item id `nalcon`). The debug log lines say
MK. The design file still says NALCON in §2 and §12: a row for the writer.

## Proof

- EditMode 56/56 (the new `DifficultyManagerTests` and `DifficultyModuleAssetTests`: each preset sums to its percent
  and gives the old numbers).
- PlayMode 240/240. New: `DifficultyTests` (7: the screen, the enemy damage, FRAGILE, ONE-HIT WONDER, MK, SHORT and HP
  recharge, the telegraph modules), the floater test, the reading checks, the kit checks. The one warning is the
  expected line of the missing-room seam test.
- The six bots CLEAN. The structural bot has a new step, `difficulty_ui` (150 % PRESET, then 205 % CUSTOM).
- The four kit rooms refer to 0 of 3310 vendor asset ids.

Shots, checked at full size: `~/glorified-body-renders/game/menu/2026-10-26_difficulty/` (`difficulty_ui.png` the
150 % preset, `difficulty_custom_ui.png` with THICK SKIN and ONE-HIT WONDER on, `hud.png` with the MK CAPSULE line).

## For the writer

The design file (`03_GAME_DESIGN.md` §2 and §12) still names the drug NALCON and says that it keeps its name. Bruno
now wants no "nalcon" on screen. The drug needs its final name.

## His answers to the three decisions

```text
1: keep it
2: no changes atm
3: mk capsule; design file is outdated in many ways
```

So 150 % stays the default, the modules and the percents stay, and the drug is the MK CAPSULE.
