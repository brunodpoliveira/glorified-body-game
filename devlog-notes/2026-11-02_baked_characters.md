# 2026-11-02: the baked game characters (T27)

Bruno (2026-11-01, night, design §28a items 7 to 9): the game meshes take more faces with the detail of the full mesh
baked in; Lucy gets the most faces, on a female base body from outside with our C3 paint; the helmet smoother, with
scratches, above 2,500 faces. Full meshes stay for cutscenes.

Full report: `~/MEGA/glorified/production/tests/T27_baked_characters/RESULT.md`.
Sheets and Unity shots: `/home/pc/MEGA/glorified/renders/T27_baked_characters/` (`sheets/`, `unity/`).

## What is in the project (new files only, nothing old changed, not committed)

- `Assets/_Game/Characters/T27/`: `t27_guard.fbx` (8,999 triangles), `t27_lucy.fbx` (body 18,000 + the helmet 2,800,
  the helmet skinned 100 percent to `mixamorig:Head`), `t27_crown.fbx` (the helmet alone), the 2048 maps
  `t27_<id>_base / _n / _ms` (+ `_ao` for the guard and the helmet), the URP Lit materials `T27_<id>.mat`, and the
  prefabs `Resources/T27/t27_guard.prefab` and `t27_lucy.prefab` (the guard and lucy_bare controllers).
- `Scripts/Editor/T27BakedCharacters.cs`: `tools/unity.sh exec GlorifiedBody.Editor.T27BakedCharacters.Import`.
- `Scripts/Automation/T27Playthrough.cs`, bot "t27": `tools/unity.sh play RemakeLookBright Main_Menu t27 600` and
  `tools/unity.sh play RemakeLook Main_Menu t27 600`. The old pair, the new pair on the same spots and all four, at the
  three cameras, B-game, 2048 maps; then the frame time. The scene is not saved (T26 stays as it is).

## Proof

Both bot runs PASS, triage CLEAN. Frame time at Cam_Recovery, 1080p, GTX 1050 (lowest of three rounds):

| Room | Old pair | New pair |
|---|---|---|
| RemakeLookBright (air off) | 11.40 ms | 12.03 ms |
| RemakeLook (air on) | 20.04 ms | 20.28 ms |

## Fix pass (2026-11-02, night)

The helmet's mouth was smeared: the high helmet's subdivision had smoothed the old UVs. The old paint is now projected
from the old flat helmet onto the high mesh, and the game helmet has one UV band that holds the eyes, the slit and
the mouth (no seam crosses them). Lucy's bare head is plain skin, the star above the back of her knee is gone, the
veins are sunk as deep as T20 subtle, the guard's cloth is capped at smoothness 0.25. Both bots PASS, triage CLEAN:
bright 11.49 / 11.95 ms (old pair / new pair), dark 20.16 / 20.31 ms. The bot checks the helmet in play (skinned to
`mixamorig:Head` only, the right way round, the 2048 maps, `_NORMALMAP`). Details: RESULT.md, "Fix pass".

## Fix 3: the gold (2026-11-02, day)

Bruno: the veins are "closer to yellow rather than gold", "darker, more metallic and less painted-on". Measured: the
gold was one flat yellow (sRGB 255, 200, 58) at metal 0.6. Now it is a darker gold (median 181, 141, 76), bright in
the middle of each vein and dark at its edges, at metal 0.9 and smoothness 0.65 (the C3 value). Only the gold texels
of `t27_lucy_base.png` and `t27_lucy_ms.png` changed. The veins and the normal map stay as they are. Script:
`~/MEGA/glorified/production/tests/T27_baked_characters/lucy_gold_fix.py`. Sheet: `sheets/lucy_gold_fix3.png`.
Both bots PASS, triage CLEAN: bright 11.73 / 12.48 ms, dark 21.04 / 21.42 ms (old pair / new pair). Unity sheet at 2x:
`sheets/lucy_gold_fix3_unity.png`.
