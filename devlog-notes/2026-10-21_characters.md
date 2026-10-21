# Devlog notes, the cast into the engine (2026-10-21)

The nine T19 characters (`~/glorified-body-renders/T19_characters/models/`) are in the training room: Lucy in the
nude outfit on the tank controls, the guard and the soldier as the two dummies, the other six along the north
wall for the look. Bruno's go of 2026-10-21, with his heights, and "put them all in the room now to test
materials". The look test (step 4) comes next, with this cast in it.

## 1. The files, as found

- Nine `<class>_game.glb`: one mesh of 2,500 triangles, one matte material, one base colour at 4096, a mixamo rig
  of 23 bones, 1.0 m tall, the figure faces glTF +x. The feet on the origin.
- Three game files carry no animation clips: `lucy_bare`, `lucy_guard` and `fireman`. The projection and the
  mirror steps of T19 dropped them. Every `<class>_animated.glb` has idle, walk and run on the same mesh (the same
  mesh node and vertex count), so the pipeline takes the mesh and the rig from the animated file and the base
  colour from the game file. No new tokens.
- Every bone has translation keys in the clips (a baked rig). Unity keeps the clips in place (the root position
  and rotation baked into the pose).

## 2. The pipeline: `tools/characters.sh`

1. `tools/characters_basecolor.py`: the base colour out of the game GLB as a 1024 px PNG (Pillow, Lanczos). The
   texture import caps at 1024 in any case (ART_LESSONS §3), so the repository carries 1 MB per class, not 7 to 16.
2. `tools/characters_export.py` in Blender 5.2 (through the RFab kit wrapper `kit/run_blender.sh`, the RAM cap):
   import the animated GLB, drop the importer's helper mesh, keep one mesh and one armature, rebuild the material
   with the base colour only (roughness 1, no specular), turn the figure from +x to -y and export the FBX with
   every action as a take. Unity shows it along +z.
   The turn goes into the DATA of the mesh and the armature (`Mesh.transform`, `Armature.transform`), with the
   corner normals turned too. The operator `transform_apply` does not do it: it applies the local transform of
   each selected object, and the parented mesh has none, so the first export kept the shoulders along y. The
   report line of each export shows the extents, and the Unity test checks the axis through the Humanoid bones.
3. Unity: `tools/unity.sh exec GlorifiedBody.Editor.Characters.CreateCharacterAssets.BuildAll`.
   - `CharacterImportSettings` (an AssetPostprocessor on `Assets/_Game/Characters/Models/`): the FBX as Humanoid
     with its own avatar (the mixamo names map by themselves), no materials, every clip a loop named after its
     action (idle, walk, run) and kept in place. The texture at 1024, point sampling, mipmaps, no compression.
   - `CreateCharacterAssets`: one URP Lit material per class (base colour, smoothness 0, metallic 0, no specular
     highlights, no reflections), the two animator controllers, one prefab per class (root > Model at
     height / 1 m, the material, the Animator, culling off), the catalog `Resources/Characters/catalog.asset`,
     then the scenes.
4. The scene builder (`MenuLoopSetup`) reads the catalog. Without it (a fresh clone before the build) the room
   gets no models and a warning.

The heights (Bruno, 2026-10-21): Lucy 1.70, patient and nurse 1.68, lab worker 1.78, guard, soldier and fireman
1.80, power armour 2.60.

## 3. The animators (placeholders, PROPOSED)

One controller per class from code (`Assets/_Game/Characters/Anim/<class>.controller`), in two shapes (the Lucy
set for the player classes, the Enemy set for the rest). Both: a 1D blend tree "Locomotion" on Speed (idle 0,
walk 0.5, run 1, the class's own clips), "WalkBack" (the walk at speed -1), and single-pose states with
Invector's humanoid clips: Dodge (`Roll`, at the pace of the 0.35 s dash), Charge or Aim (`Aiming@Pistol`),
Stagger (`Hit Small Front`), Airborne (`Falling`), Dead (`Death`). Without the Invector folder (a fresh clone)
those states play the idle, and the build log says so.
The first build shared two controllers, with the guard's clips on every enemy. The Humanoid retarget of the
guard's idle left the 2.6 m power armour 14 cm in the air (the test caught it), so each class plays its own.

- `LucyAnimator` (on Lucy): Speed from the tank controller (0.5 walk, 1 run, 0.3 the slow walk of the short),
  Back on the back step, Dodge on the dodge event, Charging from the lock-on, Dead from Health.
- `EnemyAnimator` (on a dummy): Aiming while the shooter holds a slot, Stagger on the stagger, Airborne in the
  air (telekinesis), Dead at death. No walk yet.

## 4. The room changed with the models

- Every character stands on its root (the feet at y 0). Lucy's root moved from y 1 (the capsule's centre) to the
  floor: the CharacterController is 1.7 m tall with its centre at 0.85. `TankController.Teleport` snaps the point
  down to the floor (a ray, triggers ignored), so the tests and the bot keep their old `y = 1` points.
