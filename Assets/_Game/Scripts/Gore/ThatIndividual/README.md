# ThatIndividual gore driver (module 8)

Connects "Gore Systems by ThatIndividual" (vendor folder `Assets/ThatIndividual STORE ASSETS/GoreSystemsByThatIndividual/`, gitignored) to `HitEventBus`.

## How it works

- `ThatIndividualGoreDriver` subscribes to `HitEventBus.OnHit`, like `SkinGoreDriver`.
- A bootstrap adds `Resources/ThatIndividualGore/PF_ThatIndividualGore` to any scene that has a `GoreManager` and no driver. Levels need no scene edits.
- `ThatIndividualGoreRules.Classify` sorts each hit into Light, Heavy, Fatal or FatalHeavy:
  - Heavy types: `TimeDilationMelee` (L3 punch, finisher, lunge), `Divekick`, `Throw`, `Slam`, `SonicBoom`, `Explosion`, `Grenade`, `FallingHazard`, `PropImpact` and `Debug` (the bot's kill hit).
  - Other hits count as heavy at `heavyDamageThreshold` damage or more.
  - A non-fatal `AudioPulse` hit does not bleed.
- Effects for each weight:

  | Weight | Effects |
  |---|---|
  | Light | Spray, plus a wall stain sometimes |
  | Heavy | Big spray, floor stain, wall stain |
  | Fatal | Burst, large floor stain, wall stain |
  | FatalHeavy | Burst, big spray, meat chunks or skull parts, largest floor stain, wall stain |

- When the player is hit, the driver plays one small spray and no stains.
- Gore tier comes from `GoreManager` (`DA_GoreSettings_Default`):
  - Off: no effects.
  - Low: each weight drops one step, and the caps are 40 % of Full.
  - Full: all effects.
- `ThatIndividualGoreProfile.backend` chooses the backend:
  - `SkinGore`: skin wounds only.
  - `ThatIndividual`: sprays and stains only. The driver disables `SkinGoreDriver`.
  - `Both` (default): SkinGore skin wounds plus ThatIndividual sprays and stains. They do different jobs, so the effects do not double up.
  - The backend is on `DA_ThatIndividualGoreProfile`, not on `DA_GoreSettings_Default`, because the existing `Gore/` folder is read-only for this module. Designers set the tier in `DA_GoreSettings_Default` and the backend here.
  - `SkinGoreDriver` is one scene manager on `PF_GoreManager` (`SetupGoreIntegration`), not a per-enemy part, so the driver finds it when the backend changes.
- With `muteLegacyBlood` set, the driver sets the legacy `BloodFeedbackController` caps to 0, so the old procedural blood also cannot double up.

## Budget

- All effects come from `PoolService`. They live in two fixed rings: 24 effects and 24 stains. When a ring is full, it returns its oldest entry first. A fatal heavy kill takes 3 effect slots, so 8 kills in one frame fit.
- The fatal heavy extras (meat chunks, skull parts) are optional. When the next ring slot still holds a live effect, the driver skips the extras and does not cut off another kill's burst.
- The ring index math (`TakeSlot`, `PeekSlot`, `CapFor`) is in `ThatIndividualGoreRules`, with EditMode tests.
- The driver has its own `PoolService` on `PF_ThatIndividualGore` (`ThatIndividualGoreDriver.GorePool`). It is not the game's main pool. Code that looks up "the" pool with `FindFirstObjectByType<PoolService>()` (for example `DebugHUD` when its field is empty) can get this one. Give such code a serialized reference to the main pool.
- A hit makes no `Instantiate` call and no GC allocation. The only exception is a pool that has to grow beyond its prewarm count.
- Effects use scaled time, so blood hangs in the air during L3 slow motion.
- Lifetimes are short: 1.2 s for light, 2 s for heavy, 3 s for fatal and 6 s for stains. The arena stays readable.
- At startup the driver plays and stops every prewarmed instance once, far below the arena. This moves the first-Play cost of each particle system out of the first real hit.
- Measured in GoreTest, batch editor (`gore_stats` in `runtime.jsonl`): 0 bytes allocated per hit. Worst hit 3.0 ms (first hit of a weight). Warm hits: worst 1.7 ms, average 0.18 ms over 47 hits.
- Vendor textures: `GoreVendorTexturePostprocessor` (in `Scripts/Editor/Gore/GoreVendorImportSettings.cs`) caps every texture under the vendor folder at import time: max 2048 px, compressed, power-of-two scaling (BC needs sides that are a multiple of 4; the flipbooks are 9600x5760 and similar). It restores the importer settings after import, so the vendor `.meta` files stay as they are and nothing needs to be run by hand. `GoreVendorImportSettings.Apply` only forces a re-import (for a Library imported before the postprocessor).

## Assets (generated, do not edit by hand)

`WarspiteP2.Editor.Gore.ThatIndividualGoreBuilder.Build` makes these assets:

- `Prefabs/Gore/ThatIndividual/Effects/PF_TI_*.prefab`: prefab variants of the vendor effects. Each variant has these changes:
  - Vendor scripts removed.
  - Play on awake off.
  - Trickle emitters (rate 1/s over a long duration) emit one burst at once instead.
  - Spray puddle layers switched off. The driver places the stains.
  - Billboard spray layers and their trails: copies of their materials on our unlit `Shaders/GoreSprayUnlit.shader` (`Materials/M_TI_Spray_*`), saturated red from `sprayColor`. The vendor fly and flipbook shaders are lit and read as grey smoke or dark columns in our lighting. Mist layers use `mistAlpha`. Mesh particles (meat chunks, skull parts) keep the vendor material.
  - Stains: one quad that pops in, holds, and shrinks at the end. Material `Materials/M_TI_Stain_*` on our unlit shader `Shaders/GoreStainUnlit.shader` (the vendor stain shader is lit, near-black and needs tangents that particles do not give, so it renders black in our lighting).
  - Particle lifetimes clamped to the pool slot.
  - Hierarchy scaling.
  - Collisions only with the surface mask.
- `DA_ThatIndividualGoreProfile.asset`: the effect lists and tuning.
- `Resources/ThatIndividualGore/PF_ThatIndividualGore.prefab`: the driver and its `PoolService`.

## Vendor parts not used, and why

- **Mesh slicing (`SkeletonMeshSlicer`, `SliceMeshGenerator`, `goreCutoutSender`): skipped.** These are the reasons:
  1. A cut calls `Instantiate` on the whole bone tree of the rig. Our Invector enemy bones carry gameplay components: 11 `vDamageReceiver` hitboxes, `HitZone`, `DismembermentZone`, `vRagdoll` bodies and colliders. Each cut would copy these hit targets into a second, live copy.
  2. The slicer needs CharacterJoint ragdoll bones, a "Leg" tag, the vendor `goreCutoutSender` on the enemy root and on the bones, and the vendor gore shaders on the enemy materials. That means changes to the enemy prefabs, which only the lead can change.
  3. Each cut builds new meshes and MeshColliders at runtime, on a thread pool, with LINQ and List allocations. That causes a large GC spike and CPU spike on a GTX 1050 class machine, at the moment the screen is busiest.
  4. `DismembermentController` already removes limbs by zone on fatal heavy hits. A second limb system on the same bones would fight with it.
- **Skin wound shaders (`goreMaskShaderURP`, `PaintDecal`, `shootHitSender`):** SkinGore already does skin wounds. These shaders need a material swap and UV2 on the enemy meshes.
- **Alembic 3D splashes (`3Dsplash*`):** `alembicBloodEffectCode` destroys itself after 1 s, and the prefabs call `Instantiate` for decals on collision. They cannot be pooled without vendor edits.
- **Vendor demo scripts (FPS controller, example scene code, slice weapon):** these conflict with our input and camera. They are not referenced.
- **URP decal bleeds (`URP Decal bleed*`):** variants exist (`ThatIndividualGoreDecal` replaces the run-once `animatedBloodStarter`). They are used only when the active URP renderer has a `DecalRendererFeature`. `PC_Renderer` has none today, so walls get the flat vendor stain quads.

## Test scene

`WarspiteP2.Editor.Gore.GoreTestSceneBuilder.Build` makes `Assets/Scenes/Dev/GoreTest.unity`:

- One encounter of 5 enemies in a kill zone, with a backstop wall behind them.
- A gate and a `LevelEndTrigger`.
- A baked NavMesh.
- Shot points: `overview` and `killzone_closeup`.
- A `GoreTestShotRig`. In bot runs it renders the kill zone after the first fatal burst. Then it checks the driver: every weight (Light, Heavy, Fatal, FatalHeavy) under every tier (Full, Low, Off), 12 fatal heavy kills in one frame, a player-victim hit, and each backend. It asserts the played weight, the caps and the SkinGoreDriver state, and sends `gore_matrix` (checks and failures) and `gore_stats` (hit cost). A failure logs an error, so triage marks the run DIRTY. It calls `ThatIndividualGoreDriver.PlayForCheck` directly, not `HitEventBus`, so score and hit text do not see the test hits.

Commands (from the worktree root):

```
tools/unity.sh exec WarspiteP2.Editor.Gore.GoreVendorImportSettings.Apply
tools/unity.sh exec WarspiteP2.Editor.Gore.ThatIndividualGoreBuilder.Build
tools/unity.sh exec WarspiteP2.Editor.Gore.GoreTestSceneBuilder.Build
tools/unity.sh exec WarspiteP2.Editor.Gore.GoreLookdev.Capture
tools/unity.sh play GoreTest Dev/GoreTest structural 300
tools/unity.sh shots Dev/GoreTest
tools/unity.sh test editmode WarspiteP2.Editor.Gore.Tests
```

The scene is in `Assets/Scenes/Dev/`, so the start scene argument is `Dev/GoreTest`. The structural bot also checks for a boss and a results screen. GoreTest has neither, so `boss_present` and `results_shown` fail there by design.
