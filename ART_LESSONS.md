# THE GLORIFIED BODY: art lessons (read before any art work)

What we learned about the look, the characters, the sets and the cutscenes, 2026-10-17 to 2026-10-20 (tests T12 to
T17). Each rule cost us hours once. Full detail: `the-glorified-body/tests/T15_3d_set/LESSONS_LEARNED.md` and
`the-glorified-body/02_FEASIBILITY_AND_TESTS.md`.

## 1. The look (Bruno's rulings)

1. It LOOKS like a late-PS1 game. It is not one. An "upscaled PS1 game": clean 720p, no painterly look, no VHS look.
2. The detail lives in the TEXTURE, not in the mesh. That is why faces need no lip sync and no emotes.
3. Hard shadows. Never soften them.
4. Light like Parasite Eve II and Resident Evil 2: point lights and spots, hard pools of light, near-black corners,
   no bounce light.
5. The world: concrete, conduits, steel trim, grilles, tiled textures, hard highlights. Nature is TAMED: planters
   only, no ivy. Patient rooms are clean, bright and tiled. The cult emblem is a white gear on black. Intake and
   reception rooms have a CRT TV for propaganda.
6. Camera: inside the room, at a level angle. Bruno rejected the high, tilted render angle.
7. Fonts Bruno likes (Galerians guidebook): Anton, Oswald, Zen Old Mincho Black, EB Garamond.
8. (2026-10-22) The detail of 1999 came from greebles on the walls and from the texture: Parasite Eve II and
   Galerians painted the relief a normal map gives today (ribbed panels, grooves, trims). On our concrete the
   greebles stay subtle: weathering, cracks, panel seams, tie holes, stains, chips, plus skirting, steel bands,
   grilles, conduits and junction boxes as geometry. Textures at 2K (scale down only when a number says so).
   Clarity first: trilinear, anisotropy 8, no compression.
9. (2026-10-22) Every floor point of a room is in the frame of the camera its zone picks. A section without a
   camera is a bug (Bruno walked into one in the Gallery). The PlayMode coverage test guards every look room.

## 2. Concept art: what to ask for

| Do | Do not | Why |
|---|---|---|
| ONE cast sheet with every character in one picture, one style (the pick: `lineup_A_cg1999_nbp_1.png`) | One picture per character | Each picture picks its own style, palette and light: "three characters from three games" |
| A late-1990s CG cutscene style | Pixel-art concepts | 3D Studio copies the style it gets: noise in the texture, blurred faces |
| A single-figure picture DEAD-FRONT and symmetric | A turned head | A turned head gives crooked eyes on the model |
| Say "no outlines, no cel shading" | Leave the style open | Tom's first picture came back as a line drawing |
| Normal proportions | "Big head, short legs" | It made Lucy chibi |
| Matte skin, a muted palette | Wet gloss, saturated colours | The gloss and the colours bake into the texture |

## 3. Characters (3D Studio)

The recipe that works:
1. Image to 3D from the dead-front picture, `faceLimit: 2500`. This is the in-game tier.
2. The full mesh, with no face limit, is the CUTSCENE tier.
3. Run the separate DETAILED texture stage with the dead-front picture as the guide. The texture bundled with
   `generate` damages one side of the face (Tom, Ruth and Lucy all had it).
4. After a re-texture: extract the base colour and swap it onto the rigged file. Never rig again: the animate stage
   returns the OLD cached file (36,359 tokens lost once).
5. At import (`kit/ps1_tweak.py`): `fix_normals` first (the "triangular shades" on every face were split vertices at
   the texture seams), then `flatten_lips`, `move_nose_tip` onto the painted nose, `recess_lower_face`. Then
   `restore_hair` where the re-texture painted hair as skin.
6. Measure the painted rows (eyes, nose, mouth, chin) before you move any vertex. Put the features THERE.
7. Accept about 5 mm of asymmetry in a low-poly face. No post fix removed it without a new fault.

What failed, never again:

| Try | Result |
|---|---|
| 1,000-face characters | Deformed faces: sunken nose, long jaw |
| Decimating to 1,500 triangles | Seams torn through the texture |
| PS1 vertex jitter on characters | A wobble Bruno rejected |
| Our own Blender heads (3 versions) | Balloon head, no neck, nose too high, no chin, zig-zag lips |
| A portrait painted into the face texture | A colour step at the jaw, "awful" |
| Mirroring the mesh or the texture of a face | A crumpled cheek, speckles, hair patches |
| A soft lift of one low cheek vertex | A smirk |
| A second 3D Studio build to fix symmetry | Worse: 14 mm asymmetry instead of 4.6 mm |
| Clearing the custom normals alone | Worse facets |

Costs: a character with a dead-front picture from the start is about 140,000 tokens ($2.80). With a second texture
it is about 230,000 ($4.60).

## 4. Sets and props

1. A painted AI background works only for a walk on open floor. For contact (people at a table, on chairs) it fails:
   every leg, chair and edge needs a hand-made 3D stand-in, and each one is a few pixels off. We lost two days to
   that. In the game, the engine does the occlusion: build real 3D rooms.
2. AI makes only the PARTS ("form 1"): tileable textures, decals, screens, posters, prop pictures, concept boards.
   The 3D set is the source of truth, reused with any camera and any light.
3. An AI painting of a room is a good REFERENCE for layout, materials and light. The theatre board
   (`T16/assets/board_theatre_1.png`) was the best picture of all the tests.
4. 3D Studio props arrive in any orientation and with surprises. Check each one in a viewer before you place it:
   - Text-to-3D props lie along y.
   - The table carried a 13 cm apron under the top: adult thighs hit it.
   - The "ring machine" came as a drum 0.8 m tall.
   - The "brain" came with eyes on stalks.
5. Props cost 20,000 tokens from text, 30,000 from a picture. A free CC0 model imports for 0.

## 5. Staging and checks

1. The furniture fits the pose, never the pose the furniture. A solver moves the chair, never the body.
2. Put hands on a surface by IK, with the wrist 5 cm above it.
3. Check every contact with a NUMBER (vertices inside furniture = 0), not by eye.
4. Check every frame at 2x before Bruno sees it. He found eight errors before the agent did.
5. Pose a limb by aiming the bone in world space. Per-axis bone turns failed on Lucy's rig.

## 6. Cutscenes

1. Stills: Nano Banana Pro, with voice acting over them.
2. Animated cuts: Blender renders the first and the last frame, Seedance makes the motion between them. The clip
   lands on our frames, so the next shot starts where it ends.
3. Seedance 2.5 draft mode read the prompts most literally. Iterate there: 9,240 tokens a second at 480p.
4. A Nano Banana Pro redraw of a Blender frame makes a detailed END frame (the muscles on the endoskeleton,
   16,000 tokens).
5. The organic motion (skin, flesh) belongs to the video model. A cloth peel in Blender is 1 to 2 days for what
   Seedance made in 3 minutes.
6. EEVEE and Cycles read the same lights differently: calibrate the brightness per engine, or the cut jumps.
7. No refusal at 480p in 13 clips with a person on the table, Lucy included. This is a test result, not a promise.

## 7. Working rules

1. Renders and big files go in `~/glorified-body-renders/`, never inside `~/MEGA`.
2. One Blender render at a time, through `kit/run_blender.sh` (RAM cap). Unity through its wrapper.
3. Show Bruno a picture or a clip with every result. Never "it should look fine".
