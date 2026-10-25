# 2026-10-25: two studies, the wall lean and the pain of the short

Bruno's words: "study: add an anim where they lean against a wall (like they're trying to make themselves smaller
profile but have no cover available, kinda like the guy in white on the screencaps)". And: "study: also, shock should
have an anim of the char in pain (time freezes, the anim plays out) and a shock sfx from the player going onto the
enemy/entity it is damaging (as of now it is not clear who the shock is damaging)".

These were studies. Bruno answered the same evening, and the build is in `2026-10-25_poses_short_pain.md`. In
short: the lean is one more pose, made by code. The RFab fear presets are bought. The freeze and the pain play on Lucy
at the start of the short only. The shock stuns its victim. The zap is a placeholder for now.

## Study A: the wall lean

### What it is

A civilian with no cover presses its back flat on the nearest wall. The arms go tight to the chest, the head turns
away from her, and the body trembles at full strength. It is the man in the white suit in his reference frame.

### When it shows (PROPOSED)

| Case today | With the lean |
|---|---|
| Wary, a wall behind it on every side, she is more than 5 m away: it stands and trembles | It backs to the wall and leans |
| Panicked, the flee spot is against a wall: it cowers (a crouch, the back to her) | It leans when the spot is against a wall, else it cowers |
| She comes within 3 m | It begs (no change) |

### Three ways to make it

| Option | How | Cost | Risk |
|---|---|---|---|
| 1. A pose by code (my pick) | `NavMesh.FindClosestEdge` gives the wall and its normal. The body backs to the wall. The spine and the shoulders bend back onto it. The arms fold (the two-bone reach of `EnemyGun`). The head turns away. `Tremble` at 1. | 4 to 6 h with a test and a bot shot | A pose by code looks like a placeholder (the long-gun arms) |
| 2. A clip | The vendor sets have no lean or cover pose. A free clip site has fear clips. Its licence does not let a public repo carry the raw file, so the file stays local. | 2 to 3 h after we find the clip | A clip on our T19 rig needs a retarget check |
| 3. RFab | The motion presets have no lean (RFab gap 13). They have `afraid`, `scared_01`, `scared_02` and `cry`. These can animate our T19 models, which come from RFab. They can replace the cower and the wary pose, not the lean. | About 10,000 tokens ($0.20) for each motion on each model | A preset clip needs the same import as the T19 clips (the travel measure) |

### Questions for Bruno

1. The lean replaces two poses: the cower against a wall, and the wary "stand and tremble". Is that right?
2. When she comes closer, does it slide down the wall to sit, and then beg? (A second pose, about 2 h more.)
3. Option 1 now, and a clip later?
4. Do I buy a look test of `scared_01`, `scared_02` and `afraid` on the lab worker (about 30,000 tokens, $0.60)? They
   can replace the cower and the wary pose.

## Study B: the pain of the short, and who the shock hits

### What happens today

Every 0.4 s, a shock hits one victim (12 HP). A yellow sphere of 0.2 m shows for 0.25 s at her and on the victim.

No sound plays, and the victim does not react. A kill by the short bursts the head. Bruno's report is correct: the
player cannot see well who the short hurts.

### The four parts

| Part | Proposal | Cost |
|---|---|---|
| 1. The arc | At each shock, a jagged line goes from her chest to the victim's chest. It has 4 to 6 segments that change every frame for 0.15 s. It is blue white, with a short light at both ends. This shows who the shock hits. | 1.5 h |
| 2. The sound | A zap at the victim on each shock, and a low hum on her for the 16 s. The vendor folders have no electric sound. The sound source is still OPEN (RFab gap 14). A placeholder from ffmpeg (noise, a 50 Hz buzz, crackle) can go in now. | 0.5 h for the placeholder |
| 3. The pain | Each shock jolts the victim: a strong `Tremble` (fast, the arms stiff, the spine arched). The first shock on a new victim also plays `Hit Big Front` (a vendor clip), or the RFab `hurt` preset. No preset shows an electric seizure. The body does not walk or aim during the shocks. | 2 to 3 h |
| 4. The freeze | `ImpactFrameController` already freezes the game for 0.18 s. During a freeze, the victim's Animator and `Tremble` use unscaled time. So the pain plays while the room stands still. | 1 to 2 h with the tests (the pause menu and the death flow also set the time scale) |

### When to freeze (a choice for Bruno)

The short fires 40 shocks in 16 s. A freeze at every shock makes the game stutter for the whole short.

| Option | Feel |
|---|---|
| a. Every shock, 0.1 s | Heavy, slow. About 4 s of the 16 s is frozen. |
| b. The first shock on each new victim, 0.15 s, and the killing shock, 0.5 s (my pick) | A clear start, a clear end, the middle runs |
| c. Only the killing shock, 0.6 s | A kill cam, with the head burst in the frozen moment |

### Questions for Bruno

5. Which freeze option, a, b or c?
6. Does a shock stop the victim (no walk, no aim) while it lasts? Today a shocked soldier can still shoot her.
7. The placeholder zap from ffmpeg now, and the real sound when the source is decided (RFab gap 14)?

Total for both studies, if all parts go in: about 10 to 14 h.
