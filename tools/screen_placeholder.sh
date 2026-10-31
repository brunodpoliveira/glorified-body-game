#!/usr/bin/env bash
# The placeholder clip of the in-world screens (2026-10-31, Bruno: "in-world screens (a CRT clip with sound)").
# Made here with ffmpeg alone, safe for the public repo: a green card with a title, a moving bar, a timecode, and
# a robot voice (ffmpeg's flite) that reads the placeholder lines at their times over a soft hiss. The real clips come
# with the writer's words (the rehearsal tape of W7, the orientation program of G7, 15 to 30 s each).
#
# The line times must match ScreenTestSetup.Lines (the subtitles). VP8 + Vorbis in webm: the Linux editor has no H.264.
# Output: Assets/_Game/Screens/screen_placeholder.webm
#
# Usage: tools/screen_placeholder.sh
set -euo pipefail
TOP="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$TOP/Assets/_Game/Screens/screen_placeholder.webm"
mkdir -p "$(dirname "$OUT")"
FONT=/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf
LEN=20

# (start in seconds, words): the same as ScreenTestSetup.Lines.
LINES=(
  "0.8|This is a placeholder clip on a screen."
  "5.5|Its sound comes from the screen, in the room."
  "10.5|The writer gives the words. The real clip comes later."
  "15.5|A pause stops it with the world."
)

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
inputs=()
mix=""
i=0
for row in "${LINES[@]}"; do
  at="${row%%|*}"
  words="${row#*|}"
  ms=$(python3 -c "print(int(float('$at')*1000))")
  ffmpeg -hide_banner -loglevel error -y -f lavfi -i "flite=text='$words':voice=slt" -ar 44100 -ac 1 "$TMP/line$i.wav"
  inputs+=(-i "$TMP/line$i.wav")
  mix+="[$((i + 1)):a]adelay=${ms}|${ms},volume=0.9[l$i];"
  i=$((i + 1))
done
labels=""
for ((j = 0; j < i; j++)); do labels+="[l$j]"; done

ffmpeg -hide_banner -loglevel error -y \
  -f lavfi -i "anoisesrc=d=$LEN:c=pink:r=44100:a=0.02" \
  "${inputs[@]}" \
  -f lavfi -i "color=c=0x24603a:s=640x480:d=$LEN:r=25" \
  -filter_complex "${mix}[0:a]${labels}amix=inputs=$((i + 1)):normalize=0,lowpass=f=5000,highpass=f=120,atrim=0:$LEN[a];\
[$((i + 1)):v]drawbox=x=0:y='mod(t*60,480)':w=640:h=40:color=0x5fbf7a@0.5:t=fill,\
drawtext=fontfile=$FONT:text='SCREEN TEST':fontcolor=0xe8ffe8:fontsize=84:x=(w-tw)/2:y=120,\
drawtext=fontfile=$FONT:text='PLACEHOLDER CLIP':fontcolor=0xc8ffc8:fontsize=44:x=(w-tw)/2:y=230,\
drawtext=fontfile=$FONT:text='%{pts\:hms}':fontcolor=0xc8ffc8:fontsize=48:x=(w-tw)/2:y=320[v]" \
  -map "[v]" -map "[a]" -c:v libvpx -b:v 900k -c:a libvorbis -q:a 4 -t $LEN "$OUT"
echo "$OUT: $(ffprobe -v error -show_entries format=duration -of csv=p=0 "$OUT") s"
