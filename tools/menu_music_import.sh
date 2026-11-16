#!/usr/bin/env bash
# The main menu music (Bruno, 2026-11-16: "carol d is the best", "Yes, send it"). T45 take Carol D: 51 bars at 85.00 BPM,
# 144.00 s, one loop (the 0.5 s after the cut is cross-faded into its head). MusicDirector plays it in the Menu state
# (Resources/Audio/Music/_menu/menu). WAV copy, no re-encode (Editor/MusicImport.cs sets the import).
# The record: ~/MEGA/glorified/production/tests/T45_menu_music/README.md. Usage: tools/menu_music_import.sh
set -euo pipefail
SRC="${HOME}/MEGA/glorified/renders/T45_menu_music/menu_carol_d_loop.wav"
DST="$(git rev-parse --show-toplevel)/Assets/_Game/Resources/Audio/Music/_menu"
mkdir -p "${DST}"
cp -f "${SRC}" "${DST}/menu.wav"
echo "menu.wav <- $(basename "${SRC}")"
