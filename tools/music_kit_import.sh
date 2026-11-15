#!/usr/bin/env bash
# The demo music. Since 2026-11-15 the T44 kit (Bruno: "yes, hunted a works", "yes, cut it"): one composition at
# 83.00 BPM (round 126: the three states are 12-bar loops of the same audio). Combat has an arc on the bar lines: the
# theme, the 4-bar break (the song's own drop-out), the 24-bar payoff, then the theme again. One gain for all files.
# WAV copies, no re-encode: Unity compresses them on import (Editor/MusicImport.cs).
# The record: ~/MEGA/glorified/production/tests/T44_music_structure/README.md.
# The T43 v4 kit (84 BPM, commit b7e4ef7) stays in the git history.
# exploration = ambient, search = tension, combat = combat (the theme loop). Usage: tools/music_kit_import.sh
set -euo pipefail
SRC="${HOME}/MEGA/glorified/renders/T44_music_structure/game"
DST="$(git rev-parse --show-toplevel)/Assets/_Game/Resources/Audio/Music/_demo"
mkdir -p "${DST}"
for pair in \
  "combat_explore_loop:ambient" \
  "combat_search_loop:tension" \
  "combat_theme_loop:combat" \
  "combat_break:combat_break" \
  "combat_payoff:combat_payoff"; do
  src="${pair%%:*}"; dst="${pair##*:}"
  cp -f "${SRC}/${src}.wav" "${DST}/${dst}.wav"
  echo "${dst}.wav <- ${src}.wav"
done
