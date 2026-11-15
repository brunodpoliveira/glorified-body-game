#!/usr/bin/env bash
# The set piece music of the Dining Hall, G18 (Bruno, 2026-11-15: "Still B", "Yes, cut it"; round 104: the set piece has
# its own music). T44 take B (Lyria, D minor), 165.00 BPM, cut by event, one gain for all files. RoomScore of G18 plays
# them through MusicDirector.SetPiece. WAV copies, no re-encode (Editor/MusicImport.cs sets the import).
# The record: ~/MEGA/glorified/production/tests/T44_music_structure/README.md, the last section.
# Usage: tools/setpiece_import.sh
set -euo pipefail
SRC="${HOME}/MEGA/glorified/renders/T44_music_structure/game_setpiece"
DST="$(git rev-parse --show-toplevel)/Assets/_Game/Resources/Audio/Music/_setpiece/G18"
mkdir -p "${DST}"
for pair in \
  "setpiece_entry:entry" \
  "setpiece_fight_loop:fight_loop" \
  "setpiece_armour_entry:armour_entry" \
  "setpiece_armour_loop:armour_loop" \
  "setpiece_end:end"; do
  src="${pair%%:*}"; dst="${pair##*:}"
  cp -f "${SRC}/${src}.wav" "${DST}/${dst}.wav"
  echo "${dst}.wav <- ${src}.wav"
done
