# 2026-11-17: the guard's thin neck was a stale avatar

Bruno, with a shot of Guard_B: "that guard has a REAL thin neck; the fuck is going on here?"

## Cause

The round 184 guard (`bd3c99f`) and the round 185 power armour (`de89b71`) replaced their FBX files under the same
node names. Unity keeps the reference pose of a Humanoid avatar in the `.meta` on a reimport.
`CharacterImportSettings.AvatarIsStale` compared only the node names, so it did not map these two avatars again.
The Humanoid then posed the new bodies with the bone offsets and turns of the old models.

Measured in the class's own idle at 0.5 s (the angle of the shoulder bones from the FBX rest):

| Class | Before | After |
|---|---|---|
| guard | 178.9 degrees (the top of the shoulders 3 to 5 cm low: the neck shows long and thin) | 4.6 degrees |
| power armour | 86.0 degrees (the top of the shoulders 4.7 cm high) | 3.8 degrees |
| the other nine classes | 3.7 to 6.6 degrees | the same |

The bone lengths in Unity match the Blender file (the guard: Neck to Head over UpperChest to Neck, 0.496 in both).
The A-pose rigs are not the cause: the soldier and seven other classes were correct.

## Fix

- `AvatarIsStale` also compares the reference pose with the FBX: a bone more than 1 % off its node is stale
  (`ReferencePoseDiffers`). Positions only: a fresh avatar keeps every offset (0.0 %), but Unity's own T-pose turns
  the arms by up to 67 degrees, so a rotation limit would flag every fresh avatar. The hips are skipped.
- `CreateCharacterAssets.BuildAll` mapped the guard and the power armour again (the log names the bone: Root 15.4 %,
  LeftUpLeg 20.5 %). `BuildT49EnemyPrefabs` built the enemy prefabs again.
- Tests (EditMode, `CharacterImportTests`): `EveryCharacterAvatar_KeepsTheReferencePoseOfItsFbx` (failed first on
  the guard), `EveryOwnIdle_KeepsTheShoulderBonesNearTheirRest` (limit 20 degrees, logs every class),
  `TheReferencePoseCheck_SeesAMovedBone`.

## Proof

- EditMode: 45/45 in the `CharacterImportTests` run. The full EditMode run (before the shoulder test): 124/125, and the one failure is
  `BarkBookTests` (the writer's bark file changed outside this work; `BarkBookSetup.Build` copies it).
- PlayMode: `CharacterTests`, `RagdollTests`, `EnemyAiTests`, `ArmedCivilianTests`, `HumanShieldTests` 61/61.
- Bot `lucy`: 66/66. Bot `holds`: 5/5.
- Shots, before and after: `~/MEGA/glorified/renders/game/neck_fix_2026-11-17/`.

The same fix does not repair the gun holds. It removes the turned shoulders and the crushed vest of the guard and the
armour in every pose, but the hands of the other classes stay as they were.