- The body points live in `Characters.Body`: the chest at 1.2 m (the shooters aim there, the catch radius, the
  HUD ring, the lock point) and the muzzle at 1.3 m. Before: 0.9 on a 2 m capsule.
- The dummies: a root with a capsule collider the size of the class, Health 50, the lock point at the chest,
  the model, `EnemyAnimator`. Dummy_A the guard (the shooter), Dummy_B the soldier. Both face the room centre.
- The cast along the north wall at z 4.4, facing the room: power armour (x -4.4), Lucy guard (-3.2), patient
  (-0.4), nurse (0.8), lab worker (2.0), fireman (3.2). Visual only: no Health, no collider.
- The camera zones reach below the floor now (centre y 1, height 3), since they test her root.
- The nose marker on the capsule is gone. The player object is "Lucy", no longer "Lucy_Placeholder".

## 5. Proof

- `CharacterTests` (4 PlayMode tests): the catalog has the nine classes, each a valid Humanoid at its height, the
  feet on the root, the right arm at +x, the toes at +z, a URP material with the base colour, matte, the clips
  idle, walk and run in the controller. Lucy wears the model, her controller is her height, the animator follows
  the walk, the run, the back step, the dodge and the charge. Dummy_A wears the guard, its collider and lock
  point fit, the aim pose plays when it aims. The six cast figures stand along the north wall in idle, the power
  armour over 2.3 m.
- The bot step `cast` (after `gameplay_ready`): Lucy's Humanoid animator, the two dummies with models, the six
  figures. `cast_lineup_west.png` and `cast_lineup_east.png` from a camera 4 m in front of the line-up at eye
  height, three figures each (the first single shot cut the power armour and the fireman off). Bot total: 24.
- The red lock marker floats 0.9 m over the chest point now (it sat on the guard's face at 0.45, the first shot
  showed it).
- PlayMode total: 49. EditMode: 27 cases.
- Run folders: the `RUNS` line at the end.

## 6. Bruno's notes of 2026-10-21, for later

1. The Invector FSM AI: an enemy moves OR shoots, never both at once, so the player keeps track of the enemies.
2. Props (the guns, the shield, the extinguisher): free assets, or Invector's placeholder weapons. No generation.
3. The psychic scan: interact with an object for a clue (scan a door, see a picture of its key, or a short
   movie). And logs: grab and read a placeholder log.
4. The fireman and the power armour stand in the room for the material test. They are not in the first pass of
   the demo.

## Files

`tools/characters.sh`, `tools/characters_basecolor.py`, `tools/characters_export.py` (new),
`Assets/_Game/Characters/` (the FBX, the base colours, the materials, the controllers, the prefabs: new),
`Assets/_Game/Resources/Characters/catalog.asset` (new),
`Assets/_Game/Scripts/Characters/{CharacterCatalog,LucyAnimator,EnemyAnimator}.cs` (new),
`Assets/_Game/Scripts/Editor/Characters/{CharacterImportSettings,CreateCharacterAssets}.cs` (new),
`Assets/_Game/Scripts/Editor/MenuLoopSetup.cs`, `Assets/_Game/Scripts/Player/TankController.cs`,
`Assets/_Game/Scripts/Player/PsychicPowers.cs`, `Assets/_Game/Scripts/Enemy/DummyShooter.cs`,
`Assets/_Game/Scripts/Enemy/EnemyProjectile.cs`, `Assets/_Game/Scripts/Automation/StructuralPlaythrough.cs`,
`Assets/_Game/Tests/PlayMode/CharacterTests.cs` (new), `README.md`, `CLAUDE.md`.

RUNS (final): `runs/20261021-013940-exec-417786` (the character build and the scenes, CLEAN: 9 of 9 classes, 22
human bones mapped per class), `runs/20261021-014545-test-playmode-419820` (49/49, CLEAN),
`runs/20261021-015032-play-421270` (bot 24/24, CLEAN, `cast`: lucyHuman=True dummies=2 cast=6, the shooter
rests for the two line-up shots). Earlier: `runs/20261021-014855-play-419819` (24/24, the laser across the east
shot), `runs/20261021-014023-test-playmode-418305` (49/49), `runs/20261021-014326-play-417785` (24/24, the
single line-up shot cut two figures off, the marker on the face, max frame 1,738 ms on the first skinned
frames). The first chain: `runs/20261021-013057-exec-414842` (CLEAN), `runs/20261021-013452-test-playmode-416686`
(47/49: the power armour 14 cm in the air with the guard's idle, the charge check during the roll clip).
SHOTS: `~/glorified-body-renders/game/characters/` (19 bot shots, `cast_lineup_west.png` and
`cast_lineup_east.png` are the look check, `play_training_room.png` the whole room).
