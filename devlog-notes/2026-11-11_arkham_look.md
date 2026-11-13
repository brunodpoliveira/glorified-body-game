# 2026-11-11: the Arkham Knight look test (T38)

Bruno (2026-11-11): "I'd like for experiments, same as we did for re 2002; let's do research on how arknight did its
graphics, pull examples, and do experiments." The research and the 19 reference shots are in the writing repo
(`~/MEGA/glorified/production/tests/T38_arkham_look/PLAN.md`, `reference/generic/games/arkham_knight/`). This note is the Unity
experiment. Status: a TEST. Nothing here is a style ruling until Bruno picks.

No Arkham Knight picture is in this repo or in a render. The tools read only the reference NUMBERS
(`reference_stats.txt`). All maps of T38 are procedural (0 tokens, no RFab call).

## Run 2 (late evening): the four faults fixed

Bruno: "Fix the 4 faults". Each fault had a number to pass. The first-run sheets are in
`T38_arkham_look/run1/`; the new sheets have the same names in `T38_arkham_look/`.

### What changed

1. **The rim is a thin edge.** Warm (1.0, 0.9, 0.78) against the neutral key (it was cold blue), intensity 9 dark / 7
   bright (was 22 / 16), a 16-degree cone (was 34), from 2.3 m up and 1.3 m back, aimed at 1.5 m (the shoulders and the
   head).
