# 2026-10-24: the gore after Bruno's answers (the flying arm)

The answers of Bruno to the gore study (`2026-10-24_study_gore.md`, section 6), evening of 2026-10-24:

1. The head burst: "tk bursts head, explosions dismember (same as gruesome warspite kill)".
2. "flying piece too".
3. "forearm or whole arm, randomized".
4. "blast kill at full charge to cut".
5. "gore low: no cut".
6. The body pools: "they stay for the whole room, no limits".
7. The ragdoll: "build it now before cut" (it was built in the morning, `2026-10-24_ragdoll.md`).

And on the three choices of the morning: "keep it" (a thrown or struck body stays down 2.5 s, the power armour
never falls). Answer 1 above changes the third choice: a telekinesis kill now bursts the head, it does not cut.

## What changed

| Kill | Before | Now |
|---|---|---|
| The short's shock | Head burst | Head burst |
| mk_orzabal (a throw, a slam, a returned bullet) | Limb cut | Head burst |
| mk_warspite, full charge | Limb cut | An arm comes off and flies |
| An explosion (a grenade, a rocket) | Limb cut | An arm comes off and flies |
| mk_warspite, part charge | Nothing | Nothing |
| Gore Low | No cut | No cut, no head burst, smaller pool |

- **The limbs:** a forearm or a whole arm, left or right, at random. The shins are gone from the list.
- **The flying piece** (`Gore/SeveredLimb.cs`): before the bone shrinks, the skinned mesh is baked in its pose of
  that frame. The triangles that the cut bone and its children carry (half the weight or more) become a mesh of
  their own, with the materials of the body. The piece gets a box collider and a rigidbody (about 1.5 kg for a forearm,
  3 kg for an arm). It flies away from her (from the centre for an explosion), with a spin. The vendor bone ends and
  meat sit on its open end, and a gush follows it for 3 s. It settles, freezes, and stays for the room. The physics
  root is on BodyPart (it hits the room and the bodies, not her). The mesh is on the body's layer, so it gets the
  character mark.
- **The meshes are readable now** (`CharacterImportSettings`: `isReadable = true`, about 100 KB per class). The
  piece needs the bone weights. The nine `.fbx.meta` files changed.
- **The returned grenade and rocket** hurt the enemies as explosion damage now (`Grenade`, `EnemyRocket`), not as
  telekinesis. Before, a returned grenade kill would burst the head. Rule 1 says an explosion cuts.
- **The body pools** have no limit now (`BodyBloodPool.MaxPools` removed).
- **One gush on the body** at the cut, not two. The piece carries its own.

## Proof

- PlayMode `RagdollTests` (5): a full blast kill cuts the forearm. The piece starts 2 cm from where the forearm was,
  flies 1.1 to 1.3 m away from her along her line in 0.3 s (two runs), and settles on the floor.
- PlayMode `GoreTests` (4): new `Gore_TheKillDecides_TheHeadOrAnArm`. A telekinesis kill bursts the head and cuts
  nothing. A grenade kill cuts an arm (the left upper arm in the last run, a piece of 266 triangles), and the piece
  flies. Gore Low cuts nothing. Plus the rules for every kill.
- The "ai" bot (29 steps): `gore_head_burst` (the slam kill), `gore_limb_cut` and `limb_flies` (a full blast kill
  of the gas grenadier: 170 triangles, flew 1.1 m, settled).
- Shots: `~/glorified-body-renders/game/ai/gore2/`.

## The textured sprays (Bruno, 2026-10-24, evening: "change it to the textured sprays")

- The Warspite port drew every spray with its flat comic shader (`GoreSprayUnlit`): a loud red mask, or a round
  drop with no texture. In a close shot it read as red cards.
- `Editor/Gore/GoreSprayLook.UseTexturedSprays` gives each `M_TI_Spray_` material the shader and the properties of
  its vendor material: the URP shader graphs with the blood flipbooks. The procedural drop (`bloodFlying 2`) takes
  the textured fly shader. The red mist takes URP Particles/Unlit with its picture. The materials stay ours, and
  they point at the vendor shaders and textures by GUID.
- The vendor sheets are wet mirrors (smoothness 1). Indoors they showed the blue default sky in the blood. The
  smoothness is capped at 0.55 (PROPOSED).
- Shot: `gore2/20c_arm_flies_textured.png` (before: `gore2/20c_arm_flies.png`).
