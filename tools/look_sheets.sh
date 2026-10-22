#!/usr/bin/env bash
# The contact sheets of the look test: one per camera, the three dressings side by side (greybox, real time,
# plate) at 960 x 540, plus the walk shots. Usage: tools/look_sheets.sh <run shots dir> <out dir>
set -u
SHOTS="${1:?shots dir}"; OUT="${2:?out dir}"
mkdir -p "$OUT"
cp "$SHOTS"/look_*.png "$OUT"/ 2>/dev/null
for cam in cam_wide cam_mid cam_door walk; do
  a="$OUT/look_greybox_$cam.png"; b="$OUT/look_realtime_$cam.png"; c="$OUT/look_plate_$cam.png"
  [ -f "$a" ] && [ -f "$b" ] && [ -f "$c" ] || { echo "[look_sheets] $cam: a shot is missing"; continue; }
  montage -label 'greybox' "$a" -label 'real time' "$b" -label 'plate' "$c" -tile 3x1 -geometry 960x540+6+6 -pointsize 28 "$OUT/sheet_$cam.png"
  echo "[look_sheets] $OUT/sheet_$cam.png"
done
# real time against plate at full size, the wide camera: the pair Bruno compares first
[ -f "$OUT/look_realtime_cam_wide.png" ] && [ -f "$OUT/look_plate_cam_wide.png" ] && \
  montage -label 'real time' "$OUT/look_realtime_cam_wide.png" -label 'plate' "$OUT/look_plate_cam_wide.png" -tile 1x2 -geometry 1920x1080+6+6 -pointsize 36 "$OUT/pair_cam_wide.png" && echo "[look_sheets] $OUT/pair_cam_wide.png"
