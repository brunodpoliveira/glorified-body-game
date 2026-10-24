# 2026-10-24, study: breakable cover and breakable scenery

Bruno (2026-10-24): "we should study breakable cover, for more kinetic scenarios (different from the throwable
props). stuff like greebles on tables, the table itself, wall plaster coming down, that sort of thing (and breakable
lights if possible; makes powers and the fights more kinetic); there is a demo of this called
experimental_arena_destruction; warspite also has code for it". "study =/= implementation": nothing here is built.

Short answer: Bruno's own code (the arena demo and Warspite) covers the table, the partition and the falling hazard,
with OpenFracture (MIT) for the fracture. No codebase breaks a light: that part is new work. About 32 h in total.

## What exists

**1. Experimental Arena Destruction** (`/home/pc/MEGA/code/Experimental_Arena_Destruction/`). This is Bruno's own repo, GPL-3.0. Its code is in `Assets/_ExperimentalArena/Scripts/Destruction/` (761 lines) and its notes are in `Assets/_ExperimentalArena/Docs/DestructionSpikeNotes.md` and `OpenFractureEvaluation.md`. It uses authored states: each object has intact, damaged, destroyed and debris children, and the script turns them on and off. The table gets a physics push when it breaks. A falling hazard (a chandelier) drops on a hit and hurts what it lands on. A partition swaps to rubble and opens the sightline. Damage comes in through a hit bus listener. A reset service puts everything back. It has no pooling, no sound and no decals. The OpenFracture trial there never got a runtime test.

