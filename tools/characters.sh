#!/bin/bash
# The T19 characters into the project: GLB (3D Studio) to FBX plus the base colour, one class at a time.
#
#   tools/characters.sh [class ...]     default: the nine demo classes
#
# Source: ~/glorified-body-renders/T19_characters/models/<class>_animated.glb (the rig and the actions) and
#         <class>_game.glb (the base colour). Output: Assets/_Game/Characters/Models/<class>.fbx,
#         <class>_basecolor.png|jpg and <class>.clips.json (the travel of each clip). Then the Unity side:
#   tools/unity.sh exec GlorifiedBody.Editor.Characters.CreateCharacterAssets.BuildAll
# (the import settings, the materials, the animator controllers, the prefabs, the catalog, the scenes).
# Blender runs through the RFab kit wrapper (one at a time, a RAM cap), or straight from the snap without it.
set -u
TOP="$(cd "$(dirname "$0")/.." && pwd)"
SRC="${GB_CHARACTER_SRC:-$HOME/glorified-body-renders/T19_characters/models}"
OUT="$TOP/Assets/_Game/Characters/Models"
RUNNER="${GB_BLENDER_RUNNER:-$HOME/MEGA/code/reality-fabricator/the-glorified-body/kit/run_blender.sh}"
BLENDER="${GB_BLENDER:-/snap/blender/current/blender}"
classes=("$@")
[ ${#classes[@]} -eq 0 ] && classes=(lucy_bare lucy_guard patient nurse labworker guard soldier fireman power_armour)
mkdir -p "$OUT"
fail=0
for c in "${classes[@]}"; do
  anim="$SRC/${c}_animated.glb"; game="$SRC/${c}_game.glb"
  if [ ! -f "$anim" ] || [ ! -f "$game" ]; then echo "[characters] $c: missing $anim or $game"; fail=1; continue; fi
  base=$(python3 "$TOP/tools/characters_basecolor.py" "$game" "$OUT/${c}_basecolor") || { fail=1; continue; }
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
