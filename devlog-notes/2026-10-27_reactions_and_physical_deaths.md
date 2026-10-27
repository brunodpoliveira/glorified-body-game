# 2026-10-27: hit reactions, physical deaths, and the Delirium spike

The order from Bruno (night):

```text
option d and 1 day spike of delirium; we'll analyse which is the best; every death play outs phsyically. you may
spend as much as you need on this test; you may test as much as you want
```

His review of the first clips (day):

```text
hmm, i don't really like the animations at all; they're a combo of glitched + weird that i don't like at all. the
shock reaction makes the guy looks like he's dancing; the fire reaction is too muted; the chest reaction stays longer
on this glitched out pose then in the actual animation and so on
```

```text
we should use invector clips for everyone, for all animations that are applicable; we only generate the ones that
invector does not have already
i can't see 06; but the power armor and the shock death has this uncanny knee/leg rotation; the lower leg bends
inwards in a very unnatural way
```

```text
A + B; plus hit on lucy
```

Study: `2026-10-26_study_active_ragdoll.md`.

## 1. The Delirium spike: a blocker

A sub-agent built Delirium for Linux outside the game and tested it with a body of our own (Python, no Unity). I
checked the key facts again in the clone. Full report: `/home/pc/delirium-spike/REPORT.md`.

| Question | Answer |
|---|---|
| The data | At HEAD, 992 engine numbers are encrypted. The key is a hash of a GTA IV file (`fred.xrt`). Without it every number is 0. |
| What the author says | CHANGELOG 0.2.5: "The game's whole numbers ... now load from your own game files too". The same numbers are in plain text in the history before 2026-10-26. |
| What the code says | The SDK header: "the NM (Delirium) port's C API". It reads `NMEUPHORIA` files and has `rage_*` functions. A "clean-room" section came into the README on 10-18 and went out on 10-22. |
| Our own body at HEAD | Refused (-7). The first step aborts the whole process: in Unity that kills the editor or the game. |
| Our own body before the lock | It loads, but it falls in 0.9 s with no push. Every test case (pistol, blast, electrocute, writhe, catch fall) falls first. |
| The cost | 0.27 to 0.55 ms for each figure step. |
| The Unity package | An asmdef (our rule 6 forbids it), and its start loads the GTA IV file. |

So Delirium is not safe for a public game: its tuning comes from GTA IV, and it does not work without that file. The
license (GPL-3.0-only) fits, but the data does not. Plots: `/home/pc/delirium-spike/work/results/`.

## 2. Every death plays out physically

Before, a soldier or a civilian who died standing played the Invector death clip. Only a body that was already down
fell as a ragdoll. Now (`EnemyRagdoll`, the physical death):

| Part | Behaviour (numbers PROPOSED) |
|---|---|
| The fall | In the frame of the death the bones go limp from the living pose. The body keeps its walk speed. |
| The push | The hit that killed takes the hit bone: a bullet 1.8 m/s, a part mk_warspite 2.5 to 4.5 m/s, a throw 3.5 m/s, a full mk_warspite 6 m/s, an explosion 7 m/s with a lift. The whole body takes a share, the hit bone the rest (it turns the body). The push goes away from the attacker. |
| The muscles | Each bone holds the last living pose and lets go over 1.0 s, the legs in 0.55 s: the knees buckle, and the body crumples the way it was pushed. A full blast lets go in 0.3 s. |
| A shock kill | 1.6 s of convulsions: strong jerks on the hips, the shoulders and the spine, 14 a second. |
| A fire kill | 3.2 s of writhing on the floor (slower turns). |
| The end | The body settles and freezes, as before (the saves, the drag, the CPR, the pools work on it). |
| The power armour | A ragdoll of 220 kg for its death only: it never falls alive (no knockdown, no lift, no drag). Its jerks are half as strong. |
| Lucy | A ragdoll is built on her bones at her death (`PlayerDeathFall`, the builder `HumanoidRagdoll` now runs in the game too). She keeps her speed and takes the push of the killing hit. The death screen waits 1.6 s for her fall (it was 0.45 s). |

## 3. The knee bug (Bruno: "the lower leg bends inwards")

Cause: a Unity CharacterJoint counts its twist the other way round from `Quaternion.AngleAxis` (the Unity ragdoll wizard
gives a knee -80 to 0 degrees). `HumanoidRagdoll` set the sign with `AngleAxis`, so every joint opened to the wrong
side: a knee bent 130 degrees backward and 2 degrees forward. The hips, the elbows, the shoulders and the spine had the
same fault. The knockdown ragdoll of 2026-10-24 had it too.

The fix: the sign (`HumanoidRagdoll.Joint`), 16 solver passes for each bone (32 for a body over 120 kg), the joint
projection at 10 degrees, and no shock or fire jerk on a knee or an elbow (a jerk there slammed the shin into its
limits).

| A new test (`RagdollTests.TheKnees_BendOnlyLikeKnees_InEveryDeath`, 8 knees: a shock, two fires, a bullet) | Before | After |
|---|---|---|
| Knee bent the wrong way (the worst moment) | 44 to 137 degrees | 0 to 3 |
| Shin out of the plane of the knee (the worst moment) | 9 to 80 degrees | 9 to 22 |

