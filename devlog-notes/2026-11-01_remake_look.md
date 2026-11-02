# 2026-11-01: the remake look test (T26), restarted in the black room

Bruno (2026-11-01, evening): compare A (the current look of the game) with B (real-time 3D that emulates the
RESULT of the pre-rendered Resident Evil remake of 2002: not its method, not limited to 720p). A real 3D room,
rendered live. No plates, no AI-painted backgrounds.

## The first pass is superseded

The first pass of the afternoon used the wrong style and the wrong guard texture:

1. The room was the white tile and the bare concrete of the T18 boards. Act 1 is black on black since round 11
   (design §13.8, "act 1 is nigredo").
2. The guard wore the OLD navy base colour (mean colour 37, 37, 43). Round 16 (T24) gave him the light kit.

Its renders are kept, not deleted: `~/MEGA/glorified-body-renders/T26_remake_look/superseded_first_pass/`
(AB_*, ablation_*, shots/, clips/). Its materials and room textures are out of the project.

## What was built

1. **The guard's light kit in the game** (CONCEPTS §8.0). `Characters/Models/guard_basecolor.png` and
   `lucy_guard_basecolor.png` now hold the T24 files. Mean colour after the install: guard 64, 68, 73 (was 37, 37,
   43), Lucy's disguise 77, 79, 81 (was 45, 44, 45). The 4096 copy for B (`Remake/Characters/guard_basecolor_4k.jpg`)
   is the T24 `guard_light_basecolor.jpg` (64, 68, 73). The first pass had the navy 4096 there too.
2. **One scene for both profiles**: `Scenes/RemakeLook.unity` (`RemakeLookSetup.Build`, then `.Bake`). A copy of
   LookTest2 with its recovery room dressed as W1 of `game/DEMO_PLAN.md` in the act 1 look. A and B share the
   room, the props, the light and the staging, so the comparison shows only the rendering.
   `RemakeDirector.SetProfileA` gives A. F3 cycles B all on, each switch off, all off, then A.
