#!/usr/bin/env bash
# The demo music (Bruno, 2026-11-14: the T43 v4 kit, "v4, drum break"). One composition at 84 BPM in D minor
# (round 126): the three states are 12-bar loops of the same audio, and combat has an arc (theme, a 4-bar drum
# break, the payoff, the theme again) on the bar lines. WAV copies, no re-encode: Unity compresses them on import
# (Editor/MusicImport.cs). The record: ~/MEGA/glorified/production/tests/T43_combat_kit/README.md.
# exploration = ambient, search = tension, combat = combat (the theme loop). Usage: tools/music_kit_import.sh
set -euo pipefail
R="${HOME}/MEGA/glorified/renders"
DST="$(git rev-parse --show-toplevel)/Assets/_Game/Resources/Audio/Music/_demo"
mkdir -p "${DST}"
for pair in \
  "T41_music_layers/complex/state_explore_loop.wav:ambient" \
  "T41_music_layers/complex/state_search_loop.wav:tension" \
  "T43_combat_kit/game/combat_theme_loop.wav:combat" \
  "T43_combat_kit/game/combat_drum_break.wav:combat_break" \
  "T43_combat_kit/game/combat_payoff.wav:combat_payoff"; do
  src="${pair%%:*}"; dst="${pair##*:}"
  cp -f "${R}/${src}" "${DST}/${dst}.wav"
  echo "${dst}.wav <- ${src}"
done
