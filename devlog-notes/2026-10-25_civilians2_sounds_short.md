# 2026-10-25, afternoon: the civilians walk back, the double sounds, the short on the press

Bruno's review of the morning build. His answers to the four open points:

1. Keep the auto-lock skip of civilians.
2. Keep the scream alert within 25 m.
3. Keep the `<class>_<weapon>` ids. Friendly names come later.
4. The writer's lines can go into the public repo.

## 1. A bug: two sounds for one event (CONFIRMED)

His words: "the pistol has two sound effects? check if other attacks also have this bug".

| Event | Before | Cause |
|---|---|---|
| A bullet hits a wall (pistol, rifle, a returned bullet) | The shot, then a second shot at the wall | The FSM AI package sets all five vendor impact prefabs to `pistol-shot-ricochet.wav`. That clip opens with a full gunshot (-2 dB at 0 s). |
| A power on a shield | A gunshot and the block sound | The same impact prefab |
| A frag grenade or a rocket | `Explosion_A` two times at once | The vendor `Explosion_Air` prefab plays its own copy, and our code plays one |
| A gas grenade | An explosion and the gas sound | The vendor smoke grenade keeps its own `Explosion_A`. The factory could not remove that source (an audio filter needs it). |
| The pistol and the rifle | Two gun sounds mixed at random | Each set mixed the old `Handgun@Fire` or `Rifle@Fire` with the new clips |

The fix: `ShotEffects.SpawnSilent` makes each vendor effect with its sources off. A wall hit plays one quiet hit
(`Knife Hit Wall 1-3`). A shield block plays the block only. Each gun uses one family of shots. The aim click, the
rocket and the melee sounds were already single.

Proof: `ShotSoundTests` (5 tests). A wall hit has no gunshot. An explosion, a block and a gas cloud have one sound each.
Each gun has one family. Four tests failed before the fix, with the clip names in the message.

## 2. The short: civilians and the press

- "it should also dmg civilians": it does. The new test `TheShort_HurtsACivilian` passed before any change (the victim
  is `Lab_B`, and it loses HP). No change.
- "shock should trigger the moment player presses attack": CONFIRMED BUT DIFFERENT. A press with a full bar already
  starts the short (a test since 2026-10-20). The bug: when the bar fills during a held charge (a hit), the short
  waited for the release. Now it starts in that frame, and the charge is lost. Test:
  `FullShort_TheBarFillsDuringACharge_TheShortStartsAtOnce` (it failed before the fix).

## 3. The civilians, after his rules

His words: "initially, the civvies walk back slowly"; "they'll only run and cower if the player kills one (guard,
civvie doesn't matter)"; a tremble; the drag and the CPR, "a nurse'll drag and cpr even if player on sight; a
scientist will do this if player away same as the others".

| Phase | When | What it does |
|---|---|---|
| Wary (new) | It sees her, or it hears a shot, a scream or a power hit | The first sight screams. It walks backward, facing her, about 1 m/s, until she is 6 m away. It trembles. |
| Flee, then Cower | It sees a death (any kill), or it is hurt | It runs to the far side and cowers, for good. The first line is the writer's "Oh God. Oh God, she's killing people!" |
| Beg | She comes within 3 m, or a wall is behind it on every side | No change. When she leaves, it backs away again (wary) or cowers (panicked). |
| Care (new) | A body lies within 15 m | The nurse goes at once, with her in plain sight. A lab worker goes only after 3 s without a sight of her, as a soldier does. |

- The tremble (`Tremble`): small fast turns on the spine, the neck, the head, the arms and the hands, over the clip.
  0.6 when wary, 1 when it cowers or begs, 0 when it runs or tends a body. It eases in over 0.5 s.
- `CorpseCare` works on any brain with the new `ICarer` seam (the soldier and the civilian).
- The writer added 13 rows to `barks.csv`. They cover the walk back, the kill seen, and the care of each class. The
  care lines of the soldier and the guard replace "MAN DOWN!" and "STAY WITH ME!".
- PROPOSED: a hurt also panics a civilian (his rule names only a kill).
- The writer's idea, not canon: the lab worker asks its collar "How hard do I push?" during the CPR. In the canon, only
  the scientist talks to the collar.

Two more fixes. A bark during a room unload threw an exception (`Bark`). The aim brace and the lift logged a warning
each time: the code set a velocity on a kinematic body.

## Proof

- The full run before the last two fixes: EditMode 43/43 and PlayMode 162/162. The six bots pass.
- The civilian tests (14). The wary walk: the gap goes from 4.1 to 6.5 m in 2.5 s, facing her, with a 2.7 degree
  tremble. The nurse: the drag, then the CPR, 4.1 m from her with a clear sight line. Also: the run and the cower
  after a kill, the beg, the lab worker's care after she leaves, and the short on a civilian.
- The "civilians" bot, 9 of 9: `tools/unity.sh play CivilianTest Main_Menu civilians 300`.
- Shots: `~/glorified-body-renders/game/civilians/2026-10-25_afternoon/`. They show the wary walk, the beg, the
  cornered nurse, the run, the cower, the drag, the CPR and the death screen.
- After the two last fixes, the AI bot passes with 0 warnings (55 last night). The
  cover bot passes with 2. These are the drift check of a lifted soldier, an old diagnostic. The enemy, civilian and
  morale tests pass, and the sound tests pass 7/7.
- A known flake: `EnemyAiTests.Gas_TheCloudIsVisible_AndGreen` counts green pixels of random smoke. It failed in 2 of
  13 runs on 2026-10-24. Today it failed in 2 group runs: 1028 and 1137 pixels, and the bar is 1152. It passed alone, 3 of 3.
  I did not change the test.

## Studies (not built)

`2026-10-25_study_wall_lean_shock_pain.md`: the wall lean, and the pain of the short (an arc to the victim, a zap, a
jolt, a freeze). Seven questions for Bruno.

## For later (his words)

"replace the placeholder fire/shock effects".

## Open for Bruno

1. Does a hurt panic a civilian, or only a kill?
2. Do the lab workers wear a collar (the writer's CPR line)?
3. The seven questions of the study note.
