#!/usr/bin/env bash
# The textures of the look test (step 4), made with RFab (the game is RFab's dogfood, CLAUDE.md rule 9).
# Surfaces on seegen:nano-banana-pro at 2K, one picture each (Bruno, 2026-10-22: "i prefer 1080p textures, we
# can scale it down if necessary. I want clarity as much as possible"). 2026-10-22, the first night: the default
# model drew scenes, not surfaces (RFAB_GAPS 11); Nano Banana gave real surfaces at 1024 px for $0.32 each.
# The detail of 1999 lives in the texture (Bruno: "they added a lot of grebbles to walls, textures to add
# detail... on concrete more subtle: weathering, cracks, concrete tiles"): the prompts ask for seams, stains,
# cracks and chips with a slight shadow, so the picture carries the relief a normal map would.
# The gear emblem, the CRT face and the light panel come from tools/look_textures_procedural.py.
# Output: ~/MEGA/glorified-body-renders/game/look_test/textures/nb2k/<name>_1.png, the log and the cost line.
#   tools/look_textures.sh          make the pictures (skips the ones already made)
#   tools/look_textures.sh import   copy them into Assets/_Game/Rendering/Textures/<name>.png (the fern keyed to alpha)
# Needs the RFab API key in ~/.rfab-api-key (never printed).
set -u
RFAB="node /home/pc/MEGA/code/reality-fabricator/rfab-backend/scripts/ops/rfab.js"
OUT="$HOME/MEGA/glorified-body-renders/game/look_test/textures/nb2k"
TOP="$(cd "$(dirname "$0")/.." && pwd)"
ASSETS="$TOP/Assets/_Game/Rendering/Textures"
MODEL="seegen:nano-banana-pro"
export RFAB_API_KEY
RFAB_API_KEY=$(tr -d '[:space:]' < "$HOME/.rfab-api-key")
mkdir -p "$OUT"
NAMES=(concrete_wall concrete_floor concrete_ceiling steel_panel soil tile_wall tile_floor resin_floor vent_grille fern)

if [ "${1:-make}" = import ]; then
  mkdir -p "$ASSETS"
  for n in "${NAMES[@]}"; do
    f="$OUT/${n}_1.png"
    if [ ! -f "$f" ]; then echo "[look_textures] $n: no picture"; continue; fi
    if [ "$n" = fern ]; then
      python3 -I "$TOP/tools/look_fern_alpha.py" "$f" "$ASSETS/fern.png" && echo "[look_textures] fern.png imported (keyed)"
    else
      cp "$f" "$ASSETS/$n.png" && echo "[look_textures] $n.png imported"
    fi
  done
  exit 0
fi

SURFACE="seamless tileable texture, straight on, flat even light with a soft light from the top so every seam, chip and hole carries a slight shadow, photographic, sharp, the whole frame is the surface, no objects, no room, no people, no text"
declare -A PROMPT=(
  [concrete_wall]="A raw precast concrete panel wall surface, mid grey, fine aggregate speckle, thin recessed seam lines between large rectangular panels, small round tie holes in a row, faint water stains that run down, a few hairline cracks, subtle weathering, $SURFACE"
  [concrete_floor]="A sealed grey concrete floor surface seen from straight above, smooth, a thin saw-cut joint grid, faint scuffs, darker patches and one faint oil stain, $SURFACE"
  [concrete_ceiling]="A rough board-formed concrete ceiling surface seen from straight below, mid grey, the grain of the wooden boards pressed into it, small air holes, faint dust, $SURFACE"
  [steel_panel]="A brushed dark steel panel surface, fine horizontal grain, one row of small rivets along a seam, faint scratches, $SURFACE"
  [soil]="Dark moist potting soil surface seen from straight above, small bark pieces, $SURFACE"
  [tile_wall]="White glazed ceramic wall tiles, 15 cm square, dark grey grout lines, a slight gloss variation from tile to tile, a few small chipped corners, faint grime in the grout, $SURFACE"
  [tile_floor]="Pale grey ceramic floor tiles seen from straight above, 30 cm square, dark grout lines, a matte sealed surface with faint scuffs, $SURFACE"
  [resin_floor]="A pale grey-green sealed resin floor surface seen from straight above, smooth, faint scuffs and a few dark drag marks, $SURFACE"
  [vent_grille]="A rectangular steel louvred vent grille, horizontal slats, painted light grey, a thin frame with four screws, the grille fills the whole frame, straight on, flat even light, photographic, sharp, no text"
  [fern]="A trimmed indoor fern plant, many fronds spread flat and seen straight on, bright green, on a pure black background, no pot, no soil, no shadow, photographic, sharp, no text"
)

$RFAB cost start look_textures_2k 2>&1 | tail -1
for n in "${NAMES[@]}"; do
  if [ -f "$OUT/${n}_1.png" ]; then echo "[look_textures] $n: already made"; continue; fi
  echo "[look_textures] $n $(date +%T)"
  # --res 2k is a raised tier: the API wants the reason (TIER_NEEDS_REASON). Bruno asked for 2K after he saw the 1K set.
  $RFAB image "${PROMPT[$n]}" --out "$OUT/$n" --shape square --res 2k --n 1 --model "$MODEL" \
    --tier-reason "Bruno asked for 2K surfaces for clarity after he saw the 1K set of 2026-10-22" --log "$OUT/rfab.jsonl" 2>&1 | tail -2
done
$RFAB cost stop look_textures_2k 2>&1 | tail -1 | tee "$OUT/cost.txt"
ls -la "$OUT"
