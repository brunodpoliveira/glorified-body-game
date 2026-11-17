# 2026-11-17: the gun holds come from Invector (option B)

Bruno: "we need to fix the hand positioning on the soldiers and other people first as they hold weapons and knives
weirdly; i'm sure invector has the accurate grips and poses". After the research: "Yes, build B".

## What changed

- **The grip.** `HoldCalibration.Build` (editor, batch) plays Invector's aim clip on the vBot and on each armed class.
  The vendor gun sits at zero on the vBot's Handler (the right hand). The Humanoid hand frame is the same on both
  bodies, so the gun's place and turn in the hand carry across. The left hand's turn on the gun's `leftHandIK` carries
  across the same way. The numbers go to `Resources/Characters/hold_offsets.asset` (`HoldOffsets`), one entry for each
  class and vendor weapon set: soldier 1 2 3, guard 1, toll_guard 1, labworker 1, power_armour 2 3. Each entry has
  tune fields for corrections from the shots. All are 0 now: the shots needed none.
- **`EnemyGun.ClipHoldPose`** (a class with an entry):
  - In the aim, the vendor upper-body clip (`UpperBody_ID`, `IsAiming`) sets the arms.
  - The gun sits in the right hand by the grip.
  - The chest and the upper chest turn so the barrel points at the aim point: yaw up to 45 degrees, pitch up to 60.
  - `vIKSolver` puts the left wrist on `leftHandIK`, turned as the vBot's hand.
  - At rest the low ready of the procedural hold stays: a long gun's butt in the shoulder pocket, a pistol at the hip.
    The right hand holds the gun by the same grip. The raise blends the two in 0.2 s.
  - A one-hand gun: the left hand goes to the shield's inner side, or down at the side. The vendor rest clip held it at
    the face.
  - The procedural hold (`Shoulder`, `PistolHold`) stays for a class with no entry.
- **The knife** (`EnemyGun.KnifeHold`): Invector has no knife hold, so the knife keeps our own code.
  - The hand lies straight on the forearm, the handle is in the middle of the palm, and the blade is along the
    forearm's line.
  - The wind-up raises the hand over the shoulder while the circle closes, and the strike brings it down and ahead.
- **The bot `holds`** in AI TEST is a lineup. The room's figures go, then each of the 13 armed kinds stands alone at the
  same place:
  - It is shot at the same angles: rest (side, front) and aim (hands, side, front).
  - It is measured against Invector's grip: the turn of each hand from the grip, the gun's and the wrist's gaps, the
    barrel against the line to the aim point, and the chest's turn.
  - For the knife: the blade against the forearm.
  - LAB TEST keeps the old flow.

## Numbers (bot `holds`, AI TEST)

| Kind | Right hand from Invector's grip, before / after | Left hand from the grip on leftHandIK, before / after |
|---|---|---|
| soldier_rifle, soldier_shotgun | 96.5 / 0.0 deg (gun 0.050 / 0.000 m off) | 99.0 / 0.0 deg (wrist 0.069 / 0.000 m) |
| soldier_gas, soldier_grenades | 104.4 / 0.0 deg | 94.3 / 0.0 deg (wrist 0.075 / 0.000 m) |
| guard_pistol, guard_shield | 63.5 / 0.0 deg | one hand |
| labworker_pistol | 54.0 / 0.0 deg | 53.0 / 0.0 deg |
| power_armour_guns | 52.5 / 0.0 deg | 27.8 / 0.0 deg |
| power_armour (launchers) | 60.0 / 0.0 deg | 22.1 / 0.0 deg |
| toll_guard_pistol | 47.0 / 0.0 deg | one hand |
| soldier_pistol, soldier_shield | 102.8 / 0.0 deg | one hand |
| labworker_knife | blade to forearm 37.6 / 0.0 deg | - |

- The barrel against the aim: under 2 degrees for every kind, before and after.
- The chest turns 35 to 40 degrees to the aim for the humans, and 53 to 59 for the armour. The vendor aim clip holds
  the gun across the body and ahead of a bladed stance.

## Still open for Bruno

- **The power armour bends forward** to aim at her: the 2.9 m body, its hands at chest height. The vendor guns are small
  in its hands. A bigger gun model or a pose of its own would help.
- **The knife:**
  - The hand cannot close more: there are no finger bones. The sculpted half-closed hand is the grip.
  - A real hammer grip has the handle across the palm and the blade out from the thumb side. The rule "blade along the
    forearm" gives a straight line, not that grip.
  - The knife (the vendor short sword at 0.32 m) is thin and hard to see.
- **One-hand pistols:** the left hand of the shield figures holds the shield's inner side by IK. The shield does not
  follow the hand. It stays where `EnemyShield` puts it.
- **The low ready keeps the procedural arm** (the reach), so the wrist bends to keep the grip.

## Tests

- EditMode `CharacterImportTests` 45/45.
- PlayMode `EnemyAiTests`, `ArmedCivilianTests`, `HumanShieldTests`, `RagdollTests`, `CharacterTests`, `BatonTests`,
  `CivilianEscortTests`, `EnemyMoraleTests`: 72/72.
- Bot `holds` in AI TEST: 14/14. On the code before option B, 13 of the 13 hold checks failed. Bot `holds` in
  LAB TEST: 2/2. Bot `ai`: 29/29.

Shots, before and after at the same angles, one sheet for each kind: `~/MEGA/glorified/renders/game/holds_B_2026-11-17/`.
