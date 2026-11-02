#!/usr/bin/env bash
# T26 remake look (restart): the contact sheets, every frame at full size (1920 x 1080).
#   tools/remake_sheets.sh <run shots dir> <out dir>
# Writes <out>/AB_<camera>.png (A beside B with all ten switches on), <out>/ablation_<camera>.png (B all on, each
# switch off alone, all off; 4 x 3) and copies every shot, mask and the 1440p sample into <out>/shots/.
set -u
S="${1:?shots dir}"; OUT="${2:?out dir}"
mkdir -p "$OUT/shots"
cp "$S"/remake_*.png "$S"/mask_*.png "$OUT/shots/" 2>/dev/null
names=("" "1 bake" "2 shadow maps" "3 probes + contact" "4 surfaces + decals" "5 light in the air" "6 grade" "7 live layers" "8 reflections" "9 clarity (no dither, AA)" "10 long lens")
for cam in cam_recovery cam_recovery_bed cam_recovery_long; do
  a="$OUT/shots/remake_A_$cam.png"; b="$OUT/shots/remake_B_$cam.png"
  if [ -f "$a" ] && [ -f "$b" ]; then
    montage -label "A: the current look (Simple Lit, baked bounce, Authentic dither, game characters)" "$a" \
      -label "B: the remake look, all ten switches on" "$b" \
      -tile 2x1 -geometry 1920x1080+8+8 -pointsize 40 -background '#202020' -fill white "$OUT/AB_$cam.png" && echo "[remake_sheets] $OUT/AB_$cam.png"
  fi
  args=(-label "B all on" "$b")
  for i in $(seq 1 10); do
    f="$OUT/shots/remake_B_off${i}_$cam.png"; [ -f "$f" ] && args+=(-label "off: ${names[$i]}" "$f")
  done
  f="$OUT/shots/remake_B_alloff_$cam.png"; [ -f "$f" ] && args+=(-label "all ten off (B characters)" "$f")
  montage "${args[@]}" -tile 4x3 -geometry 1920x1080+8+8 -pointsize 40 -background '#202020' -fill white "$OUT/ablation_$cam.png" && echo "[remake_sheets] $OUT/ablation_$cam.png"
done
