# 2026-11-10, evening: the flaky tests, the writer's lines, the manual tests

Bruno, from his phone:

```text
Do everything except the release build, it's not necessary to vuild the release
```

## 1. The manual tests

`devlog-notes/2026-11-10_manual_tests.md`: 41 rows for his next play, from the card RAVEN to the bulletin in R1.

## 2. The fireteam search test (CONFIRMED BUT DIFFERENT: a fault in the AI)

`TheFireteam_SheHides_...` failed 1 of 3 runs on 2026-11-09: in the search, the two men of a pair walked at the same
time in a third of the frames. The test allows a quarter.

1. A diagnostic in the test's log: the longest streak of both walking, pair by pair, and the frames with a gun slot
   held (the search skips its stop then). Four runs: streaks up to 5.24 s, and no gun slot. So it was not the 0.12 s
   lag of the speed window at a swap.
2. A trace at the first streak over 1 s: in every streak the two men stood 0.60 m apart, and only one had a walk order.
   The walker shoved his still partner ahead of him, for up to 7.2 s.
3. The cause (`FireTeam.SearchOrder`): the second's spot beside the first came from his own position each frame, so it
   swung from side to side as he walked up, and a wall or a planter snapped it to the NavMesh next to the first. The
   pair ended a leg touching.
4. The fix: the spot is chosen once per leg (`SearchPair.beside`), on one side, else the other, else behind, and never
   closer than 1.1 m to the first (`MinPartnerGap`). The diagnostic stays in the test's log.

## 3. The R1 poster test (NOT REPRODUCED)

`R1_ThePosterBeat_...` failed once, on 2026-11-08 ("the flag goes up", 8 s after the flashback). The line before the
flag is "Who was I calling?", about 2.5 s, so the wait has room. It passed in all 11 runs since, and the test changed
today (the news first). No change on a guess: it stays on watch.

## 4. The writer's lines in the game (round 113, PROPOSED)

The writer agent wrote the rows of `game/barks.csv`. They replace the last PLACEHOLDER barks:

| Where | Line |
|---|---|
| The night manager, hiding (G19) | "Kitchen's closed. Kitchen's closed. Kitchen's closed." |
| The night manager, found | "Ma'am. The key's on the board, in the office. Take it. I'll sign it out. Just take it and go." |
| The cook of G10, found | "Please. I just make the soup. I just make the soup." |
| The server of G10, found | "Please. I'm off at six. I just want to go home at six." |
| A soldier sees the feed | "Contact. She's — Jesus. She's eating." or "She's on him! Put her down! Put her down!" |
| A guard sees the feed | "Oh God. Oh God, she's — get away from him!" or "Hey! Leave him alone! Leave him alone!" |
| A guard retreats | "Dispatch, I'm pulling back! Somebody else take her!" |

The writer also set L18 to one of each, logged round 113 in `RULINGS.md`, and brought `DEMO_PLAN.md` §10 and §13 up
to date. Two questions for Bruno are in its §15: the old night manager rows (a man inside walk-in 1, the build has him
beside it), and the code 867-5309 is the number of a well-known song.
Bruno, the same evening:

```text
1: beside it.
2: keep it
```

The night manager stays beside walk-in 1, as built, and the code stays 8675309. No change in the game.

## 5. The devlog brief

Today's beats are at the end of `the-glorified-body/devlog/ep11/BRIEF.md`.

## Proof

| Check | Before the fix | After |
|---|---|---|
| Frames with both men of a pair walking (the search test) | 474 to 3414, in 7 runs | 125 to 287, in 3 runs |
| The longest streak of both walking | up to 7.2 s | 0.77 s at most |
| Legs of the search in 25 s | 3 | 6 (the pairs cover twice the ground) |
| The fireteam tests, `WardTests`, `CivilianTests`, `EnemyAiTests` | | 71 of 71 |
| The team bot | | passes |
