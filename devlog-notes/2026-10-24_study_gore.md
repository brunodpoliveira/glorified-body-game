# 2026-10-24, study: the Gore System URP (dismemberment and the other systems)

Bruno (2026-10-24): "we should also study gore urp's dismemberment and other gore subsystems; not sure how feasible
it is to implement it right now". Then: "the head burst is for shock enemy kills; mk_warspite should dismember some
part of the enemy's body".

Short answer: the full vendor dismemberment needs cut models per class and a ragdoll (16 to 24 h). The cheap path
(shrink the bone, put the vendor bone ends and meat on the cut) costs 2 to 3 h, and it is the one built today (see
`2026-10-24_ragdoll.md`). The ragdoll that the study asks for (question 7) is built too, and so is the pool under a
dead body (section 4). The study text follows as the agent wrote it, before that work.

## 1. The vendor gore systems

Vendor folder: `/home/pc/MEGA/code/glorified-body-game/Assets/ThatIndividual STORE ASSETS/GoreSystemsByThatIndividual/`. The vendor guides are the two PDFs in that folder ("Unity GoreSystems.pdf" and "Unity GoreSystems-Update1_1.pdf"). The vendor has no x-ray system.

| System | What it does | Main files | What a character needs | Used today? |
|---|---|---|---|---|
| Deep wounds | A shot paints a mask into a texture, and the gore shader cuts the skin away to show organ meshes | `Scripts/shootHitSender.cs`, `ShootDismembermentAddon.cs`, `PaintDecal.cs` (MIT, Dalton Nell), `Shaders/goreMaskShaderURP.shadergraph`, `goreMaskClothesShaderURP.shadergraph` | A second UV map, a rendering layer mask, `goreCutoutSender` on the root and on the limb bones, colliders tagged "Enemy", `DecalableCollider`, the vendor shaders on our materials | No. SkinGore (MIT, `/home/pc/MEGA/code/glorified-body-game/Assets/SkinGore`) does this job, but no enemy has a `SkinGoreRenderer`, so it does nothing now |
| Organs and stumps | Bone, rib and meat meshes, turned on at random by `goreAndBoneRands` | `Prefabs/goreHead-prefab`, `goreTorso-prefab`, `goreLimb-prefab` (each also "-withMuscles"), `Models/human internal organs.fbx`, `extra gore 3D models.fbx` | You place them at the stump | No |
| Slicing, "preCut" | Hides a body-part mesh, starts the ragdoll, spawns a ready-made cut piece | `Scripts/SkeletonMeshSlicer.cs` (`premadeGoreParts`), `goreCutoutSender.cs`, `Prefabs/soldier-PreCut-*`, `soldier-cut-legLower-L/R` | One skinned mesh per body part, a ragdoll, cut pieces made in Blender | No |
| Slicing, "real" | Cuts the skinned mesh along a plane at run time, on worker threads | `SkeletonMeshSlicer.SliceByMeshPlane`, `SliceMeshGenerator.cs`, `SliceMeshDatas.cs`, `SliceWeaponController.cs` | Read/Write on the mesh, a CharacterJoint ragdoll, a "Leg" tag, a cap material | No |
| Footprints | Bloody footprints on the floor | `footprintDecalSpawner.cs`, `URP Decal footprint*` | A URP DecalRendererFeature. None of our renderers has one | No |
| Floor blood | Paints blood into the floor texture | `groundHeightBlendedBlood.shadergraph`, `DecalShooter.cs`, `PaintDecal` | Floors with that shader and `DecalableCollider` | No |
| 3D splashes | Alembic blood meshes, 3 to 13 MB each | `Prefabs/3Dsplash1..10`, `Models/splash*.abc` | The Alembic package (it is in our manifest) | No. They destroy themselves after 1 s, so we cannot reuse them from a pool |
| Particles and decals | Hits, splatters, gushes, pools, drips, meat chunks, skull parts | `Prefabs/Particle System-*`, `URP Decal *` | Nothing | Yes: 22 cleaned copies in `/home/pc/MEGA/code/glorified-body-game/Assets/_Game/Prefabs/Gore/ThatIndividual/Effects/` |
| Sounds | 22 CC0 wav files (bone snaps, wet sounds) | `Sounds/` | Nothing | No |

## 2. Full dismemberment on our enemies

Our enemies are the T19 models: one mesh of 2,500 triangles, a 23-bone mixamo rig, Humanoid import, `isReadable = false` (`/home/pc/MEGA/code/glorified-body-game/Assets/_Game/Scripts/Editor/Characters/CharacterImportSettings.cs:107`).

`InvectorEnemyFactory.cs` adds no ragdoll. `InvectorEnemyBody` plays the death animation and then freezes the body.

Our `DismembermentController` and `DismembermentZone` came from Warspite. There they were only a test (a "Core" zone, a "Head" zone and a debug cube), and no enemy here uses them.

**PreCut path, 16 to 24 h:**
1. A Blender step in `tools/characters.sh` that splits each class into about 10 skinned parts on the same rig.
2. Cut pieces with filled caps.
3. A ragdoll on each enemy.
4. `premadeGoreParts` wiring for 9 classes.
5. Our own "cut part N" code. The vendor runs the preCut cut only from its raycast and input code in `ShootDismembermentAddon`, with no separate API.

