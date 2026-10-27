# 2026-10-26, study: living ragdolls (Delirium) or more reactions from the figures

Study only, nothing is built. Bruno's words:

```text
sstudy: adding delirium for ragdolling, or adding more reactivity to the npcs in some way:
Delirium swaps the game's limp ragdolls for living ones, just like GTA IV.
It's powered by Delirium, an open-source engine that recreates the Euphoria-style physics behind GTA IV, adapted to
work in Skyrim.
NPCs stagger, fight to keep their balance, clutch their wounds and stumble the way the hit pushed them.
Fire makes them writhe, shock makes them convulse and frost makes them seize up. Unrelenting Force sends them
flailing through the air.
Kill moves are gone too, so every death plays out physically.
```

## What the figures do today

| Event | What the body does now |
|---|---|
| A hit that does not kill | A stun (`IStaggerable.Stagger`): the aim breaks, the brain waits. The body does not move. |
| A full mk_warspite | A knockdown: the limp ragdoll for 2.5 s, then the vendor stand-up clip (`EnemyRagdoll`). |
| The lift, a throw, a slam | The limp ragdoll, held by the hips, thrown or dropped. |
| A shock of the short | A stun and a jolt (`Tremble.Jolt`), the arc. |
| Fire (full charge) | The burn tint and the damage. No body reaction. |
| A death | The limp ragdoll. It settles and freezes. Head burst or limb cut by the kind of kill. |
| A civilian in fear | Poses (crouch, wall lean, plea), the tremble, the slide down a wall. |

So: the limp ragdoll exists (11 bones). A body that fights to stay up, staggers in the push direction, clutches a
wound, writhes or convulses does not exist.

## Option A: Delirium

Facts (from the repository, 2026-10-26):

- An active ragdoll engine in Rust, GPL-3.0-only. Our repository is GPL-3.0, so the license fits.
- A native library (`.so` for Linux) and an SDK with C, C++, C#, Unity and Python bindings.
- Two modes. LEAD: Delirium runs its own physics world. FOLLOW: the host (Unity's physics) owns the bones, and
  Delirium gives a force and a torque for each part every step. The Unity package uses FOLLOW.
- A body has exactly 21 parts and 20 joints (ours has 11). The tuning works best near 1.8 m (our soldiers are 1.80 m).
  One agent per figure, 60 Hz.
- Behaviours: balance, stagger, stumble, teeter, flinch, brace, grab, writhe, electrocute, catch fall, high fall,
  roll down stairs, dangle, drunk.
- Small and new: 44 stars, 93 commits, 4 open issues. The Skyrim mod is two days old.

Risks:

1. Where the data comes from. Its demo room reads the character, the animations and the settings from the user's own
   copy of GTA IV, and the repository holds readers for "the game's file formats". The README makes no clean-room
   statement and does not name NaturalMotion (the owner of Euphoria). We must make our own body file and tuning, and
   ship nothing that comes from those files. A legal check before a public release is your decision.
2. A crash in the native library stops the editor and the batch runs.
3. In FOLLOW mode the bones must be live physics while the soldier walks and aims. Today Invector's animator owns the
   living soldier, and the bones are kinematic. So Delirium also needs a "powered ragdoll" layer (option C's base).
4. Delirium removes kill moves, so every death is physical. Our head bursts and limb cuts stay possible on top.

Cost: about 1 day for a spike, then 5 to 8 days for the adapter (a 21-part rig, the FOLLOW bridge, one behaviour for
each of our events), if the spike passes.

## Option B: PuppetMaster (RootMotion)

- A paid Asset Store package: $90 a seat, version 1.4 (January 2025), Unity 6 listed. Like Invector, it stays out of
  git (vendor data), and the enemy prefabs that use it stay local (`Resources/Enemies`).
- Mature: the animated pose pins a muscle ragdoll, a hit unpins the hit part for a moment, a loss of balance becomes
  a fall, and the figure gets up. Reviews say the setup takes care, or the bodies fumble.
- No writhe, convulse or clutch behaviours ready to use: we script them on its muscles.

Cost: 3 to 5 days.

## Option C: our own powered ragdoll

- Each bone follows the animated pose through a joint drive. A hit weakens the drive of the hit bone and its
  neighbours, and the strength comes back over 0.3 to 0.6 s. A balance check (the centre of mass over the feet)
  chooses a stagger step or a fall into the limp ragdoll that we have.
- Shock: noise on the drive targets (convulse). Fire: the drive targets play a writhe loop.
- No package, no license question. The highest risk of jitter (Unity's ragdoll stability notes).

Cost: 6 to 10 days.

## Option D: more reactions, no new physics

Animation and code on the figures that we have. Each item is 0.5 to 1 day:

| Reaction | How |
|---|---|
| A flinch by hit zone (head, chest, arm, leg) | An additive hit layer over the animator, from the hit point of `HitInfo`. |
| A stagger step in the push direction | A short root move plus a step clip, instead of the still stun. |
| A leg hit makes a limp | A limp walk for 3 s (an RFab motion). |
| Clutch the wound | Arm IK from the hand to the hit point, while the figure moves. |
| Fire: the burning figure runs and screams, then writhes on the floor | A panic run, then an RFab writhe clip. |
| Shock: convulse | A stronger `Tremble.Jolt` on the whole body for the stun time. |
| Her charge circle | The target flinches or ducks when her full charge comes, and others take cover. |
| A thrown body lands near them | They jump back. |

RFab motions cost about 10,000 tokens ($0.20) each. A limp walk, a writhe loop and three flinches: about $1.

## My recommendation

1. Build option D first (3 to 5 days). It gives the most reaction for each day and adds no risk.
2. In parallel, a 1-day spike of Delirium: build the Linux library, run its Unity example in our project in batch, and
   push one soldier with a blast. The spike also reads the code for the data question.
3. If the spike fails, use PuppetMaster ($90) for the physical falls.

## Your decisions

1. Which goal comes first: physical falls and deaths (A, B or C), or more reactions while they live (D)?
2. Do you allow the 1-day Delirium spike?
3. If Delirium fails, do you buy PuppetMaster ($90)?
4. Must every death play out physically, as in the mod? The head burst and the limb cut stay on top.
5. Do you allow about $1 of RFab motions for option D?

## Sources

- Delirium engine: https://github.com/PVCPC/delirium-physics (README, docs/how-it-works.md)
- The Skyrim fork: https://github.com/YoucefNabil/delirium-physics
- The Skyrim mod: https://www.nexusmods.com/skyrimspecialedition/mods/196333
- PuppetMaster: https://assetstore.unity.com/packages/tools/physics/puppetmaster-48977
- Unity ragdoll stability: https://docs.unity.com/en-us/engine/6000.3/manual/physics-section/physics-overview/ragdoll/ragdoll-stability
