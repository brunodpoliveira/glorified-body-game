#!/usr/bin/env bash
# The overnight devlog build. Reads the newest script versions of one episode, renders the narration on RFab,
# assembles the long video and the short, and leaves a review packet for the morning.
#
# Usage: tools/devlog/night.sh <ep> [--version N] [--record] [--skip-short]
#   <ep>        the episode folder name under the writers' devlog folder, for example ep00
#   --version N use long_vN.md and short_vN.md (default: the highest N present)
#   --record    first run the bot with video (tools/unity.sh record). Waits up to 8 h for the editor to close.
#   --skip-short  the long video only
# Scripts: $GB_SCRIPTS/<ep>/long_vN.md, short_vN.md (default: the-glorified-body/devlog in reality-fabricator)
# Out:     ~/glorified-body-renders/devlog/<ep>/vN/{long,short}/final.mp4, contact_sheet.png, REVIEW.md, NOTES.md
#          and ~/glorified-body-renders/devlog/<ep>/vN/night.log
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TOP="$(cd "$HERE/../.." && pwd)"
SCRIPTS="${GB_SCRIPTS:-/home/pc/MEGA/code/reality-fabricator/the-glorified-body/devlog}"
RENDERS="${GB_RENDERS:-$HOME/glorified-body-renders}"

ep="${1:?episode folder required, for example ep00}"; shift
version=""; record=0; skip_short=0
while [ $# -gt 0 ]; do
  case "$1" in
    --version) version="$2"; shift 2 ;;
    --record) record=1; shift ;;
    --skip-short) skip_short=1; shift ;;
    *) echo "[night] unknown option $1"; exit 2 ;;
  esac
done

epdir="$SCRIPTS/$ep"
[ -d "$epdir" ] || { echo "[night] no script folder $epdir"; exit 2; }
if [ -z "$version" ]; then
  version=$(ls "$epdir" | sed -n 's/^long_v\([0-9]*\)\.md$/\1/p' | sort -n | tail -1)
fi
[ -n "$version" ] || { echo "[night] no long_vN.md in $epdir"; exit 2; }
out="$RENDERS/devlog/$ep/v$version"
mkdir -p "$out"
log="$out/night.log"
exec > >(tee -a "$log") 2>&1
echo "[night] $(date -Is) episode $ep version v$version -> $out"

rc=0
echo "[night] visuals catalog"
python3 -I "$HERE/catalog.py" --copy "$SCRIPTS" | tail -1

build() {  # build <long|short>
  local kind="$1" md="$epdir/${1}_v$version.md" dir="$out/$1"
  [ -f "$md" ] || { echo "[night] no $md, skipped"; return 0; }
  mkdir -p "$dir"
  cp "$md" "$dir/script.md"
  echo "[night] $kind: narration"
  node "$HERE/narrate.js" --script "$md" --out "$dir" --cache "$RENDERS/devlog/$ep/audio_cache" || return 1
  echo "[night] $kind: assembly"
  python3 -I "$HERE/assemble.py" --timing "$dir/timing.json" --renders "$RENDERS" --label "v$version" || return 1
}

# The videos first, with the clips in place: a packet exists even when the editor stays open all night.
build long || rc=1
[ "$skip_short" = 1 ] || build short || rc=1

if [ "$record" = 1 ]; then
  echo "[night] bot video run (waits for the editor to close, up to 8 h)"
  if ( cd "$TOP" && GB_EDITOR_WAIT_MAX="${GB_EDITOR_WAIT_MAX:-28800}" tools/unity.sh record TrainingRoom ); then
    echo "[night] fresh clips: assembling again (the narration is cached, no tokens)"
    build long || rc=1
    [ "$skip_short" = 1 ] || build short || rc=1
  else
    echo "[night] record failed or the editor stayed open: the videos use the clips that were in place"; rc=1
  fi
fi

{
  echo "# Episode $ep, v$version: the morning review"
  echo
  echo "Built $(date -Is). Log: night.log"
  echo
  for k in long short; do
    [ -f "$out/$k/final.mp4" ] && echo "- $k: \`$out/$k/final.mp4\` ($(ffprobe -v error -show_entries format=duration -of csv=p=0 "$out/$k/final.mp4" 2>/dev/null | cut -d. -f1) s), sheet \`$out/$k/contact_sheet.png\`, review \`$out/$k/REVIEW.md\`, notes \`$out/$k/NOTES.md\`"
  done
  echo
  echo "Next: write notes in NOTES.md, then run \`tools/devlog/night.sh $ep\` again after the writer makes v$((version + 1))."
} > "$out/README.md"
echo "[night] done, rc=$rc. Packet: $out/README.md"
exit $rc
