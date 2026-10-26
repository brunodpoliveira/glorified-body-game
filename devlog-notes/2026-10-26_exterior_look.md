# 2026-10-26, night: the exterior look test (the yard)

Bruno's question: "we'll likely to have exterior envs. is the method we're doing for illuminating scenes work for
them fine?" His answer to my study: "yes", build the test.

**The answer: yes.** The rooms' method works outdoors with a sun in place of the spots. The shots:
`~/glorified-body-renders/game/look_test/2026-10-26_exterior/` (`sheet_baked.png` holds four cameras).

## The room

`LookTestExterior.unity` (`ExteriorLookSetup.BuildExterior`, then `BakeExterior`; F11 cycles to it from the look
rooms): the yard of the facility in the late afternoon.

- The yard: 30 x 20 m of asphalt, with one 120 m ground under all of it.
- On the north side, the facade: 9 m of concrete, a row of dark windows, the gear panel, a loading door under a
  canopy. On the west side, the wing. On the east side, a low wall. On the south side, a steel fence.
- In the yard: a white van (the story's beat at the window), three concrete barriers, two planters, a dumpster and
  two lamp poles.
- The cast: Lucy, a soldier, a guard.

| Part | The rooms | The yard |
|---|---|---|
| Direct light | Hard spots, live | One sun: a Mixed directional light, live, hard shadows |
| Bounce | Baked, indirect only, 10 texels a metre | Baked from the sun and the sky, 4 texels a metre (the far ground at a quarter of that) |
| Ambient | Near black | The sky (a procedural skybox), baked into the lightmaps |
| Shadow map | The 4096 spot atlas | The sun's 2048 map. Each camera sets its shadow distance (20 to 26 m, `FixedCamera.shadowDistance`), so the map covers only what it sees |
| Depth | None | Linear fog from 22 m to 95 m |
| Cameras | 3 to 8 a room | 7 for the yard: an open area needs a camera for each part |
| Bake | Minutes | 17 s, 3 lightmaps |

## What I learned

- An open yard needs more fixed cameras than a room. The first layout had 3, and the coverage test found 31 % of the
  floor out of frame. Seven cameras, each on a 10 m cell and 4 m up, pass the test. The van needed its own camera
  for the strip behind it.
- Outdoors the bake does not make the picture brighter. The live sky light already lights the real-time dressing,
  and the bake replaces it with the bounce of the sun and the ambient occlusion. The test for the yard checks that
  the bake changes the picture (real time 0.349, baked 0.335 on the mean of a frame).
- The sun cannot move: the bounce is for one time of day. A night yard uses the rooms' method (lamps).
- `ShadowDistance` keeps the URP asset's own value (40 m) and puts it back when the room changes. The asset is a
  shared file, and the editor would save a changed value otherwise.
- A room face shows its front to -z at no rotation. The first build turned the facade, the wing and the fence the
  wrong way, so the camera did not draw them (fixed before the shots).

## Proof

- `LookTestExteriorTests` (8, the shared look room tests with an exterior branch):
  - The room has one sun (Mixed, hard), a sky and fog.
  - The four dressings switch.
  - Every dressing renders a frame with shading. This test now samples the whole frame: it read only the bottom rows
    before.
  - The bake changes the picture. 38 of 2066 floor points are out of frame, under the 3 % bar.
  - Each camera sets its shadow distance, and the training room gets the asset's own 40 m back.
- The Gallery and LookTest2 tests still pass (22 of 22 for the three look rooms).
- The look bot in the yard: pass, every dressing and camera shot.

## Open for Bruno

1. The yard is a greybox: a backdrop past the fence (trees, other buildings) is content for later.