3. **The black room (W1)**, textures from T22 (the `_1` of each), maps by `tools/remake_maps.py` (0 tokens):
   - Walls: black tile, white grout (about 27 cm tiles). Floor: polished black granite. Ceiling: the black grid.
   - The chrome bed (LookTest2's frame), a black leather mattress, brass at the joints. The straps burned through
     from inside: six straps hang short with charred ends, the stubs with brass buckles stay on the mattress.
   - The drip stand (chrome, five castors, an empty bag). The cabinet with amber ampoules (black steel, a glass
     front, two shelves of ampoules, three bottles on top, the tray set of the first pass beside them).
   - The fern planter (the 3D Studio mesh, the planter recoloured to black granite, the fern kept green). The
     locker (the 3D Studio mesh in black enamel, its white gear kept). The gear on the east wall (a cutout, as on
     the board). The dead camera in the south-east corner (a dark lens, no light). A round chrome vent over the
     bed head with a fan behind the grille.
   - One small ceiling light: a round downlight, a hard spot straight down, 120 degrees, neutral white, at
     (5.1, 3.2, 5.4). The figures stand in its pool (T22's rule: walls black, pools on the floor).
   - Decals of B, only what a clean, cared-for ward shows: the scorch where each strap burned through, a faint mop
     haze on the polished floor, trolley scuffs by the door. No grime.
4. **A** = Simple Lit with the colour map (1024 maps, uncompressed, as LookTest2), A's bake numbers, A's shadow
   tiers (4096 atlas, 256 / 512 / 1024), the Authentic dither, no anti-aliasing. Characters: the game models at
   about 2,500 triangles with 1024 maps. Lucy is C3 (the T20 subtle sunken veins on the game mesh,
   `RB_lucy_c3_1k`), the guard is the game guard in the light kit.
5. **B** = the ten switches, each can go off alone: (1) the high bake (40 texels a metre, four bounces,
   directional, the fixture as a baked disc light), (2) the 4096 shadow map on the light (8192 atlas), (3) dense
   probes, SSAO at contact distance and foot blobs, (4) Lit surfaces with 2048 colour, normal and smoothness maps,
   and the decals, (5) the light in the air with dust in the cone, (6) the grade (Neutral tone map, a light grain,
   soft bloom, a vignette), (7) the live layers (the light stutters now and then, the vent fan turns), (8) the
   box-projected reflection probe, (9) native 1080p with MSAA 4x and SMAA, no dither, (10) the long lens (26
   degrees) on `Cam_Recovery_Long`.
6. **B's characters**: the guard is the new full-mesh build (below). Lucy is the 2,500 game mesh with the 4096 C3
   maps (the fallback, below). `RemakeBodySwap` shows the full-mesh guard in B: the game body keeps its animator,
   its colliders and its code. The B body plays the same state of the same controller every frame, through the
   Humanoid retarget.
7. **The bot** (`RemakePlaythrough`, bot `remake`): one run renders A, B, each switch off and all off for three
   cameras, a mask of each figure (the contrast check), a 1440p sample, the frame times, and the walk in A and in B.
   Tools: `remake_sheets.sh`, `remake_contrast.py` (the T22 method), `remake_closeups.py` (each figure at 2x).

## The full mesh: the rig answer

**Yes. 3D Studio rigs and animates a mesh with no face limit.** The guard from the light-kit picture
(`T24_guard_light/guard_light_1.png`): image to 3D with no face limit gave 230,556 triangles (121,861 vertices).
The detailed texture stage with the same picture gave 4096 base colour, normal and metal-rough maps. The rig (v1.0,
mixamo, 23 bones) and idle plus walk worked in one pass each. The price is the same as for the 2,500 mesh. In
Unity it imports as Humanoid and plays the game's guard controller. Files:
`~/MEGA/glorified-body-renders/T26_remake_look/full_mesh/` (model id `99a6e3b2-02be-4401-9c2e-27beb9770e67`).

**Lucy has no full mesh.** Image to 3D refused the T19 doll picture `lucy_bare_plain_1.png` (Tripo code 2008,
"content policy", no charge). The same picture passed on 2026-10-21. With the two T20 refusals (the C3 skin, the
clay version) this is the third refusal of the figure, so there was no fourth try (the craft rule). RFab gap 17.
B's Lucy is the fallback the brief allows: the 2,500 mesh with the 4096 C3 maps. Her gold veins are our own paint on
the T19 UVs, so they stay right on that mesh. Two routes for a full-mesh Lucy, if Bruno wants one: subdivide the
2,500 mesh in Blender (the UVs, the rig and the veins stay, 0 tokens), or import an own base body through the import
route and rig it there (25,000 plus the motions).

## The spend

95,000 tokens ($1.90), measured with `rfab cost start/stop t26_restart`: the guard's image to 3D 20,000, the
detailed texture with the picture 30,000, the rig 25,000, idle and walk 20,000. Lucy's refusal cost nothing. The
props were paid before (T15/T16). The local balance (66,297) was too low for two builds: a local grant of 300,000
(`_grant_local_tokens.js`, the local database only).

## Frame times

GTX 1050 (2 GB), 1920 x 1080, the bed camera, each frame waited for on the GPU, the lowest of three rounds:

| Setting | ms a frame |
|---|---|
| A | 7.8 |
| B, all ten on | 24.3 |
| B, 1440p | 88.0 (A at 1440p: 11.2) |
| B, all ten off (B characters) | 12.0 |

Switch by switch (B with that one switch off), ms: 1 bake 25.3, 2 shadow maps 24.9, 3 probes and contact 21.8,
4 surfaces 23.3, 5 the air 18.2, 6 grade 25.5, 7 live 25.6, 8 reflections 25.8, 9 clarity 25.3, 10 lens 25.9. B with
the 2048 shadow map instead of 4096: 24.6. Over the last three runs B all on was 22.8 to 24.3 ms: differences
under about 1.5 ms are noise (the desktop shares the GPU).
The big costs: the light in the air (about 6 ms), the probes, SSAO and blobs (about 2.5 ms), the surfaces (about
1 ms). The other switches cost less than the noise. The full-mesh guard is in every B number, all off too (12.0
against A's 7.8: the guard of 230,556 triangles, the 4096 maps, the dense probes). The 1440p number jumps between
runs (63 to 94 ms): the 2 GB of VRAM is the limit there.

## Contrast (the T22 method)

Weber contrast of each figure against a ring of background just around it. Positive = the figure is brighter than
what is behind it. `T26_remake_look/contrast.txt` has every number.

| Camera | Profile | Lucy | Guard |
|---|---|---|---|
| Cam_Recovery | A | +3.23 | +9.73 |
| Cam_Recovery | B | +0.59 | +0.70 |
| Cam_Recovery | B, air off | +1.32 | +5.36 |
| Cam_Recovery_Bed | A | +4.54 | +2.56 |
| Cam_Recovery_Bed | B | +1.02 | +0.33 |
| Cam_Recovery_Bed | B, air off | +2.06 | -0.03 |
| Cam_Recovery_Long | A | +9.58 | +6.12 |
| Cam_Recovery_Long | B | +1.29 | +0.87 |
| Cam_Recovery_Long | B, air off | +2.42 | +3.96 |

For scale, T22 and T24 in Blender: the light-kit guard +0.12 to +0.98, Lucy +1.59 to +1.81.

Lucy reads in every frame of both profiles. The guard reads in every frame too, but in B the air of the cone
sits behind him, so his number is lower (bed camera +0.33). With the air off (switch 5), the guard on the bed
camera falls to -0.03: the wide cone lights the bed behind him more than his dark vest. Same finding as T22
setup B (a light that lights the background more than the figure).

## The checks at full size

- The walls are black: the median brightness of the upper third of the frame (the figures left out) is 0.000 in A
  and 0.011 to 0.015 in B. On the long lens it is 0.17 in B: the cone of air fills that frame. With the air off it
  is 0.000 in B too.
- The guard is grey-blue with the white bands: his figure pixels have blue over red in every frame (+5 to +9) and
  2 to 5 percent near-white pixels (the bands). Mean colour in B on the long lens: 62, 66, 69. Close-ups:
  `closeups/closeup_guard_*.png`.
- Lucy is pale with the gold veins: pale in every frame (mean 49 to 106 grey, by the light). The veins read as thin
  dark grooves with a line of gold, as in the T20 "subtle" pick. The gold pixel share is under 1 percent: the gold is
  metal, and a black room gives it little to reflect. Close-ups: `closeups/closeup_lucy_*.png`.
- A's 2,500 guard shows jagged shadow steps on the vest and the legs (A's 1024 shadow map on a low mesh). B's full
  mesh has none.

## What failed on the way (and the fix)

1. The light in the air at strength 1 hid the room and the guard in fog: 0.08 on the light, half density.
2. ACES tone mapping crushed the black room under A (frame mean 0.011 against 0.024): Neutral.
3. The first normal maps drew every grout line as a black groove: strengths down by about 20 times.
4. The polished floor reflected Unity's default blue sky at the room's edge: no sky, a black default reflection.
5. **A bug of the first pass too**: static batching burned B's lightmap layout into the meshes, so A and switch 1 off
   read A's lightmaps with B's layout (30,174 warnings in the log). Static batching is off in this scene.
6. A warm light turned the guard's grey-blue to brown-grey: neutral white.
7. The guard stood behind Lucy, and the light was behind Lucy on the bed camera: new staging.
8. A 92 degree cone missed the cabinet, the fern and the drip stand: 120 degrees.
9. The locker stood in front of the long camera: moved to the east wall.
10. The walk ended on the cabinet's base: a shorter walk.

## The files

`~/MEGA/glorified-body-renders/T26_remake_look/`:
- `AB_cam_recovery.png`, `AB_cam_recovery_bed.png`, `AB_cam_recovery_long.png`: A left, B (all on) right, 1920 x
  1080 each.
- `ablation_<camera>.png`: B all on, each switch off alone, all off, 4 x 3 at full size.
- `clips/remake_A_walk.mp4`, `clips/remake_B_walk.mp4`, `clips/remake_A_B_walk_side_by_side.mp4`.
- `shots/` (every frame, the masks, `remake_B_1440p_cam_recovery_bed.png`), `closeups/`, `contrast.txt`,
  `full_mesh/`.

Runs: `runs/20261101-205917-exec-76886` (build), `20261101-210155-exec-77547` (bake: low 33 s, high 472 s), `20261101-211042-record-81606` (the bot, 71 of 71 steps, CLEAN).

## For Bruno (the pick)

1. **A or B?** A is the game today: hard light, black walls, the room read by its white grout, sharp and clean.
   B adds the cone of light in the air, the soft tone of a pre-rendered frame, smooth contact shadows, and the full-
   mesh guard. B costs about 3 times the frame time of A (24.3 ms against 7.8 ms at 1080p on the GTX 1050).
2. **If B, which switches?** The ablation sheets show what each one adds. The cheap ones with a clear effect: the
   grade (6), the surfaces (4), the reflections (8), the long lens (10). The expensive one: the light in the air
   (5, about 7 ms). It is also the one that lowers the guard's contrast.
3. **The full-mesh characters:** the guard works (rig and motion), at the same price as the 2,500 build. Lucy
   needs another route (above). Do you want the full mesh for the game, or only for cutscenes?
4. **The room's light:** one 120 degree downlight lights the props now. The north wall and the corners stay black.
   Is that the level of dark you want for act 1?