Risks: visible seams at 2,500 triangles, and the clothes on each class.

**Real slicer path, 8 to 12 h, plus 3 to 5 h for the ragdoll. I do not recommend it.**
- The plane has no edge, so a cut at the elbow can also cut the torso.
- Each cut copies the whole bone tree with `Instantiate`. That copies our components on the bones (and the Invector hit boxes, once a ragdoll is on).
- Each cut builds MeshColliders, uses LINQ and the thread pool, and allocates. At 2,500 triangles the mesh cost is small. The GC hitch at the busiest moment is the real risk on the GTX 1050.
- VRAM is not the problem. `GoreVendorImportSettings.cs` already caps the vendor flipbook textures at 2048.

## 3. Cheap path for the mk_warspite limb cut, 2 to 3 h

This is the HeadStump method:
1. When a fatal `PsychicBlast` hit at full charge arrives, pick one limb bone with `animator.GetBoneTransform`: `LeftLowerArm` / `RightLowerArm` (forearm), `LeftUpperArm` / `RightUpperArm` (whole arm), or `LeftLowerLeg` / `RightLowerLeg` (shin). The name lookup works on any Humanoid rig.
2. Shrink that bone to 0.01 and hold the scale in `LateUpdate`, as `HeadStump` does. Make a general `BoneStump`.
3. Parent the vendor `Prefabs/goreLimb-prefab.prefab` to the parent bone at the joint. It holds bone ends and meat meshes, and its `goreAndBoneRands` (`useOnEnable`) picks them at random.
4. Spawn sprays at the joint: `PF_TI_gushingBlood_insane_small` (the vendor demo uses this for its cut gush), `PF_TI_meat_chunks`, and `PF_TI_BloodHit_classic_Huge`.

The vendor has no generic severed-limb piece. `soldier-PreCut-foreArm-L/R`, `soldier-cut-legLower-L/R` and `soldier-PreCut-legsKneeUp` are the vendor soldier's own mesh (`Soldier PreCut parts.fbx`, `Soldier-demoScene.mat`). They are placeholders only. With no piece, the blast turns the limb to pulp, which fits a psychic blast.

**Optional flying piece, plus 3 to 4 h:** before the shrink, bake the skinned mesh. Keep the triangles whose main bone is in the cut limb, and give the piece the same material, a capsule collider and a Rigidbody. Put `goreLimb-prefab` on the open end. This needs `isReadable = true`, which adds about 100 KB per class.

**Two code changes are needed:**
- `HitInfo` (`/home/pc/MEGA/code/glorified-body-game/Assets/_Game/Scripts/Combat/HitInfo.cs`) does not record the charge. Add a field, set in `PsychicPowers.FireAt` (`/home/pc/MEGA/code/glorified-body-game/Assets/_Game/Scripts/Player/PsychicPowers.cs:226`). Damage alone cannot show a full charge, because the MK boost doubles it.
- The future ragdoll must switch off the collider and joint of the cut limb, or the hidden limb will still move.

## 4. Blood pool under a dead body

The vendor pools are `Prefabs/Particle System-bloodPool1..5` and `Particle System-bloodPool Procedural` (one particle that lives 50 s). The vendor spawn call is `goreCutoutInteractorManager.BloodPool(pos)`, but `bloodPools` is empty in `goreInterractorsCode.prefab`.

We have them as `PF_TI_bloodPool1..3` in `floorStains`. They do not make a body pool:
- The driver places them at the hit point, not under the body.
- They live only 6 s in a ring of 24.

**Fix, 1.5 to 2 h:**
1. When `LayDown` ends, cast a ray down from the hips.
2. Put a quad there with `M_TI_Stain_pool1..3` (already in git, our unlit stain shader).
3. Grow it from 0.3 to 1.5 m over about 4 s.
4. Keep it while the body stays. Cap the number at about 8.

## 5. Vendor files and git

The tracked `PF_TI_*` prefabs are prefab variants that hold only our changes. The `M_TI_*` materials only point to vendor textures by GUID. No vendor files are in git.

Our README and the builder that made these prefabs are still in Warspite: `/home/pc/MEGA/code/project-warspite/Project Warspite2/Assets/Scripts/Editor/Gore/ThatIndividualGoreBuilder.cs`. A new copy (for example a gush variant) needs that builder ported here, because of rule 6.

## 6. Open questions for Bruno

1. `GruesomeDeathFx.BurstsTheHead` bursts the head on telekinesis, explosion, grenade and rocket kills too, not only on the short. Do we keep the head for the short only? Then those kills need a different effect (a limb cut?).
2. Pulp only (2 to 3 h), or a flying piece too (5 to 7 h)?
3. Which limbs: forearm, whole arm, shin? Random, or the side that faces Lucy?
4. Must the Blast kill at full charge to cut, or does a full-charge hit that does not kill also cut?
5. Gore Low: no cut, or the cut without the meat?
6. Body pools: do they stay for the whole room, and how many at most?
7. The ragdoll for mk_orzabal: build it now, before the cut, so both are made once?
