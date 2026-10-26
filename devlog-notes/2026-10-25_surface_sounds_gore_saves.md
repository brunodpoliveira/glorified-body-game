# 2026-10-25, late night: the surface sounds, and the bodies and the gore in the saves

Bruno's words:

1. "what is this ricochet sound from the pistol? it sounds like a hammer hitting a chisel. sounds like metal-on-metal.
   is the material of the wall assigned to be a metallic one?" Then: "aren't surface sounds already in the pack?"
2. The saves: a manual save and a load count as the checkpoint: "keep". After a load the dead "stay on the floor;
   destruction and gore should be saved as well if possible". A tutorial save step: "no need".

## 1. The surface sounds

The fact check:

- The walls are not metal. The game had no surface sounds. Every bullet hit played the same three clips on every
  wall, floor and desk.
- The clips were Invector's `Knife Hit Wall 1-3.wav` (the melee pack): a steel blade on a wall. Clip 2 had 78 % of its
  energy over 4 kHz. I picked them on 2026-10-25, when I removed the ricochet clip that opens with a gunshot.
- The packs: Invector has five impact prefabs (concrete, metal, wood, glass, dirt), but all five play the same
  `pistol-shot-ricochet.wav`. The gore pack has flesh sounds only. The Asset Store cache has no other sound pack.

The fix:

| Surface | Materials (by name) | Particles | Sound |
|---|---|---|---|
| Concrete (the default) | concrete, plaster, tile, the greybox, any name not in the list | `impactConcrete` | `concrete_hit_0-3`, made in code: a crack, a thud, grit, no ringing tone |
| Metal | steel, grille, gear, conduit, door, light panel | `impactMetal` | Kenney `impactMetal_light` |
| Wood | plywood, laminate, crate, desk | `impactWood` | Kenney `impactWood_light` |
| Glass | glass, mirror, monitor, lamp, skylight | `impactGlass` | Kenney `impactGlass_light` |
| Soft | mattress, leather, canvas, soil, plants, cardboard | `impactDirt` | Kenney `impactSoft_medium` |

- `Surfaces.Of(collider)`: an intact breakable gives its kind (plaster is concrete, plastic is wood). Else the material
  name decides. The concrete words come first: `GB_ConcretePlanter` is not a plant.
- One sound for one hit: a bullet on an intact breakable plays the chip of its surface, and the impact stays silent.
  Before, the generic chip and the knife clip both played.
- Kenney's "Impact Sounds" 1.0 is CC0 (`Assets/_Game/Audio/Impacts/Kenney_License.txt`), so it is in git.
  `tools/impact_sfx.py <kenney folder>` makes the concrete clips and copies the Kenney files.
- All are placeholders until the final sound effects.
- I checked every material in the game scenes against the table. Each room material gets the right surface.

## 2. The bodies and the gore in the saves

- A save keeps each dead body: the root, the humanoid bones, and whether it lay still.
- It keeps the stumps: a burst head, a cut arm.
- It keeps a shield that fell, the cut arms on the floor, the floor stains and the pools under the bodies (`RoomGore`).
- A load kills the figure again, quietly:
  - No one sees or fears the death (`EnemyMorale.MarkOldDeath`).
  - No one tends the body (`CorpseCare.MarkTended`). Bruno, 2026-10-26: "keep it".
  - No pool grows, and no spray flies. The particles of a restored stump stay still.
- Then the bones take the saved pose. A ragdoll freezes in it, or it falls from that pose if the save caught it in a
  fall or a drag.
- Each quiet death counts a kill. Her saved kills and SHORT overwrite the count.
- A cut arm is cut again from the body at the load. The piece turns so that it carries its bone as the saved piece did.
  The bend at the elbow or the wrist can differ a little.
- The Gore option of the moment applies: Off puts back the bodies and no blood. Low puts back the blood and no cuts.
- Not saved: the blood on the walls (the vendor's decals fade), the wounds painted on the skin.
- Broken cover and props were in the saves already (`RoomDamageLedger`).
- The training room dummies vanish at their death (`destroyOnDeath`, since step 3). They have no body to keep.
- An older save file still loads: a dead figure with no saved body stays hidden, as before.

## Proof

- `ShotSoundTests`: a bullet on concrete, steel and plywood plays its surface's sound, once. The material names pick
  the right surface. No set uses the knife clips.
- `BreakableTests`: a bullet on the barricade plays one wood hit.
- `SaveGoreTests` (the AI TEST room), with four deaths: a burst head, a cut arm, a ragdoll knocked down then killed,
  and the heavy. After a save and a load:
  - Each root and each hips bone is within 5 cm of its place, and the stumps are back.
  - The dropped shield, the piece (2 cm, 1°), the 4 pools and the 5 stains are back.
  - Her kills are the saved ones, and no one witnesses the deaths.
  - After 2.5 s nothing moved, no pool grew, and no one tends a body.
  - Gore Off: the bodies only.
- Shots: `~/glorified-body-renders/game/saves/2026-10-25_gore/sheet.png` (before the save, after the load: the room,
  the burst head, the cut arm, the ragdoll).
- The full run (23:55): EditMode 46/46. PlayMode 188/189. The six bots pass (ai, cover, civilians, tutorial,
  structural, look). The structural bot's slowest frame is 507 ms.
- The one PlayMode failure is `EnemyAiTests.Gas_TheCloudIsVisible_AndGreen`, a flaky test. The cloud's green pixel
  count is random: 1023 to 3202 over 16 runs, and the bar is 1152. It failed at 15:08 and 15:15 too, before this
  work. Alone, it passed three times (2172 to 2618).

## Bruno's answers (2026-10-26)

1. A body that a load puts back is never tended again: "keep it".
2. The dropped shield lies still after a load (kinematic): "keep it".
3. A save machine in AI TEST for a manual check: "no need".
4. A steady gas cloud test: "no need".
