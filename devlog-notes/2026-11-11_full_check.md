# 2026-11-11: the full check after the W14 TV and the floater fix

The whole PlayMode suite and all 27 bots, from 02:50 to 04:01 (rule 10).

## Result

| Check | Result | Run |
|---|---|---|
| PlayMode suite | 483 passed, 0 failed, 1 skipped (`TheFrame_Profile`, explicit) | `runs/20261111-025011-test-playmode-206433` |
| Bots | 26 of 27 passed. The `team` bot failed at `pair_one_walks`. | `runs/20261111-034226-play-210897` |

## The team bot: a fault in the bot check

The detail of the failed step:

```text
Soldier_Pair1_B walks, Soldier_A walks too
```

1. The bot looked at one frame of the search and asked: does only one man of the pair walk?
2. At a handover, both men of a pair walk for a moment. The note of 2026-11-10 measured this: 2 to 4 % of the frames.
3. So one frame fails in about 1 run of 30. The AI did what it must do.
4. The PlayMode test `TheFireteam_SheHides_TheyFireAtThePlace_ThenSearchInPairs_ThenContact` uses a ratio over 25 s: "one walks" frames are more than 3 times
   the "both walk" frames. That test passed in the suite.

Verdict: WORKS AS DESIGNED. The bot check was wrong.

## The fix

The bot now counts the frames of the pair for 3 s and uses the same rule as the test
(`TeamPlaythrough`, step `pair_one_walks`).

| Run | Result |
|---|---|
| `runs/20261111-040218-play-217424` | pass: one walks 390 frames, both walk 0 |
| `runs/20261111-040344-play-217817` | pass: one walks 394 frames, both walk 0 |
| `runs/20261111-040500-play-218122` | pass: one walks 401 frames, both walk 0 |

No shot: the change is in the bot only.
