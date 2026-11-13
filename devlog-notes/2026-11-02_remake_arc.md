# 2026-11-01 to 2026-11-02: the REmake look, start to now (the index for the devlog)

One page for the devlog writer: the whole arc of the "2002 Resident Evil remake" look, in order, with the detail
notes and reports for each step. Read this first, then the step notes.

## 1. The question (2026-11-01, afternoon)

Bruno asked how the GameCube Resident Evil remake (2002) made its pictures, and whether we can get that look. His
questions: how the remake did it, the hard stencil shadows, whether more polygons fix the face texture, a facial
rig, and RFab still pictures for emotes.

The short answer we gave: the remake drew its rooms ahead of time as pre-rendered pictures, one for each fixed
camera. The characters were real-time 3D on top, and hidden depth data let them pass behind the room objects. That
method gives a rich room but a frozen light, and it is tied to one resolution.

His rulings, the start of everything after:
- "the objective is to recreate the result", not the method. "we'll not restrict ourselves to 720p either."
- "real-time 3d logic that emulates pre-rendered look." A real 3D room, rendered live, with no plates and no AI
  backgrounds.
- He asked for a test with the RFab API and Unity to see if the new look is better.

## 2. T26: the A/B look test (2026-11-01, evening)

Note: `2026-11-01_remake_look.md`. A is the old look, B is real time that aims for the remake's result. The first
pass used the wrong look (white tiles, the navy guard), so Bruno stopped it. It ran again in the black room of act 1,
with the light-blue guard and the C3 Lucy.

Bruno's picks (round 23, design §28a, CANON):
1. B wins.
2. No dither. Anti-aliasing stays on. The expensive effects go and the cheap ones stay.
3. The full meshes are for cutscenes only.
4. Most rooms are bright LED hospital rooms. Dark rooms are only in abandoned areas. The black materials stay.
5. The light in the air goes only in some rooms (a flashlight, a flood light, emergency lights). It must be
   adjustable later (direction, origin, several in one room), not built now.

Frame times (GTX 1050, 1080p): B costs about 13 ms in the bright room and about 20 ms in the dark room with the air.
The light in the air is the big cost (9.5 to 12 ms).

## 3. T27: baked characters (2026-11-02, night)

Note: `2026-11-02_baked_characters.md`. Report: `~/MEGA/glorified/production/tests/T27_baked_characters/RESULT.md`.

Bruno's idea (design §28a items 7 to 9): more faces for the game meshes, with the detail of the full mesh baked in
("don't need to break the bank"). Lucy gets the most faces, on a free outside body, with our paint. The helmet gets
the same treatment.

What came out: the guard at 9,000 triangles, Lucy at 18,000 plus a 2,800 helmet. Lucy is on the CC0 Blender
"human base meshes" body (the RFab 3D service refused her three times). The cost in play is 0.2 to 0.5 ms a frame.

Bruno's answers (round 24, design §28b, CANON): the budgets stay, the veins are sunk through the normal map as on
C3, the gold joints with a groove stay, and the helmet maps stay at 2048. Gloss goes by material: masks may shine,
fabric may not, for every character from now on (ART_LESSONS §3 item 8).

The fix pass (night):
- **The helmet's mouth:** the paint was smeared and the teeth were doubled. The cause was a subdivision that slid the
  UVs. The fix copied the old paint onto the new helmet, with no UV seam through the mouth or the eyes.
- **Lucy:** her bare head is plain skin now, the gold "star" at the knee is gone, and the veins are sunk.
- **The guard:** his cloth is matte (smoothness 0.51 down to 0.26).

Fix 3 (2026-11-02, day), the gold: Bruno found the veins "closer to yellow rather than gold". They were one flat
yellow at half metal. Now they are a darker gold, bright in the middle of each vein and dark at its edges, at metal
0.9. Details: the T27 note, "Fix 3".

## 4. T28: act 2 concept art (2026-11-02, night, then the redo in the day)

Report: `~/MEGA/glorified/production/tests/T28_act2_concepts/RESULT.md`. Files: `/home/pc/MEGA/glorified/renders/T28_act2_concepts/`.

The brief: act 2 (the Swan) in winter Philadelphia: a city street, an apartment hallway and an elevated station. Some
street concepts match the camera of the snowy-alley reference exactly. The sky references set the sky, the light and
the colour.

- **Night run:** 15 concepts, $10.64, on Nano Banana Pro. The street concepts S1 to S4 match the reference camera
  (edge match 0.74 to 0.79).
- **Bruno's verdict:** they look good, but they look like photos, "it's hard to judge if it is good enough for the
  game or not". He wants the 3D CG look of the remake.
- **First try:** a redraw of the old picks as CG. Bruno rejected it because the photo tells stayed (the people, the
  train details): "the original concepts must be forgotten".
- **Second try, from scratch:** three new concepts, one for each scene. The photo references went in only as grey
  layout sketches (shrunk, median-filtered, posterized), so the model got the camera but no photo texture.
  Bruno: "Yes, they do" (they read as the game style).
- **Then:** "they need more dramatic shadows and contrast". One lighting edit for each picture. Bruno: "good enough
  to get the idea noted down, but I'm still not satisfied", "we'll circle back around it later".
- **What we learned:** an AI edit keeps the photo texture of its source. To get CG, start from text, and give the
  references only as layout. Describe the people as "2002 video game character models".

Open: the Splendor Solis livery (a proposal), the train that is still close to a SEPTA car, and three overlay files
with real logos that Bruno must delete.

## 5. T29: the Galerians fonts (2026-11-02, night)

Report: `~/MEGA/glorified/production/tests/T29_fonts/RESULT.md`. Sheets: `/home/pc/MEGA/glorified/renders/T29_fonts/sheets/`.

The source is the "STAFF'S COMENTS" page of the Galerians guidebook (the typo is in the print). The page has five
type styles. The free picks (SIL OFL), each with the way to set it:

| Style | Pick | How to set it |
|---|---|---|
| Headline | Roboto Condensed 700 | Squeezed to 64 % |
| Names | Archivo Narrow 450 | As published |
| "PROFILE" and its bar | Arimo 600 | Squeezed to 77 % |
| Body text | EB Garamond 450 | As published |
| Kanji names | Noto Serif JP Black | As published |

The real kanji font is likely Fontworks Matisse (the Evangelion title font), a commercial font.

Open: Bruno's pick, then a test in TextMeshPro at game resolution.
