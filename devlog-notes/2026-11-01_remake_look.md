# 2026-11-01: the remake look test (T26), restarted in the black room

Bruno (2026-11-01, evening): compare A (the current look of the game) with B (real-time 3D that emulates the
RESULT of the pre-rendered Resident Evil remake of 2002: not its method, not limited to 720p). A real 3D room,
rendered live. No plates, no AI-painted backgrounds.

## Round 23: Bruno's picks and the final set (late evening)

His answers (CANON, design §28a):
1. "I prefer B."
2. No dither, anti-aliasing on. "Turn off the most expensive ones and keep the cheapest ones for performance."
3. The full mesh is for cutscenes only. In play, the 2,500 game meshes.
4. "Since it is a hospital most rooms will be brightly lit." The dark black-walled room is for "some more abandoned
   areas". The black-on-black materials stay.
5. The light in the air is only for rooms "that call for that direct light, especially on act 1": a flashlight, a
   flood light, an abandoned room under emergency lights. Most rooms have bright LED panels, "not ones with strong
   directional lights".
6. The light in the air must be adjustable later (see "For a later build" below). Not built now.

He also found a fault: Lucy's gold veins did not show in B (0.0 percent gold pixels).

### What changed

1. **B-game** (`RemakeDirector.SetProfileGame(air, tier)`): B with switch 3 OFF (SSAO and the foot blobs, the most
   expensive switch after the air), switch 5 (the air) a room setting, every other switch on: the high bake, the
   4096 shadow map, the Lit surfaces and decals, the grade, the live layers, the reflection probe, no dither with
   MSAA 4x and SMAA, the long lens. The 2,500 game meshes, the full-mesh guard off. The figures keep their baked
   probes in every profile now (switch 3 is the contact shadows only).
2. **Two rooms, the same W1 and the same materials:**
   - The bright hospital room, `RemakeLookBright.unity` (`RemakeLookSetup.BuildBright`, `BakeBright`): four
     recessed LED panels in a 2 x 2 grid. Each is a hard spot straight down (live) and a soft rectangle baked into the
     lightmaps (the even fill). Air OFF.
   - The dark abandoned room, `RemakeLook.unity`: the ward's ceiling light is dead (the fixture stays, unlit). A twin-
     head emergency light over the door lights the room, both heads aimed across it. Air ON.
3. **The gold veins** (`tools/remake_lucy_gold.py`): the T20 subtle maps kept the painted colour inside the veins, a
   pale beige (170, 148, 112) almost the same as the skin (164, 148, 135), at full metal. A metal shows what it
   reflects, and a black room gave it nothing. Now the vein colour is the T20 gold (1.00, 0.76, 0.22) shaded by the
   painted brightness, and the metal is 0.6, so 40 percent of the gold is diffuse and reads under any light. The sunken
   groove stays. The grade's saturation is 0 (it was -10). Gold pixels on Lucy: 6.7 to 14.3 percent in B-game (was
   0.0), 8.1 to 14.9 percent in A (A takes the same fix at 1024).
4. **The texture pick:** 2048 (below).
5. A fault found on the way: in the bright room the reflection probe put the fern's green on the glossy tiles as a
   green stain. The fern is out of the probe now.

### The texture pick: 2048

The bot renders the same frame at 1024, 2048 and 4096 with the figures frozen, the dust paused and the grain off, so
only the maps differ. A second 4096 frame gives the noise floor. PSNR against 4096, inside each figure's mask, over
three cameras and both rooms (`game/tiers_bright.txt`, `game/tiers_dark.txt`):

| Figure | 1024 | 2048 | 4096 again (noise floor) |
|---|---|---|---|
| Lucy | 36.6 to 40.6 dB (mean diff 1.1 to 1.8 levels) | 41.5 to 44.2 dB (0.6 to 0.85 levels) | 45.9 to 53.7 dB |
| Guard | 46.1 to 51.3 dB (0.3 to 0.6 levels) | 47.2 to 53.1 dB (0.2 to 0.9 levels) | 48.8 to 56.2 dB |

At 2x (`game/tiers_<room>_<who>_<camera>.png`): **Lucy at 1024 loses the veins**: they go soft and wide on the shins,
the feet and the forearms. At 2048 nothing differs from 4096. The guard at 1024 is the same as 4096 within the noise
(plain cloth, large shapes).
- **The pick: 2048 for Lucy.** The guard (and figures of plain cloth like him) can stay at 1024 with no visible loss.
  The final sheets use 2048 for both.
