# 2026-11-14, night: the deep check after the shortcut doors

Bruno: "you can do deep testing to ensure everything is properly functioning". The chain ran from 03:01 to 04:23 on
the tree with the shortcut doors (S3 at x -1.5).

## Result

| Run | Result |
|---|---|
| EditMode | 76 of 76 |
| PlayMode, the whole suite | 505 of 506 passed, 1 skipped (`DemoTests.TheFrame_Profile`, explicit). No failure. |
| Bots | 27 of 28 passed. The cover bot failed one step (below). After the fix: 3 of 3 runs passed. |

The bots: structural, tutorial, demo, team, look, ai, reactions, cpr, holds (AI TEST and LAB TEST), shield, lab,
civilians, cover, code, dark, doors, edgar, outfit, physics, screens, map, tape, ward, pieces, brain, remake,
arkham_time. Each bot's `playthrough.json` says `"pass": true`.

The G10 tree blink test (flaky in the day's first run) passed in the whole suite.

## The fault: the cover bot

Step `barricade_breaks`: Barricade_3 broke after 3 blasts, but the next blast did not hurt Rifleman_A (hp 80).

The cause is in the bot, not in the game. This run he took cover at the east end of Barricade_1 (x -3.25). From
her place, her line passed Barricade_3 first and then the end of Barricade_1. The fourth blast hit Barricade_1
(`cover_took Blast:1,00:Barricade_1:55` in `runtime.jsonl`). That is the rule: cover on her line takes the power.
The bot broke only the first barricade on her line.

The fix (`Automation/CoverPlaythrough.cs`): after the first barricade breaks, the bot blasts again while a barricade
stands on her line (12 blasts at most), waits for the pieces, then fires the last blast. The detail line names the
other barricades and the last result.

Proof: 3 runs of `play CoverTest Main_Menu cover 300`, all pass. In these 3 runs he stood clear of Barricade_1, so the
new loop did not run. The loop runs only when he stands at the end of his cover.

## What is not in this check

1. No player build (the rule of 2026-11-14).
2. Nothing is pushed.
