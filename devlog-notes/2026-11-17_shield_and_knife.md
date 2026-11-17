# 2026-11-17: the shield on the forearm, the knife by Invector's sword

Bruno: "Yes, fix them first" (the shield guard and the knife, before the texture pass).

## The shield (`guard_shield`, `soldier_shield`)

Before, the shield stood in front of his left side and followed no bone. The left hand hung at his side, or sat at
his face in the vendor rest clip.

Now (`EnemyShield`, `EnemyGun.SupportHandOff`):

- **The shield's own frame** comes from its mesh: the face out of the thinnest side, up along the longest side. The
  mesh is a turned and unevenly scaled child of the model, so the model's box axes lie. The first try used them, and
  the shield stood edge-on to his front.
- **Two points on its back.** The handle sits at the middle, a little over half the height, 3.5 cm behind the back.
  The cuff sits 7 cm left of the handle and 6 cm farther back. A model with the children `grip` and `forearm` gives
  its own points.
- **The left arm carries it.** `vIKSolver` puts the wrist at a carry point in his frame, with the elbow hint out to
  his left. The carry point moves left in the aim.
- **Then the shield sits on the arm.** `EnemyShield.HoldOn` puts its handle at the wrist and its face to his front,
  then leans it at most 25 degrees, so the cuff lies on the forearm. In the aim the face turns 8 degrees to his right.
- **The left hand** turns with the fingers toward his right and the palm to the shield.
- **The strike** keeps the shield on the arm: the generic strike path calls `SupportHandOff`.
- **The pistol arm** fires past the shield's right edge.

## The knife (`labworker_knife`)

Invector's melee side has one-hand sword clips (`Melee_CombatSet`, `Melee_StrafeMoveset`). Its melee vBot
(`AI - Arena_Blue_Melee`) holds the short sword at its right-hand Handler. The probe shots of each clip on the lab
worker set the three poses:

| Moment | Invector clip | What it shows |
|---|---|---|
| Rest and walk | `standing_idle` at 0.3 | The blade low at his side |
| Wind-up (the telegraph) | `WeakAttack_SwordA` at 0.10 | The slash drawn back, low at his right |
| Strike | `WeakAttack_SwordA` at 0.55 | The thrust ahead at chest height |

- `HoldCalibration` samples the three clips on the class's own rig. It stores the arm bones' local turns for both arms
  (`HoldOffsets.Entry.restArms`, `guardArms`, `strikeArms`, upper body 0).
- It also stores the sword's grip and blade line in the hand, carried across the Humanoid hand frame.
- `EnemyGun.InvectorKnife` blends the poses:
  - At rest: the rest pose.
  - In the wind-up: rest to guard, by the circle.
  - In the strike: guard to thrust (45 % of the strike), then back to rest.
- The knife's `grip` point sits on the grip and its line to `muzzle` lies along the sword's blade, so a new knife
  model drops in with its own two points.
- The vendor animator's unarmed arms (the fists at the face) are no longer seen.
- The knife is 0.45 m (it was 0.32 m), 0.39 m from the grip to the tip.

## Proof (bot `holds`, AI TEST)

The lineup shows the shield and knife kinds at rest, walking, aiming (or winding up) and striking, at the same
angles. The new `Clear` check fails a moment when either of these is true:

- a point of the head, chest, right arm or gun is inside the shield's box (1 cm margin);
- the right arm or the knife is in a box ahead of the face.

For a shield it also needs the wrist on the handle (3 cm) and the forearm through the cuff (8 cm).

| Moment | Before | After |
|---|---|---|
| Knife: rest, walk, strike | face crossed (the blade and the tip) | clear |
| Knife against Invector's sword grip | 0.0 deg, 0.000 m | 0.0 deg, 0.000 m |
| Shield arm, every moment | not measured | wrist 0.000 m from the handle, forearm 0.000 to 0.023 m from the cuff |
| Shield body cuts | none | none |

`guard_shield` aimed only once the bot let it aim on the way: it shoots from afar and then keeps its distance.
Result: 26/26.

Shots, before and after at the same angles: `~/MEGA/glorified/renders/game/shield_knife_2026-11-17/`.

## Still open

- The vendor great shield has no handle mesh: the left palm lies flat on its back. The stock shield (T51) should
  bring a handle and a cuff (the children `grip` and `forearm`).
- The knife is the vendor short sword, scaled. It is thin, so its edge reads less well than its length. T51 brings
  the stock knife, with a `grip` child and a `muzzle` tip.
- No finger bones, so the glove is the grip.
- Power armour: no change (Bruno, 2026-11-17). It still bends far forward to aim at her.
