#!/bin/bash
# The characters into the project: GLB (3D Studio) to FBX plus the maps, one class at a time.
#
#   tools/characters.sh [class ...]     default: the nine demo classes
#
# Source: ~/MEGA/glorified/renders/T19_characters/models/<class>_animated.glb (the rig and the actions) and
#         <class>_game.glb (the base colour). Output: Assets/_Game/Characters/Models/<class>.fbx,
#         <class>_basecolor.png|jpg and <class>.clips.json (the travel of each clip). Then the Unity side:
#   tools/unity.sh exec GlorifiedBody.Editor.Characters.CreateCharacterAssets.BuildAll
# (the import settings, the materials, the animator controllers, the prefabs, the catalog, the scenes).
# Blender runs through the RFab kit wrapper (one at a time, a RAM cap), or straight from the snap without it.
#
# One-file classes (T49, the final models): one passed game GLB holds the rig, the clips (in place) and the maps
# (base colour, normal, metal-rough, 2048; one set for each mesh). The script reads it as both files and carries
# all three maps (characters_basecolor.py --maps). A T19 class keeps the base colour only (its normal map ghosts the paint).
set -u
TOP="$(cd "$(dirname "$0")/.." && pwd)"
SRC="${GB_CHARACTER_SRC:-$HOME/MEGA/glorified/renders/T19_characters/models}"
OUT="$TOP/Assets/_Game/Characters/Models"
RUNNER="${GB_BLENDER_RUNNER:-$HOME/MEGA/glorified/production/kit/run_blender.sh}"
BLENDER="${GB_BLENDER:-/snap/blender/current/blender}"
T49="$HOME/MEGA/glorified/renders/T49_characters/models"
declare -A ONE_FILE=(
  [lucy_bare]="$T49/lucy_bare/v2/lucy_t49v2_game.glb"   # rounds 175, 178: the body and the T27 Crown
  [guard]="$T49/guard_v2/guard_t49_game.glb"             # round 178 (T26 full mesh + T27 bake)
  [patient]="$T49/patient_v2/patient_t49_game.glb"       # round 178
  [nurse]="$T49/nurse_v2/nurse_t49_game.glb"             # round 178
  [lucy_guard]="$T49/lucy_guard_v2/lucy_guard_t49_game.glb"    # round 179: the body and the T27 Crown
  [lodge_staff]="$T49/lodge_staff_v2/lodge_staff_t49_game.glb" # round 179: the night manager of G19
  [toll_guard]="$T49/toll_guard_v2/toll_guard_t49_game.glb"    # round 179: the navy guard of R3, no vest
  [labworker]="$T49/labworker_v2/labworker_t49_game.glb"       # round 181 (the suit keeps its baked gloss)
  [soldier]="$T49/soldier_v2/soldier_t49_game.glb"             # round 181
  [fireman]="$T49/fireman_v2/fireman_t49_game.glb"             # round 181
)
classes=("$@")
[ ${#classes[@]} -eq 0 ] && classes=(lucy_bare lucy_guard patient nurse labworker guard soldier fireman power_armour lodge_staff toll_guard)
mkdir -p "$OUT"
fail=0
for c in "${classes[@]}"; do
  anim="$SRC/${c}_animated.glb"; game="$SRC/${c}_game.glb"; maps=""
  if [ -n "${ONE_FILE[$c]:-}" ]; then anim="${ONE_FILE[$c]}"; game="$anim"; maps="--maps"; fi
  if [ ! -f "$anim" ] || [ ! -f "$game" ]; then echo "[characters] $c: missing $anim or $game"; fail=1; continue; fi
  # A class without the full map set loses the extra maps of an earlier run.
  [ -z "$maps" ] && rm -f "$OUT/${c}_normal.png" "$OUT/${c}_normal.png.meta" "$OUT/${c}_ms.png" "$OUT/${c}_ms.png.meta"
  base=$(python3 "$TOP/tools/characters_basecolor.py" "$game" "$OUT/${c}_basecolor" $maps) || { fail=1; continue; }
  # The travel of each clip (the hips over a cycle), read by the Unity import: a clip that travels gives its
  # travel to the root motion, a clip that stays keeps its root in the pose (the slide-back of 2026-10-21).
  python3 "$TOP/tools/characters_clips.py" "$anim" "$OUT/${c}.clips.json" || { fail=1; continue; }
  rm -f "$OUT/${c}.fbx"
  if [ -x "$RUNNER" ]; then
    "$RUNNER" -b --factory-startup -P "$TOP/tools/characters_export.py" -- "$anim" "$OUT/${c}.fbx" "$base" > "$OUT/${c}_export.log" 2>&1
  else
    "$BLENDER" -b --factory-startup -P "$TOP/tools/characters_export.py" -- "$anim" "$OUT/${c}.fbx" "$base" > "$OUT/${c}_export.log" 2>&1
  fi
  if [ -s "$OUT/${c}.fbx" ]; then
    grep "\[characters_export\]" "$OUT/${c}_export.log"
  else
    echo "[characters] $c: no FBX, see $OUT/${c}_export.log"; tail -5 "$OUT/${c}_export.log"; fail=1
  fi
done
# The logs are not assets: keep them out of Unity's import.
mkdir -p "$TOP/runs"; mv -f "$OUT"/*_export.log "$TOP/runs/" 2>/dev/null
exit $fail