- VRAM (BC7 and BC5, 1 byte a pixel, with mipmaps): a 4096 map 22.4 MB, a 2048 map 5.6 MB, a 1024 map 1.4 MB. Lucy has
  three maps (colour, metal-smoothness, normal): 67 MB at 4096, 17 MB at 2048, 4 MB at 1024. The guard has one map: 22,
  5.6 or 1.4 MB. With ten classes in a room, 4096 would need about 300 to 700 MB of the 2 GB card. 2048 needs a quarter.
- The frame time does not change with the tier (bright room, B-game: 13.7 ms at 1024, 13.8 at 2048, 13.5 at 4096).

### Frame times of B-game (GTX 1050, 1080p, the bed camera, the lowest of three rounds)

Bot `remake_time`: the measures first thing after the load, no shots, one texture tier loaded. Two runs each, ms:

| Room | A | B-game | B-game with the other air setting | B-game at 1440p |
|---|---|---|---|---|
| Bright hospital room (air off) | 9.0 / 9.6 | 12.5 / 13.1 | 24.6 / 25.3 (air on) | 18.1 / 18.5 |
| Dark abandoned room (air on) | 7.8 / 7.6 | 20.5 / 20.1 | 10.8 / 10.9 (air off) | 31.4 / 31.4 |

- The air is the big cost: about 9.5 ms with the two emergency heads, about 12 ms with the four panels.
- B-game in the bright room costs about 1.4 times A (about 13 ms against 9). That is 75 frames a second on the GTX
  1050. The dark room with the air: about 50 frames a second.
- **VRAM is the limit, not the GPU time.** The other apps held 250 to 612 MiB of the 2 GB card before the runs. When
  the bot first took its shots and loaded the three tiers, and then measured, the dark room's B-game ran into VRAM
  thrash (frames of seconds). Those numbers are not the cost of the settings and are not in this note. The same room,
  measured first, is stable at about 20 ms. For the game: keep the character maps at 2048 and watch the VRAM of a
  room with the air on.

### Contrast in B-game (Weber, the T22 method)

The final shots (`game/contrast_bright.txt`, `game/contrast_dark.txt`), Lucy / guard:

| Camera | Bright A | Bright B-game | Dark A | Dark B-game (air on) | Dark B-game, air off |
|---|---|---|---|---|---|
| Cam_Recovery | +3.70 / +2.33 | +1.71 / +1.24 | +6.57 / +9.21 | +1.11 / +0.58 | +3.64 / +5.24 |
| Cam_Recovery_Bed | +3.43 / +1.70 | +2.16 / +0.82 | +9.89 / +4.68 | +1.62 / +0.42 | +5.34 / +2.93 |
| Cam_Recovery_Long | +4.42 / +0.96 | +2.84 / +0.47 | +15.47 / +5.25 | +1.23 / +0.52 | +0.85 / +1.80 |

Both figures are brighter than what is behind them in every frame. The guard is lowest where the air of the
emergency light sits behind him (+0.42). His colour: blue over red by +9 to +12 in every frame, the white bands 0.4
to 4.3 percent of his pixels. Lucy's gold: 13.2 to 14.3 percent of her pixels in the bright room, 6.5 to 9.6 percent
in the dark room. The walls: the median of the upper third of the frame is 0.04 to 0.05 in the bright room (a lit
black wall) and 0.004 in the dark room where no beam crosses it.

### For a later build: the light in the air, adjustable (design §28a item 6, not built now)

The requirement: the direction, the point of origin and the number of air sources in a room can each be set, for
"maximum flexibility when applying this effect on the room". Examples: a flashlight, a flood light, emergency lights.

**The current switch-5 setup does not block it.** Each air source is a `VolumetricLight` component on a real URP spot
or point light, so its origin and direction are the light's own transform and cone, with a strength of its own. The
dark room already has two. The limits to lift in that build: at most four air sources in one frame (the shader's
array), one density for the whole frame, and no air-only source (an air source also lights the surfaces and needs its
shadow map).

### The final files

`~/MEGA/glorified-body-renders/T26_remake_look/game/`:
- `AB_bright_<camera>.png`, `AB_dark_<camera>.png`: A beside B-game, three cameras each.
- `air_bright_<camera>.png`, `air_dark_<camera>.png`: B-game with the room's air setting beside the other one.
- `tiers_<room>_<lucy|guard>_<camera>.png` and `tiers_<room>.txt`: the texture pick.
- `contrast_bright.txt`, `contrast_dark.txt`.
- `clips/`: the walk in A and in B-game, in both rooms, and side by side.

Runs: RUNS_GAME.

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

## For Bruno (the pick), answered: see "Round 23" at the top

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