**2. Warspite destruction** (`/home/pc/MEGA/code/project-warspite/Project Warspite2/Assets/Scripts/Destruction/`, 2647 lines, Bruno's code). This is the mature version of the Arena code.
- `DestructibleObject.cs`: HP, the authored states, damage gates per damage type, an optional break effect prefab, a debris timer, a reset.
- `DestructionDamageReceiver.cs`: subscribes to `HitEventBus.OnHit` and accepts a hit only when `HitTransform` or `HitCollider` is under its object. When a hit carries no damage it uses a fallback value.
- `OpenFracture/OpenFractureAdapter.cs`: the fracture is made at runtime. At most 2 fractures per frame, and the others wait for the next frames. It does one warm-up fracture at load, because the first call costs about 25 ms of JIT. Fragments get a push away from the hit.
- `OpenFracture/OpenFractureDebrisCleanup.cs`: fragments fly for 2.5 s, then freeze (kinematic, no collider). They shrink away at 6 s and the script frees the runtime meshes. A global cap removes the oldest groups above 160 live fragments. Fragments go on the "Pushable" layer.
- `BreakableTable.cs` and `BreakablePartition.cs` are thin wrappers. `FallingHazard.cs` shows a drop marker on the floor. `PropImpactDamage.cs` lets a thrown object damage a breakable. `DestructionProbe.cs` measures the cost.
- Measured on the DestructionTest scene: 6 breaks in one frame made 88 fragments. Worst frame 20.9 ms, average 3.0 ms, at most 5 ms per fracture.
- It has no pooling (the effect is `Instantiate` plus `Destroy`) and no break sounds.

**3. OpenFracture** (Daniel Greenheck, MIT, a git UPM package). License is in `.../Project Warspite2/Library/PackageCache/com.dgreenheck.openfracture@6d0ddff64173/LICENSE.md`. MIT is compatible with GPL, so it can enter our repo. Its limits: closed, non-intersecting meshes only, one submesh, no skinned meshes, no local fracture at the hit point. It also has `Prefracture`, which makes the fragments in the editor. The Asset Store folder holds only Invector, MIS and Gore System, and none of them does destruction.

## What we can port, by example

- **The table:** port `DestructibleObject`, the receiver and the debris cleanup. Use `Prefracture` (or Blender Cell Fracture in `tools/`, like `tools/characters.sh`) so the break costs no CPU at runtime. Rule 6 is kept, because editor code makes the assets. The intact table blocks bullets and enemy sight on Default. It needs a `NavMeshObstacle` (carve) that turns off when the table breaks. RFab/Meshy models are often not closed meshes, so test them first.
- **Table greebles:** these need no fracture. Cups, trays, files and monitors are kinematic Rigidbodies that wake up on a hit or a blast near them. They fly, settle, sleep, and keep a cap on their number. Glass items swap to a pooled shard burst. This is new code, but it is small.
- **Wall plaster:** do not fracture the walls. They are static and lightmapped, and OpenFracture cannot cut them. Put thin plaster patches over the wall (dynamic, lit by probes). A hit drops 3 to 6 pre-cut chunks and leaves a bare-concrete decal on the wall. The Arena's authored-state code fits this.
- **Lights:** we must build this. A `BreakableLight` makes sparks and flickers about 0.3 s. Then it turns the `Light` off, swaps the emissive material to off, and drops glass bits. The bake problem: our lights are `LightmapBakeType.Mixed` with `MixedLightingMode.IndirectOnly` (`Editor/LookTestSetup.cs` lines 179, 1139, 1158). The bounce of a light that breaks stays in the lightmap, so the walls keep a glow with no source. My proposal: make breakable lights Realtime so the bake leaves them out, and fake their bounce with one weak point light with no shadows that dies with them. The other way is a second baked lightmap set per room, which costs a lot.

## Hook points in our code

- Our `HitEventBus` and `HitInfo` have the same shape as Warspite's. The Warspite receiver needs only a namespace change and a `DamageType` mapping (`PsychicBlast`, `PsychicFire`, `PsychicTelekinesis`, `ShortShock`, `EnemyRocket`).
- `Player/PsychicPowers.cs`, `FireAt`: the Blast case (lines 224–227) and the full-charge Fire case (234–240). A power never misses, because it hits the lock target directly. So add collateral damage: a sphere at `point`, plus the line from her to the target.
- `Player/PsychicPowers.cs`, `Release` line 159: a prop now gives `no_target` for every power except mk_orzabal. If a light must be a Blast target, that rule changes.
- `Combat/TelekinesisLift.cs`, `Fly`: the wall raycast (about line 230). Today any collider without `Health` stops the flyer, so a breakable there should take damage and break. The landing in `Slam`/`Fly` should send a splash. `Break()` (about line 179) can switch from `PropPiece` cubes to the shared debris.
- `Enemy/EnemyProjectile.cs`, `Impact`: the room branch (`ShotEffects.Impact`, about line 236). Raise a hit there with `HitCollider`, so enemy bullets wear down cover.
- `Enemy/EnemyRocket.cs` `Burst` and `Enemy/EnemyGrenade.cs` `Burst`: add a radius splash on breakables.
- `Player/ShortController.cs`, `Shock`: this could make the lights near her flicker or pop.
- Pooling: `Pooling/PoolService.cs`, the same pattern as the decals in `Gore/BloodDecalSpawner.cs`.

## Performance risks (GTX 1050 2 GB, 4 cores)

- Runtime fracture costs about 5 ms each. Prefracture removes that cost.
- The bigger risk is shadow draws. Each fragment casts shadows from every hard spot that covers it, and a shadowed point light draws it 6 times. Turn shadow casting off on small debris and on all debris after it settles.
- Every fragment has its own mesh and MeshCollider. Keep a per-room cap of about 60 to 80, lower than Warspite's 160.
- Use a new "Debris" layer (a slot is free) instead of Invector's "Pushable".
- What the fixed camera shows: it is high and 5 to 12 m away. Items under 10 cm are a few pixels, so use large items (monitors, binders, bottles, lamps). Plaster only reads on walls that face the camera. The Authentic look dithers the debris, which is acceptable.
- Warspite's 6 s shrink-away looks "gamey" in a slow horror game. Small debris could settle and stay.

## Build order (rough hours)

1. Port `DestructibleObject`, the receiver and the cleanup into `GlorifiedBody.Destruction`, plus the Debris layer and EditMode tests: 4 h.
2. A breakable table with Prefracture through editor code, the NavMeshObstacle, and a PlayMode test: 5 h.
3. Hook the powers, the TK fly and slam, bullets, grenades and rockets, with tests: 5 h.
4. Greebles (wake, settle, cap): 4 h.
5. `BreakableLight` with the Realtime and fill-light setup, and a bake check: 5 h.
6. Plaster patches, chunks and a decal: 5 h.
7. Break sounds (no source yet: a CC0 pack, or an RFab gap row), effects, a tutorial step, a bot shot pass and the devlog note: 4 h.

Total: about 32 h.

## Open questions for Bruno

1. Does debris stay for the whole visit, and does a broken room stay broken when she comes back?
2. Can mk_warspite lock onto a light or cover, or is all destruction collateral damage?
3. Do enemy bullets wear down her cover?
4. For breakable lights: Realtime with a fake bounce, or two bakes?
5. Can a broken light make a room dark enough to need a mechanic (for example a scan, or a light source)?
6. Can Lucy use cover at all with tank controls, or does cover only shield enemies?
