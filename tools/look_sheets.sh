#!/usr/bin/env bash
# The contact sheets of a look test run: one per camera, the four dressings side by side (greybox, real time,
# real time + baked, plate) at 960 x 540, plus the walk shots, plus the pair real time against plate at full
# size for the default camera. Usage: tools/look_sheets.sh <run shots dir> <out dir> <default camera, e.g. cam_wide>
set -u
SHOTS="${1:?shots dir}"; OUT="${2:?out dir}"; MAIN="${3:-cam_wide}"
mkdir -p "$OUT"
cp "$SHOTS"/look_*.png "$OUT"/ 2>/dev/null
cams=$(ls "$OUT"/look_plate_*.png 2>/dev/null | sed 's/.*look_plate_//; s/\.png$//')
for cam in $cams; do
  a="$OUT/look_greybox_$cam.png"; b="$OUT/look_realtime_$cam.png"; c="$OUT/look_realtimebaked_$cam.png"; d="$OUT/look_plate_$cam.png"
  [ -f "$a" ] && [ -f "$b" ] && [ -f "$c" ] && [ -f "$d" ] || { echo "[look_sheets] $cam: a shot is missing"; continue; }
  montage -label 'greybox' "$a" -label 'real time' "$b" -label 'real time + baked bounce' "$c" -label 'plate' "$d" -tile 4x1 -geometry 960x540+6+6 -pointsize 28 "$OUT/sheet_$cam.png"
  echo "[look_sheets] $OUT/sheet_$cam.png"
done
# real time against plate at full size, the default camera: the pair Bruno compares first
[ -f "$OUT/look_realtime_$MAIN.png" ] && [ -f "$OUT/look_plate_$MAIN.png" ] && \
  montage -label 'real time' "$OUT/look_realtime_$MAIN.png" -label 'plate' "$OUT/look_plate_$MAIN.png" -tile 1x2 -geometry 1920x1080+6+6 -pointsize 36 "$OUT/pair_$MAIN.png" && echo "[look_sheets] $OUT/pair_$MAIN.png"