2. **No lift in the exposure.** The grade volume is ACES (its highlight roll-off) at +0.4 (it was +1.85 / +2.15), no
   split toning. The LUT carries the rest: a gain and a contrast for the mid-tones, a highlight curve (a boost above a
   knee) and a shoulder, a global warm balance, and the tints. `tools/ak_lut.py` fits it with the four faults as
   guards against the B-game frames of the same run: the guard's mean colour, his blue over red, his pixels over 0.6,
   Lucy's pixels over 0.9, Lucy's colour, her gold, the walls. The shadow tint acts only under 0.015 (at 0.05 it took
   the red out of the guard's folds). The dark room's LUT took a hand change after the fit (lift 0.001 to 0, warm
   0.07 to 0.09): in the render, the lift raised the black walls of `Cam_Recovery` by +0.011 and the guard's blue over
   red was +13 on `Cam_Recovery_Bed`. Both are in `lut_fit_dark.txt`.
3. **The accents never light the walls.** The red and the teal lights are on rendering layer 4
   (`ArkhamDirector.AccentLayer`). At the start, every renderer gets layer 4, but not the walls, the ceiling and the
   gear (`accentBlocked`: the renderers with the B-game materials `RB_Tile`, `RB_Ceiling`, `RB_Gear`). So the accents
   light the props, the floor and the figures. The lamps now glow by their own emission and the bloom. A
   script-made URP Lit material with `EmissiveIsBlack` or `None` lost its `_EMISSION` keyword on a later import (the
   lamps went dark in two runs). `BakedEmissive` keeps it, as T26's `RB_Lamp`.
4. **The HUD.** In the game the HUD is a Screen Space Overlay canvas: it is drawn after the post-processing. The fault
   was in the recorder: `FrameRecorder` switched the canvases to the main camera for the clip, so they took the grade
   and the lens. Now the recorder renders the scene, then the canvases alone through a HUD camera with no
   post-processing (once on black, once on white) and composites them (`FrameRecorder.SaveWithHud`). This changes
   the clips of every bot: the HUD in a clip now looks as it does in the game (in run 1 the B-game HUD was also dimmed by
   the B-game grade).

### The four faults: PASS or FAIL (`faults_dark.txt`, `faults_bright.txt`, `hud_check_<room>.txt`)

| Fault and pass number | Dark room (3 cameras) | Bright room (3 cameras) |
|---|---|---|
| 1 Guard mean colour within ±15 levels of B-game | PASS (max diff 12, 4, 4) | PASS (9, 1, 4) |
| 1 Guard blue over red +6 to +12 | PASS (+7, +11, +8; B-game +9 to +10) | PASS (+10, +11, +9; B-game +11, +12) |
| 1 Guard pixels over 0.6 under 10 % | PASS (4.7, 0.6, 4.3 %) | PASS (8.5, 4.7, 3.3 %) |
| 2 Lucy pixels over 0.9 under 5 % | PASS (0.0 % in each) | PASS (0.4 % in each) |
| 2 Lucy gold at 80 % of B-game or more | PASS (151, 151, 165 %) | PASS (131, 138, 135 %) |
| 3 Walls (upper third median) at most B-game + 0.01 | PASS (−0.004, −0.001, +0.009) | PASS (−0.002, −0.001, −0.014) |
| 4 HUD pixels match B-game in the walk clip | PASS (the HP and MK bars: mean difference 0.0 to 0.1 levels) | PASS (0.0 to 0.1 levels) |

Run 1 for comparison: the HUD bars differed by 43 to 63 levels; the guard was 35 to 64 % over 0.6.

Narrow margins: the guard's colour on the dark `Cam_Recovery` (12 of 15), his pixels over 0.6 on the bright
`Cam_Recovery` (8.5 of 10), the walls of the dark `Cam_Recovery_Long` (+0.009 of +0.01).

### The cost: the distance to the reference grew

| Median of 3 cameras | p5 | p50 | p95 | p99 | chroma | shadow a*, b* | highlight a*, b* | distance |
|---|---|---|---|---|---|---|---|---|
| Arkham Knight | 0.003 | 0.026 | 0.426 | 0.750 | 7.4 | −1.7, 0.0 | −1.9, +3.4 | 0 |
| Dark B-game | 0.000 | 0.023 | 0.093 | 0.338 | 1.2 | 0.0, 0.0 | −0.1, +1.0 | 3.30 |
| Dark AK, run 2 | 0.000 | 0.023 | 0.122 | 0.398 | 2.8 | 0.0, 0.0 | −1.1, +5.6 | **2.70** (run 1: 0.40) |
| Bright B-game | 0.001 | 0.012 | 0.141 | 0.381 | 2.0 | −0.2, −0.3 | +0.1, +4.2 | 2.76 |
| Bright AK, run 2 | 0.001 | 0.008 | 0.223 | 0.566 | 4.7 | −0.1, −0.2 | −4.2, +3.0 | **2.17** (run 1: 0.69) |

**Both rooms are now over 1.0. I kept the fixes, as the brief says: the four faults come first.** The cause is plain in
the numbers. In run 1 the reference median was reached by lifting the whole frame: the mid-tones, the walls and the
figures. With the figures and the walls held at B-game, only the highlights and the accents are free. They give
p95 0.12 to 0.22 (the reference has 0.43) and a chroma of 2.8 to 4.7 (the reference has 7.4). Arkham Knight's
frames are brighter and more coloured than a black-on-black act 1 room can be.

### Frame times (GTX 1050, 1080p, the bed camera, the lowest of three rounds, two runs)

| Setting | Dark room, ms | Bright room, ms |
|---|---|---|
| B-game | 21.6 / 21.9 | 13.5 / 13.2 |
| AK, all eight on | 28.7 / 31.5 | 19.5 / 19.4 |
| B-game + 4 planar | 25.9 / 29.2 | 17.9 / 18.0 |
| B-game + each other switch alone | 21.3 to 22.1 / 22.5 to 23.4 | 13.1 to 13.8 / 13.1 to 14.0 |

Other apps held 607 to 648 MiB of the GPU before each run. The card peaked at 1.91 to 1.93 GB. A first set of runs
(at 21:37, other apps at 775 MiB) gave 35 to 70 ms: VRAM thrash, not the cost of the settings. They are not used.

### The T26 check (the `driven` flag in `RemakeDirector`)

- Bot `remake` (`RemakeLook`): 71 of 71 steps, CLEAN.
- Bot `remake_time`, bright room: A 8.6 ms, B-game air off 12.9 ms, air on 24.8 ms. T26 (2026-11-01): 9.0, 12.5,
  24.6. The same.
- Bot `remake_time`, dark room: A 7.3 to 8.2 ms (T26: 7.6 to 7.8), but B-game 251 to 275 ms with the air and 57 to 63
  ms without it (T26: 20.1 to 20.5 and 10.8). That is VRAM thrash (the card at 1.86 GB). **An A/B run with the two
  `driven` checks taken out gave the same thrash (251 ms, run `20261111-224918-play-392562`), so the flag is not the
  cause.** The flag is false in the T26 scenes, and it changes only the F3 key and the label. A likely cause: the
  T26 scene starts in profile B with the full-mesh guard and the 4096 maps, so they go into VRAM before the switch
  to B-game. The T38 copy of the same room switches to B-game before the first frame and measures 21.6 ms. This is
  not checked further.

### Runs of run 2

Build `20261111-222805-exec-382446` (the last `ArkhamLookSetup.Build`), shots and clips `20261111-222916-record-383103` (dark) and
`20261111-223344-record-384194` (bright), frame times `20261111-223841`, `-223948` (dark), `-224100`, `-224158`
(bright), T26 `20261111-211117-play-356959` (remake), `-224249`, `-224430`, `-224544`, `-224918` (remake_time).
Spend: 0 tokens.

## Run 1 (evening): the first build

The sections below are run 1, kept as it was. Its faults are fixed in run 2 above. Its sheets are in `run1/`.

### What was built

1. **Two new scenes**, copies of the two T26 rooms: `Scenes/ArkhamLook.unity` (the dark abandoned room, emergency
   light, air on) and `Scenes/ArkhamLookBright.unity` (the bright hospital room, four LED panels, air off).
   `ArkhamLookSetup.Build` (editor code) copies the T26 scene, keeps its bake (the same lighting data), and adds one
   root, `[ArkhamAK]`. `RemakeLook.unity`, `RemakeLookBright.unity`, their profiles and the shipped rooms did not
   change.
2. **Profile AK** = Bruno's B-game (design §28a, the control) + eight switches (`Rendering/ArkhamDirector.cs`). Each
   switch turns off on its own. F3 cycles: AK all on, each switch off in turn, B-game. The scene's `RemakeDirector`
   runs B-game. `ArkhamDirector` drives it (`RemakeDirector.driven`: no F3 and no label there).

| Switch | What it does |
|---|---|
| 1 grade | A volume over the B-game grade: ACES, exposure (+1.85 dark room, +2.15 bright room), contrast, saturation, a mild split, and a LUT for each room fitted to the reference median (`tools/ak_lut.py`) |
| 2 rim | One spot for each figure behind it, placed for the camera in use each frame. No shadow. Rendering layer 2, so it lights only the figures. Cold (0.62, 0.86, 1.0) |
| 3 wet | The floor in the Shader Graph sample "Production Ready Shaders" rain floor, with our granite maps: darker, smoothness up, the sample's puddles. No rain on the surface (indoors) |
| 4 planar | A mirror camera renders the room at half size into a target. A quad on the floor (`GB/AK/PlanarOverlay`) adds it with a Fresnel term and a puddle mask |
| 5 specular suits | The guard: the dark parts (vest, boots, belt) glossy with a breakup noise, the cloth at 0.22. Lucy: a wet sheen on the doll skin (0.55 ± 0.12), the gold veins as T26 |
| 6 practicals | A red alarm lamp (east wall, by the bed's foot) and a teal monitor (over the bed head), each with a small light. Lens flares on the lamps. Bloom with a lens dirt map |
| 7 leak | Dark room only: a leak from the ceiling in front of the bed's foot. A narrow light from the hole (it also marks the air), the drips, the ripples, a puddle |
| 8 lens | Chromatic aberration 0.08, a Gaussian depth of field (5 to 11 m), grain |

3. **The wet floor: the Unity sample works.** The sample "Production Ready Shaders" is in the package cache
   (`Library/PackageCache/com.unity.shadergraph@*/Samples~`). `tools/ak_import_weather.py` copies only the rain floor's
   subgraphs and their files (20 files, found by GUID, the GUIDs kept) to `Assets/Samples/Shader Graph/17.3.0/`, and
   writes `Arkham/AK_WetFloor.shadergraph`: the sample's `RainFloorExample` with our granite maps in place of the
   cobblestones. It imports and compiles in batch mode under URP 17.3.0. No own subgraph was necessary.
   `Assets/Samples/` and `AK_WetFloor.shadergraph` (a copy of the sample graph) are out of git (Unity Companion License,
   not GPL). So a fresh clone needs the script run once before `ArkhamLookSetup.Build`.
4. **The bot** (`Automation/ArkhamPlaythrough.cs`):
   - `arkham` (`tools/unity.sh record ArkhamLook Main_Menu arkham 1500`): for each camera, B-game, AK, AK with each
     switch off, AK with the LUT out (the input of the fit), the masks. Then the walk in B-game and in AK.
   - `arkham_time` (`tools/unity.sh play ArkhamLook Main_Menu arkham_time 600`): the frame times only, measured first
     after the load. B-game, AK, B-game with each switch alone. The mirror renders before each frame, so it is in
     the number.
   - Both rooms: 51 of 51 steps, CLEAN.
5. **Tools:** `tools/ak_textures.py` (the procedural maps), `tools/ak_import_weather.py`, `tools/ak_lut.py` (the fit),
   `tools/ak_sheets.py` (the sheets, the stats, the contrast).

### The grade: tuned with numbers

`tools/ak_lut.py` reads the AK frames with the LUT out. It fits nine numbers (gain, contrast, a tinted lift,
shoulder, saturation, two shadow tints, two highlight tints) to the reference median. It writes a 32-point LUT
strip and applies that same grid (trilinear, as URP does) in the fit. Two guards: Lucy's gold pixels must stay at 80 %
of the count before the LUT, and the walls (the upper third, figures out) must stay under 0.06.

`ak_stats.py` numbers of the final frames, median of the three cameras (`stats_<room>.txt`):

| | p5 | p50 | p95 | p99 | chroma | shadow a*, b* | highlight a*, b* | distance |
|---|---|---|---|---|---|---|---|---|
| Arkham Knight (19 shots) | 0.003 | 0.026 | 0.426 | 0.750 | 7.4 | −1.7, 0.0 | −1.9, +3.4 | 0 |
| Dark room, B-game | 0.000 | 0.023 | 0.094 | 0.337 | 1.2 | 0.0, 0.0 | −0.1, +1.0 | 3.30 |
| Dark room, AK | 0.003 | 0.025 | 0.438 | 0.917 | 7.6 | −1.8, +0.2 | −2.5, +3.3 | 0.40 |
| Bright room, B-game | 0.001 | 0.012 | 0.141 | 0.383 | 2.0 | −0.2, −0.3 | +0.1, +4.2 | 2.76 |
| Bright room, AK | 0.003 | 0.022 | 0.503 | 0.854 | 8.0 | −1.1, 0.0 | −2.7, +4.0 | 0.69 |

- The medians land near the reference. The single frames spread more: dark room 1.70 to 5.05, bright room 0.61 to
  1.74 (distance = the log luminance, the chroma / 3 and the tints / 2, against the reference median).
- p99 is high (0.85 to 0.92 against 0.75). The beam, the lamps and Lucy's skin go near white.
- The colour is mostly teal. Of the coloured pixels, 68 % are teal in the dark room and 17 % in the bright room,
  against 7 % in the reference. Arkham Knight spreads its colour over more hues.

### Frame times (GTX 1050, 1920 x 1080, the bed camera, the lowest of three rounds, two runs)

| Setting | Dark room (air on), ms | Bright room (air off), ms |
|---|---|---|
| B-game | 22.7 / 22.9 | 14.6 / 15.1 |
| AK, all eight on | 30.6 / 31.4 | 23.2 / 27.3 |
| B-game + 1 grade | 23.0 | 14.7 |
| B-game + 2 rim | 23.0 | 14.5 |
| B-game + 3 wet | 23.0 | 15.0 |
| B-game + 4 planar | 28.9 | 19.5 |
| B-game + 5 specular suits | 22.9 | 14.7 |
| B-game + 6 practicals | 22.9 | 14.7 |
| B-game + 7 leak | 23.9 | no leak in this room |
| B-game + 8 lens | 23.4 | 15.5 |

(The lower of the two runs for each switch. Every number is in `T38_arkham_look/time/`.)

- **The planar reflection is the only big cost: +5 to +6 ms.** The other seven switches cost 0 to 1.2 ms each. Under
  about 1.5 ms is noise (T26).
- AK in the dark room: about 33 frames a second. In the bright room: about 43 (one run gave 27.3 ms).
- B-game measured 22.7 ms (dark) and 14.6 ms (bright) today. T26 measured 20.1 and 12.5 ms. The desktop had other
  apps on the GPU (about 0.64 GB before each run).
- **VRAM:** the card peaked at 1.89 to 1.92 GB of 2.0 GB during the runs (nvidia-smi every 0.5 s). Unity's graphics
  memory: 1,086 MB (dark, B-game), 1,092 MB (dark, AK); 1,017 and 1,023 MB (bright). The mirror target adds about 6 MB.
  The card is near full. Measure first after the load, as T26 found.

### Contrast (Weber, the T22 method), Lucy / guard

| Camera | Dark B-game | Dark AK | Bright B-game | Bright AK |
|---|---|---|---|---|
| Cam_Recovery | +1.12 / +0.58 | +0.61 / +0.76 | +1.72 / +1.24 | +1.35 / +0.98 |
| Cam_Recovery_Bed | +1.64 / +0.42 | +1.10 / +0.79 | +2.13 / +0.76 | +1.64 / +0.96 |
| Cam_Recovery_Long | +1.31 / +0.55 | +1.14 / +0.95 | +3.13 / +0.76 | +2.18 / +0.89 |

- Both figures stay brighter than what is behind them in every frame. In AK, Lucy's number falls (the room behind
  her is brighter). The guard's number rises in the dark room (the rim).
- **Lucy's gold veins read in AK.** Gold pixels: dark room 6.7 / 8.8 / 12.9 % (B-game 6.5 / 9.4 / 9.7), bright room
  12.6 / 12.6 / 12.8 % (B-game 14.3 / 14.4 / 13.4).

### Faults found at full size (fixed in run 2)

1. **The guard goes pale blue in AK.** His mean colour in the dark room is 140 to 154 grey with blue over red +24
   to +38 (B-game: 70 to 86, +9). 35 to 64 % of his pixels are over 0.6 (B-game: the white bands only, 0.4 to 4 %).
   The cause: the grade's exposure and the rim together. A lower rim on the guard or a lower exposure would fix it.
2. **Lucy is near white in the beam** of the dark room (mean 209 to 236). The veins still read.
3. **The walls are less black.** The upper third of the bright room: 0.085 to 0.107 in AK, 0.038 to 0.050 in B-game.
   The red and the teal accents light the white grout around them. Act 1 is black on black (design §13.8): this is
   the biggest conflict with the rulings.
4. **The HUD goes through the grade and the lens.** In the walk clips the HUD bars and text have colour fringes and a
   brighter white. In the game, the HUD must stay out of the AK post-processing.
5. The rim gives a thin light outline. With the depth of field on the long lens it reads as a soft halo.
6. ART_LESSONS 3.8 caps the fabric gloss at 0.25. Switch 5 keeps the guard's cloth at 0.22 but makes his vest and
   boots glossy (to 0.92). That is the Arkham Knight suit idea, not the current rule.

The rulings that hold: hard shadows (the rims and the practicals have no shadows; the key lights are as B-game), the
level cameras, 1080p.

### The files of run 1

`~/MEGA/glorified/renders/T38_arkham_look/run1/`:
- `BA_<dark|bright>_<camera>.png`: B-game left, AK right (look at these first).
- `ablation_<dark|bright>_<camera>.png`: AK all on, each switch off, B-game, 4 x 3 at full size.
- `stats_<room>.txt`, `contrast_<room>.txt`, `lut_fit_<room>.txt`, `time/` (frame times, VRAM samples).
- `clips/<room>_BG_AK_side_by_side.mp4`, `clips/<room>_BG_walk.mp4`, `clips/<room>_AK_walk.mp4` (5 s each).
- `shots/<room>/`: every frame and mask.

Runs: build `20261111-203643-exec-346488` (the last `ArkhamLookSetup.Build`), shots and clips
`20261111-204225-record-351599` (dark), `20261111-204426-record-352129` (bright), frame times
`20261111-203736`, `203854` (dark), `204006`, `204110` (bright). Spend: 0 tokens.

## The files (run 2)

`~/MEGA/glorified/renders/T38_arkham_look/`:
- `BA_<dark|bright>_<camera>.png`: B-game left, AK right (look at these first).
- `ablation_<dark|bright>_<camera>.png`: AK all on, each switch off, B-game, 4 x 3 at full size.
- `faults_<room>.txt` (the four faults, PASS or FAIL), `hud_check_<room>.txt` and `hud_<room>.png` (the HUD boxes at 2x,
  B-game left), `stats_<room>.txt`, `contrast_<room>.txt`, `lut_fit_<room>.txt`, `time/` (frame times, VRAM, the T26
  check), `shot_runs.txt`.
- `clips/<room>_BG_AK_side_by_side.mp4`, `clips/<room>_BG_walk.mp4`, `clips/<room>_AK_walk.mp4` (5 s each).
- `shots/<room>/`: every frame and mask.
- `run1/`: the first run, the same names.

Tools: `tools/ak_hud_check.py` (new), `tools/ak_lut.py` (the guards, `--params`), `tools/ak_sheets.py` (the fault table).

## Open for Bruno

1. Which switches to keep, and for which rooms (PLAN.md §4). The cheap ones with a clear effect: the rim, the wet
   floor, the suits, the practicals. The planar reflection costs +4 to +7 ms.
2. The grade: with the figures and the black walls held at B-game (run 2), AK's colour numbers are out of reach (the
   distance is 2.7 dark, 2.2 bright; run 1 reached 0.4 and 0.7 by lifting everything). Keep the act 1 rule and
   take AK's highlights and accents only, or allow a brighter, more coloured frame in some rooms.
3. Whether the concept art should follow, or only the game.
