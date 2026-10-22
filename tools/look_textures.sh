#!/usr/bin/env bash
# The textures of the look test (step 4), made with RFab (the game is RFab's dogfood, CLAUDE.md rule 9).
# Four surfaces on seegen:nano-banana-pro, one picture each (2026-10-22: the default model drew scenes, not
# surfaces, RFAB_GAPS 11; Nano Banana gave real 1024 px surfaces at $0.32 each). The gear emblem comes from
# tools/look_textures_procedural.py (a white gear on black, drawn as shapes).
# Output: ~/glorified-body-renders/game/look_test/textures/nb/<name>_1.png, the log and the cost line.
#   tools/look_textures.sh          make the four pictures (skips the ones already made)
#   tools/look_textures.sh import   copy them into Assets/_Game/Rendering/Textures/<name>.png
# Needs the RFab API key in ~/.rfab-api-key (never printed).
set -u
RFAB="node /home/pc/MEGA/code/reality-fabricator/rfab-backend/scripts/ops/rfab.js"
OUT="$HOME/glorified-body-renders/game/look_test/textures/nb"
TOP="$(cd "$(dirname "$0")/.." && pwd)"
ASSETS="$TOP/Assets/_Game/Rendering/Textures"
MODEL="seegen:nano-banana-pro"
export RFAB_API_KEY
RFAB_API_KEY=$(tr -d '[:space:]' < "$HOME/.rfab-api-key")
mkdir -p "$OUT"
NAMES=(concrete_wall concrete_floor steel_panel soil)

if [ "${1:-make}" = import ]; then
  mkdir -p "$ASSETS"
  for n in "${NAMES[@]}"; do
    f="$OUT/${n}_1.png"
    if [ -f "$f" ]; then cp "$f" "$ASSETS/$n.png" && echo "[look_textures] $n.png imported"; else echo "[look_textures] $n: no picture"; fi
  done
  exit 0
fi

SURFACE="seamless tileable texture, straight on, flat even light, photographic, the whole frame is the surface, no objects, no room, no people"
declare -A PROMPT=(
  [concrete_wall]="A raw poured concrete wall surface, mid grey, fine aggregate speckle, faint horizontal formwork lines, a few small air holes, $SURFACE"
  [concrete_floor]="A sealed grey concrete floor surface seen from straight above, smooth with faint scuffs and darker patches, $SURFACE"
  [steel_panel]="A brushed dark steel panel surface, fine horizontal grain, one row of small rivets along a seam, $SURFACE"
  [soil]="Dark moist potting soil surface seen from straight above, small bark pieces, $SURFACE"
)

$RFAB cost start look_textures 2>&1 | tail -1
for n in "${NAMES[@]}"; do
  if [ -f "$OUT/${n}_1.png" ]; then echo "[look_textures] $n: already made"; continue; fi
  echo "[look_textures] $n $(date +%T)"
  $RFAB image "${PROMPT[$n]}" --out "$OUT/$n" --shape square --n 1 --model "$MODEL" --log "$OUT/rfab.jsonl" 2>&1 | tail -2
done
$RFAB cost stop look_textures 2>&1 | tail -1 | tee "$OUT/cost.txt"
ls -la "$OUT"