## 4. The reactions: Invector clips first

The first version (the night) played RFab presets on every figure. Bruno did not like them. Three causes: the RFab
clips came from an unarmed lab worker, so on a soldier the clip bent the spine while the arm IK of the gun pulled the arms
back (the "glitched" pose). A hand-on-wound IK held that pose for 2.5 s after a 0.9 s clip. The shock used `freaky`, a
dance-like preset.

Now (`HitReactions`, on every soldier, guard, civilian and heavy, numbers PROPOSED):

| A hit that does not kill | The body |
|---|---|
| A bullet, a part blast under half a charge | The small vendor hit (`Small_From_Front/Right/Left/Back`), on the UpperBody layer: the legs walk on. The side comes from the attacker (the `HitAngle` rule of the vendor, the same path as `vAIMotor.TriggerDamageRection`). Plus a step of 0.18 m (0.35 m for melee) on the NavMesh. |
| A strong hit (30 or more, an explosion, a blast of half a charge or more) | The big vendor hit (`Big_From_...`), on the FullBody layer. Its clip moves the body. |
| A leg | A limp for 3 s: the walk at 70 % speed, the hips dip and roll on each step. |
| A shock of the short | A spasm for 0.6 s: the animator stops (the body locks) and the spine, the head and the arms shake 24 times a second. Invector has no clip for it. |
| A part-charge fire (the flash) | The small vendor hit from the front. |
| The start of a full fire | A panic for 3 s, and no attack (a stun): a big vendor hit from a new side every 0.8 s, and over it the torso twists, the head shakes and both arms beat at the flames. The hands leave the gun (it stays in the right hand). Bruno picked both versions together. |
| A full mk_warspite | No flinch: the knockdown is the reaction. |
| A heavy | Only the small hit: no step, no limp, no panic. |
| A civilian in a fear pose | A jolt (`Tremble`): the pose holds. |
| Lucy (Bruno: "plus hit on lucy") | The small vendor hit from the side of the shot, on a new "Hit" layer over her upper body (`CreateCharacterAssets.AddHitLayer`, the mask `Characters/Anim/UpperBody.mask`). The legs and the tank control stay free. One flinch per 0.35 s. |

Two faults found on the way:

- The Invector `vAIMotor.UpdateAnimator` writes `animator.speed` from its `animatorSpeed` in every frame. The first limp
  set `animator.speed` once, so the walk slowed for one frame only (its test read that one frame). Now the body sets
  the vendor field (`IEnemyBody.SetAnimatorSpeed`).
- The burn ticks are fire hits with damage. Each tick made a flinch (8 in 2.8 s). Now the panic is the only reaction
  to a fire.

The reactions run before the gun (`[DefaultExecutionOrder(-10)]`): the gun and the hands on it follow the moved torso.

## 5. RFab

- The lab worker model of T19 lives on the local backend, which was down. I did not start Bruno's server. I rendered a
  dead-front picture of our own lab worker FBX in Blender and made a new model on rfab.ai from it.
- Spent: the model 20,000, the rig 25,000, ten motions 100,000 tokens. Total 145,000 tokens ($2.90).
- The ten clips are out of the game after the ruling of Bruno (Invector has hit clips). They stay in
  `~/glorified-body-renders/game/reactions/rfab_hits/` (the FBX in `unused_in_game/`).
- Gap 15 (new): ten motions in one call fail at the provider. Groups of three work.
- Gap 16 (new): no hit while walking, no limp, no writhe, no electrocution in the presets. The game makes them in code.

## Proof

| Check | Run | Result |
|---|---|---|
| EditMode | `runs/20261027-151703-test-editmode-176144` | 56/56 |
| PlayMode, every suite | `runs/20261027-151732-test-playmode-176432` | 253/253 (new: `HitReactionTests` 8, the knee test, the physical death tests in `RagdollTests`, the fall of Lucy in `MenuLoopTests`) |
| Bots: structural, look, tutorial, ai, cover, civilians, reactions | the same chain | 7/7 PASS, triage CLEAN |
| Reactions bot, record mode | `runs/20261027-151336-record-174862` | 12/12 steps |

Clips for Bruno (1920 x 1080): `~/glorified-body-renders/game/reactions/clips/latest/`

| Clip | It shows |
|---|---|
| `react_01_small` | two bullets, two sides: the small vendor hit and the step |
| `react_02_big` | half a mk_warspite: the big vendor hit |
| `react_03_limp` | a leg hit while he runs |
| `react_04_shock` | the spasm of a shock |
| `react_05_fire` | the fire panic (A + B) |
| `react_06` to `react_09` | the deaths: a part blast, a full blast, a shock, the armour by fire |
| `react_10_lucy_hit` | Lucy: a hit from the front, then from her left |

Changed tests (each with its reason in a comment): `CivilianTests.ALeaningCivilian_...` kills with a full blast
now (a knockdown refuses a dead body), `RagdollTests.AFullBlastKill_...` checks the push speed (5.2 m/s) because a
cover block behind the soldier stops the flight at 0.9 to 1.6 m.
