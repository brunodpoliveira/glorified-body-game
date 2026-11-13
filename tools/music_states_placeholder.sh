#!/usr/bin/env bash
# The placeholder music of the three states (rounds 119 and 123, CANON: one composition, three states cut from the same
# audio). The loops of the T41 study (tests/T41_music_layers/RESULT.md in the writer's repo, "complex"): the same length
# in samples, so MusicDirector keeps the beat when the state changes. They are studies, not the final track.
# exploration = Ambient, search = Tension, combat = Combat. Usage: tools/music_states_placeholder.sh
set -euo pipefail
SRC="${HOME}/MEGA/glorified/renders/T41_music_layers/complex"
DST="$(git rev-parse --show-toplevel)/Assets/_Game/Resources/Audio/Music/_placeholder"
for pair in "state_explore_loop:ambient" "state_search_loop:tension" "state_combat_loop:combat"; do
  src="${pair%%:*}"; dst="${pair##*:}"
  ffmpeg -nostdin -loglevel error -y -i "${SRC}/${src}.wav" -c:a libvorbis -q:a 5 "${DST}/${dst}.ogg"
  echo "${dst}.ogg <- ${src}.wav"
done
