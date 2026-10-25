# 2026-10-25, evening: the fear poses, the wall lean, the pain of the short

Bruno's answers to the afternoon questions and to the study (`2026-10-25_study_wall_lean_shock_pain.md`):

| His answer | What it changed |
|---|---|
| "after a kill; the civvies will have low enough health that a direct hit will kill a civvie anyway" | A hurt only alerts a civilian (wary, a scream). Only a kill it sees makes it run. |
| The lab workers wear no collar | The writer's fallback line: "How hard do I push? How hard do I push?" (`barks.csv` and the code) |
| "the lean should be added; the multiple poses is to add more dynamicism to the scene" | The lean is one more pose. Each civilian takes its own pose. |
| Option 1, "let's test then refine" | The lean is a pose by code (`WallLean`) |
| "yes, buy it" | Three RFab fear presets on the lab worker |
| "the freeze/pain anim on the player is only at the start of the shock" | At the start of the short, the game freezes and Lucy shows pain |
| "the shock should also stun the victim" | Each shock stuns its victim |
| "placeholder zap, then real sound" | A zap from ffmpeg, as a placeholder |

## 1. The RFab fear presets

- `rfab model animate 2482e45d-… --motions scared_01,scared_02,afraid` on the T19 lab worker. Cost: 30,000 tokens
  ($0.60), as estimated. Log: `~/glorified-body-renders/game/civilians/rfab_fear/log.jsonl`.
- `afraid` (2.6 s) is a startle: a hand to the chest, then back. `scared_01` (3.3 s) is a shaking stance with the
  knees bent. `scared_02` (3.3 s) is a self-hug. None of the three moves the body (measured: under 1 cm).
- `tools/characters_export.py` made `Models/labworker_fear.fbx` (900 KB) with its `labworker_fear.clips.json`. It is a
  Humanoid FBX, so every T19 class can play the clips.
- `PoseOverride` plays one clip over the Animator's controller (a PlayableGraph with one clip). The controller takes the
  body back when the clip stops. `PoseClips` (`Resources/Characters/pose_clips`, made by
  `CreateCharacterAssets.BuildPoseClips`) holds the clips.

## 2. The poses of the civilians

Each civilian gets a variant in turn (0 to 3), so neighbours do not stand in the same pose.

| Moment | Pose |
|---|---|
| Wary, she is far and it stands | `afraid` once, then `scared_01` (even variants) or `scared_02` (odd variants) |
| Wary, a wall behind it on every side | The wall lean |
| Panicked, at its spot | Variant 0: the crouch, the back to her. Variant 1: the wall lean (the self-hug with no wall close). Variant 2: the self-hug. Variant 3: the shaking stance. |
| Begging | The kneel (no change) |

The wall lean (`WallLean`):

- 16 level rays at the height of the hips find the nearest wall within 1 m.
- The skeleton moves back to the wall: the agent cannot get closer than its radius.
- The spine bends back onto the wall, and the head turns away from her.
- The pose plays over `scared_02` (the self-hug) and eases in over 0.4 s.

A fix on the way: the body slides on for a moment after it stops (the Invector walk). The first wall check missed the
wall by about 1 m. A lean civilian now checks again once it stands.

## 3. The short

- The start: the game freezes for 0.6 s, with a white flash (`ImpactFrameController`). Lucy plays a pain clip on
  unscaled time through the freeze. The clip is a vendor placeholder (`Hit Big Front`). No RFab preset shows an
  electric seizure. The shocks do not freeze the game.
- Each shock:
  - A jagged blue white arc goes from her chest to the victim's chest for 0.15 s, with a light at the victim
    (`ShockArc`).
  - The zap plays at the victim (`Resources/Audio/short_zap.wav`, a placeholder made with ffmpeg).
  - The stun lasts 0.6 s: one shock interval and a little more. So the stun does not break. A soldier
    stops and drops his aim (`IStaggerable`). A civilian stops, and its care stops.
  - The victim's body jolts: a strong, fast `Tremble`.
- The lock-on skips frames while the game is frozen. So a release of the attack key counts after the freeze.

## Proof

- The full run: EditMode 43/43, PlayMode 167/167. The ai, cover, tutorial, structural and look bots pass. The AI bot
  logs 0 warnings.
- The civilian bot failed once in that run. The guard came closer than the nurse and became the victim of the short.
  The bot read only the stun of a civilian. The telemetry shows the guard's stun (`enemy_staggered
  guard_pistol:0,60`). The bot now reads any victim, and it passes 11 of 11:
  `tools/unity.sh play CivilianTest Main_Menu civilians 300`.
- New tests:
  - `ShortStart_FreezesTheGame_WhileLucyShowsPain`: the time scale is 0 at the start, and the pain clip plays
    through the freeze. The shocks freeze nothing.
  - `Short_EachShock_ArcsToTheVictim_Zaps_AndStunsIt`: the stun holds from one shock to the next.
  - `AHurt_AlertsThem_OnlyAKillMakesThemRun`, `Wary_Standing_Startles_ThenTakesAFearStance`.
  - `Panicked_TheyTakeDifferentPoses_OneLeansOnTheWall`: the hips stop 0.17 m from the wall.
  - `TheShort_HurtsACivilian` checks the stun and the jolt too.
- One old test changed: `FullShort_StartsOnTheAttackPress_SlowWalk_AndADrugEndsIt` now waits for the start freeze
  to end before it checks her legs.
- Shots: `~/glorified-body-renders/game/civilians/2026-10-25_evening/`. `civ_04b` shows the fear stance, `civ_06` the
  wall lean, `civ_09` Lucy's pain at the start, and `civ_10` the arc. `rfab_fear_sheet.png` shows the three presets.

## Open for Bruno

1. The second study question, said again. A civilian leans on the wall, and she walks up to it. Today it kneels and
   begs at 3 m, as all of them do. Do you want a second pose before the beg? It slides down the wall to the floor,
   then begs from there. About 2 h.
2. The freeze of 0.6 s and the white flash at the start of the short: the right length?
