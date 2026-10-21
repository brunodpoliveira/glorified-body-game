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
- Every bone has translation keys in the clips (a baked rig). The walk and the run are NOT in place: the hips
  travel 1.53 m per walk cycle (2.33 s) and 2.95 m per run cycle (1.23 s) at 1 m scale, in a straight line
  (the residual under 8 cm). The idle stays on the spot, with a sway of 17 cm. Found by Bruno on the morning of
  2026-10-21 (section 7).

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
     action (idle, walk, run). The idle keeps its root in the pose. The walk and the run give their travel to
     the root motion (Root Transform Position XZ not baked, centre of mass), which the Animator does not apply,
     so they play in place. The texture at 1024, point sampling, mipmaps, no compression.
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
- `Lucy_WalksAndRunsInPlace_TheModelStaysOverHerRoot` (added in section 7): the shooter off, Lucy walks 2 s
  and runs 1.2 s across the floor, then stops. Every frame, the hips stay within 0.35 m of her root and the
  model root does not move. The root stays where she stopped. With the travel baked, the hips reached 2.6 m.
- PlayMode total: 50. EditMode: 27 cases.
- Run folders: the `RUNS` line at the end.

## 6. Bruno's notes of 2026-10-21, for later

1. The Invector FSM AI: an enemy moves OR shoots, never both at once, so the player keeps track of the enemies.
2. Props (the guns, the shield, the extinguisher): free assets, or Invector's placeholder weapons. No generation.
3. The psychic scan: interact with an object for a clue (scan a door, see a picture of its key, or a short
   movie). And logs: grab and read a placeholder log.
4. The fireman and the power armour stand in the room for the material test. They are not in the first pass of
   the demo.

## 7. The morning check (Bruno, 2026-10-21): the model slid

Bruno: "every time she walks into a direction and stops, her model slides back", "every time she starts
walking, the model also snaps forward then it goes back", "that was not happening with the capsules".

- The cause: the walk and the run travel in the source (section 1), and the import baked the root XZ into the
  pose. So the model walked 2.6 m ahead of the controller in one cycle, snapped back at the loop, and the blend
  to the idle slid it back when she stopped. The snap forward at the start: the blend tree syncs the clips by
  normalized time, so the walk started in the middle of its travel. The capsule had no clip, so it did not move.
  The controller itself stops at once (no inertia): only the model moved.
- The fix: `CharacterImportSettings` takes the travel out of the walk and the run (Root Transform Position XZ
  not baked, based on the centre of mass). The Animator has no root motion, so the body stays over the root.
  The idle keeps its sway in the pose. The sway of the walk goes with the travel (a small loss, placeholders).
- The proof: the test in section 5, and the chain in the `RUNS` line.

Bruno, after the fix: "ensure this animation glitch won't happen to the other models once we start animating
them as well". The first fix decided by the clip name (walk, run). A future clip with another name that travels
(a sprint, a patrol, a crawl) would bake again. Now the decision comes from the data:

1. `tools/characters_clips.py` (a step of `tools/characters.sh`) reads the animated GLB and evaluates the hips
   through the node chain (the armature, the root bone, the hips) at every key time. `travel` = the net move
   on the floor over the clip, `reach` = the largest move from the start. A clip travels when travel is 0.1 m
   or more (source units, the figures are 1 m tall). It writes `<class>.clips.json` next to the FBX. The nine:
   the idle travel 0.000 (reach 0.15 to 0.18, the sway), the walk 1.38 to 1.66, the run 2.65 to 3.19.
2. `CharacterImportSettings` reads that file for each clip. Without the file, the names decide (walk, run) and
   the import logs a warning. `BuildAll` logs the decision per class ("idle stays, walk travels 1.53, ...").
3. `CharacterImportTests` (EditMode, 3 tests): every FBX under `Models/` has its measurement, every clip of the
   import follows it (root XZ to the root motion when it travels, baked when it stays), the measured clips are
   all in the import, no prefab applies root motion, and the lucy_bare file flags the walk and the run, not
   the idle. A new class without its file fails here.
4. `EveryClass_PlaysItsLocomotionInPlace` (PlayMode): each of the nine prefabs plays its idle, walk and run
   across one cycle (`Animator.Play` at 21 normalized times, `Update(0)`), the hips within 0.3 x height of the
   root, no net travel over the cycle (under 0.1 x height), the model root still. A baked walk gives 1.5 x
   height.

What this does not cover: a clip in a state outside the Locomotion tree (a future patrol or a crawl) gets its
import setting from the measurement (item 2 and 3), but the PlayMode sampling only plays the three locomotion
speeds. Add the state to the test when the clip arrives. A clip that walks in a circle (no net travel, a large
reach) stays baked: none in T19.

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

RUNS (final, the measured root motion of section 7): `runs/20261021-150303-exec-34377` (the rebuild reads the
nine `clips.json`, CLEAN, "idle stays, run travels 2.95, walk travels 1.53" per class),
`runs/20261021-150356-test-editmode-34909` (30/30, CLEAN), `runs/20261021-150423-test-playmode-35181` (51/51,
CLEAN), `runs/20261021-150737-play-35730` (bot 24/24, CLEAN, 19 shots).
The in-place fix by name: `runs/20261021-142811-exec-29643` (the rebuild with the new
clip settings, CLEAN), `runs/20261021-142842-test-playmode-29995` (50/50, CLEAN),
`runs/20261021-143144-play-30574` (bot 24/24, CLEAN, 19 shots). The run before the fix,
`runs/20261021-124328-exec-504981`, stopped with the PC freeze of 12:59 (a memory thrash into the swap, 4.8 GB
free at the start: the rebuild runs with `GB_MIN_AVAIL_MB=6000` from now on).
Before the fix: `runs/20261021-013940-exec-417786` (the character build and the scenes, CLEAN: 9 of 9 classes, 22
human bones mapped per class), `runs/20261021-014545-test-playmode-419820` (49/49, CLEAN),
`runs/20261021-015032-play-421270` (bot 24/24, CLEAN, `cast`: lucyHuman=True dummies=2 cast=6, the shooter
rests for the two line-up shots). Earlier: `runs/20261021-014855-play-419819` (24/24, the laser across the east
shot), `runs/20261021-014023-test-playmode-418305` (49/49), `runs/20261021-014326-play-417785` (24/24, the
single line-up shot cut two figures off, the marker on the face, max frame 1,738 ms on the first skinned
frames). The first chain: `runs/20261021-013057-exec-414842` (CLEAN), `runs/20261021-013452-test-playmode-416686`
(47/49: the power armour 14 cm in the air with the guard's idle, the charge check during the roll clip).
SHOTS: `~/glorified-body-renders/game/characters/` (19 bot shots, `cast_lineup_west.png` and
`cast_lineup_east.png` are the look check, `play_training_room.png` the whole room).
