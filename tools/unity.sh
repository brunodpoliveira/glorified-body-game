#!/usr/bin/env bash
# Single entry point for every Unity call in THE GLORIFIED BODY (adapted from Warspite's tools/unity.sh).
# Agents must never start Unity in another way.
#
# Usage: tools/unity.sh <mode> [args]
#   compile                     Batch: open project, compile, quit.          (slot: batch)
#   exec <Class.Method> [args]  Batch: run an editor method (-executeMethod). (slot: batch)
#   cmdb <name> [-- cmd args]   Batch: run a [CliCommand] (unity run --command). (slot: batch)
#   test <editmode|playmode> [filter]  Batch: run tests.                     (slot: batch)
#   wcompile                    Warm editor: unity recompile.                 (slot: warm)
#   wcmd <name> [args]          Warm editor: unity command <name> [args].     (slot: warm)
#   sanity <Scene>              Batch: WorldSanity checks -> sanity-<Scene>.json. (slot: batch)
#   shots <Scene>               Batch with graphics: screenshots -> shots/*.png. (slot: batch)
#   play <Level> [StartScene] [bot] [timeoutSec]
#                               Batch Play mode with a bot (default: Main_Menu, structural, 900)
#                               -> playthrough.json, runtime.jsonl, shots/.     (slot: batch)
#   record <Level> [StartScene] [bot] [timeoutSec]
#                               The play run with video: the bot saves 1080p30 frames of its steps,
#                               ffmpeg makes clips/<step>.mp4, copies go to
#                               ~/glorified-body-renders/devlog/clips/{latest,<stamp>}/. (slot: batch)
#   import <file.unitypackage>  Batch: import an Asset Store package (unity assets import). (slot: batch)
#   triage <run-dir>            Re-run log triage on a run folder.
#
# Project = the git toplevel of cwd (the Unity project sits at the repo root). Override with GB_PROJECT.
# Each call makes runs/<id>/ in the git toplevel, writes logs there, and prints the triage summary.
# Exit code = Unity/CLI exit code, or 1 when triage finds errors.
set -uo pipefail

export UNITY_NO_BANNER=1 UNITY_NO_PAGER=1 UNITY_NON_INTERACTIVE=1 UNITY_NO_UPDATE_CHECK=1 UNITY_NO_CONSENT_PROMPT=1
MAIN_PROJECT="/home/pc/MEGA/code/glorified-body-game"
UNITY_BIN="${GB_UNITY_BIN:-/home/pc/Unity/Hub/Editor/6000.3.15f1/Editor/Unity}"
export DISPLAY="${DISPLAY:-:0}"   # batch runs with graphics (shots, play) need an X display
LOCK_DIR="$HOME/.warspite-locks"   # shared with Warspite on purpose: one batch Unity on this machine
BATCH_TIMEOUT="${GB_BATCH_TIMEOUT:-1800}"
# Batch Unity is the first target of the OOM killer, so a RAM shortage never kills the user's editor.
echo 1000 > /proc/self/oom_score_adj 2>/dev/null || true
MIN_AVAIL_MB="${GB_MIN_AVAIL_MB:-4500}"   # batch waits until this much RAM is available
RAM_WAIT_MAX="${GB_RAM_WAIT_MAX:-3600}"
mkdir -p "$LOCK_DIR"

TOP="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
PROJ="${GB_PROJECT:-$TOP}"
TOOLS="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
mode="${1:-}"; shift || true

new_run() {
  RUN="$TOP/runs/$(date +%Y%m%d-%H%M%S)-$1-$$"
  mkdir -p "$RUN"
  echo "[unity.sh] run dir: $RUN"
  { echo "mode=$1"; echo "project=$PROJ"; echo "args=$*"; date -Is; } > "$RUN/meta.txt"
}

editor_open_on() {  # true when an editor process holds this project
  pgrep -af "Editor/Unity" | grep -F -- "-projectpath $1" >/dev/null 2>&1 ||
  pgrep -af "Editor/Unity" | grep -F -- "-projectPath $1" >/dev/null 2>&1
}

disk_ok() {  # Unity writes logs and imports; refuse to start below 5 GB free
  local free_gb; free_gb=$(df --output=avail -BG "$HOME" | tail -1 | tr -dc '0-9')
  [ "$free_gb" -ge 5 ] && return 0
  echo "[unity.sh] ERROR: only ${free_gb} GB free on disk. Ask the lead to free space."; return 1
}

wait_ram() {
  disk_ok || return 1
  local waited=0 avail
  while :; do
    avail=$(awk '/MemAvailable/ {print int($2/1024)}' /proc/meminfo)
    [ "$avail" -ge "$MIN_AVAIL_MB" ] && return 0
    [ "$waited" -ge "$RAM_WAIT_MAX" ] && { echo "[unity.sh] RAM gate: only ${avail} MB free after ${waited}s. Giving up."; return 1; }
    echo "[unity.sh] RAM gate: ${avail} MB free < ${MIN_AVAIL_MB} MB. Waiting..."
    sleep 30; waited=$((waited + 30))
  done
}

with_lock() {  # with_lock <slot> <cmd...>
  local slot="$1"; shift
  exec 9>"$LOCK_DIR/$slot.lock"
  echo "[unity.sh] waiting for slot '$slot'..."
  flock 9
  echo "[unity.sh] got slot '$slot' ($(date +%T))"
  "$@"; local rc=$?
  flock -u 9
  return $rc
}

triage() {
  python3 "$TOOLS/log-triage.py" "$RUN"; local t=$?
  return $t
}

batch_guard() {
  # GB_EDITOR_WAIT_MAX > 0: wait that many seconds for an open editor to close instead of refusing at once.
  local waited=0
  while editor_open_on "$PROJ"; do
    if [ "${GB_EDITOR_WAIT_MAX:-0}" -le 0 ] || [ "$waited" -ge "${GB_EDITOR_WAIT_MAX:-0}" ]; then
      echo "[unity.sh] ERROR: an editor has '$PROJ' open. Batch mode cannot open the same project."
      echo "[unity.sh] Close the editor first, or use the w* modes for the warm editor."
      exit 3
    fi
    [ "$waited" -eq 0 ] && echo "[unity.sh] editor gate: an editor has the project open. Waiting up to ${GB_EDITOR_WAIT_MAX}s ($(date +%T))..."
    sleep 30; waited=$((waited + 30))
  done
  [ "$waited" -gt 0 ] && echo "[unity.sh] editor gate: editor closed after ${waited}s ($(date +%T))."
  rm -f "$PROJ/Library/EditorInstance.json"   # stale file copied with Library/ from another tree
  playmode_flag_guard
}

# Settings guard. The Gore System URP import (2026-10-20) reset ProjectSettings to the template state:
# the Invector layers, the legacy input axes, the build scene list, the input handler and the PlayMode test
# flag. The PlayMode tests live in Assembly-CSharp (no asmdefs: the vendor code has none) and compile only while
# "playModeTestRunnerEnabled: 1" holds. This guard puts every known value back before a batch run and says so.
# Baseline copies: tools/settings-baseline/ (refresh them with: cp ProjectSettings/<file> tools/settings-baseline/).
playmode_flag_guard() {
  local f="$PROJ/ProjectSettings/ProjectSettings.asset" b="$TOOLS/settings-baseline"
  [ -f "$f" ] || return 0
  if grep -q "^  playModeTestRunnerEnabled: 0" "$f"; then
    sed -i 's/^  playModeTestRunnerEnabled: 0/  playModeTestRunnerEnabled: 1/' "$f"
    echo "[unity.sh] settings guard: playModeTestRunnerEnabled was 0, set back to 1."
  fi
  if grep -q "^  activeInputHandler: [01]$" "$f"; then
    sed -i 's/^  activeInputHandler: [01]$/  activeInputHandler: 2/' "$f"
    echo "[unity.sh] settings guard: activeInputHandler was not Both (2), set back."
  fi
  if ! grep -q "INVECTOR_SHOOTER" "$f"; then
    echo "[unity.sh] WARNING: the Invector scripting defines are missing from ProjectSettings.asset (see CLAUDE.md rule 4)."
  fi
  restore_if_missing "TagManager.asset" "CoverPoint" "the Invector layers"
  restore_if_missing "InputManager.asset" "LeftAnalogHorizontal" "the legacy input axes"
  restore_if_missing "EditorBuildSettings.asset" "TrainingRoom.unity" "the build scene list"
  # URP: both files must point at Assets/Settings/PC_RPAsset.asset (guid 4b83569d...). Without it the
  # pipeline falls back to Built-in and every URP material renders pink.
  restore_if_missing "QualitySettings.asset" "4b83569d67af61e458304325a23e5dfd" "the URP pipeline asset (PC_RPAsset)"
  restore_if_missing "GraphicsSettings.asset" "4b83569d67af61e458304325a23e5dfd" "the URP pipeline asset (PC_RPAsset)"
}

restore_if_missing() {  # restore_if_missing <file> <marker> <what>
  local f="$PROJ/ProjectSettings/$1" b="$TOOLS/settings-baseline/$1"
  [ -f "$b" ] || return 0
  if ! grep -q "$2" "$f" 2>/dev/null; then
    cp "$b" "$f"
    echo "[unity.sh] settings guard: $1 lost $3, restored from tools/settings-baseline/."
  fi
}

run_batch() {  # run_batch <extra unity run args...>  (after -- go editor args)
  batch_guard
  wait_ram || return 4
  unity run "$PROJ" --timeout "$BATCH_TIMEOUT" --log-file "$RUN/editor.log" --no-tail "$@" > "$RUN/cli.out" 2>&1
  local rc=$?
  echo "[unity.sh] unity exit code: $rc"
  [ $rc -ne 0 ] && tail -5 "$RUN/cli.out"
  return $rc
}

case "$mode" in
  compile)
    new_run compile
    with_lock batch run_batch -- -nographics; rc=$?
    triage; t=$?; [ $rc -eq 0 ] && rc=$t; exit $rc ;;
  exec)
    method="${1:?Class.Method required}"; shift
    new_run exec "$method" "$@"
    with_lock batch run_batch -- -executeMethod "$method" -gbRunDir "$RUN" "$@"; rc=$?
    triage; t=$?; [ $rc -eq 0 ] && rc=$t; exit $rc ;;
  cmdb)
    name="${1:?command name required}"; shift
    new_run cmdb "$name" "$@"
    cmdb_batch() {
      batch_guard; wait_ram || return 4
      unity run "$PROJ" --command "$name" --format json --timeout "$BATCH_TIMEOUT" \
        --log-file "$RUN/editor.log" --no-tail "$@" > "$RUN/result.json" 2> "$RUN/cli.err"
    }
    with_lock batch cmdb_batch "$@"; rc=$?
    cat "$RUN/result.json"
    triage; t=$?; [ $rc -eq 0 ] && rc=$t; exit $rc ;;
  test)
    plat="${1:?editmode|playmode}"; filter="${2:-}"
    new_run "test-$plat" "$filter"
    targs=(--mode "$plat" --output "$RUN/test-results.xml" --timeout "$BATCH_TIMEOUT")
    [ -n "$filter" ] && targs+=(--filter "$filter")
    # The guard runs inside the lock: a queued test waits for the running batch instead of refusing (exit 3).
    test_batch() {
      batch_guard; wait_ram || return 4
      unity test "$PROJ" "${targs[@]}" -- -logFile "$RUN/editor.log" > "$RUN/cli.out" 2>&1
    }
    with_lock batch test_batch; rc=$?
    echo "[unity.sh] unity test exit code: $rc  [legend: 0 = all tests passed, 8 = some tests failed, 6 = run error]"
    triage; t=$?; [ $rc -eq 0 ] && rc=$t; exit $rc ;;
  wcompile|wcmd)
    WPROJ="${GB_WARM_PROJECT:-$MAIN_PROJECT}"
    new_run "$mode" "$@"
    LOG="$HOME/.config/unity3d/Editor.log"
    off=$(stat -c %s "$LOG" 2>/dev/null || echo 0)
    if [ "$mode" = wcompile ]; then
      with_lock warm unity recompile --project-path "$WPROJ" --format json --timeout 300 > "$RUN/result.json" 2> "$RUN/cli.err"; rc=$?
    else
      name="${1:?command name required}"; shift
      with_lock warm unity command "$name" --project-path "$WPROJ" --format json --timeout "${GB_WARM_TIMEOUT:-300}" "$@" > "$RUN/result.json" 2> "$RUN/cli.err"; rc=$?
    fi
    tail -c +"$((off + 1))" "$LOG" > "$RUN/editor-slice.log" 2>/dev/null
    head -c 4000 "$RUN/result.json"; echo
    [ $rc -eq 7 ] && echo "[unity.sh] exit 7: warm editor not reachable (closed, Safe Mode, or Pipeline not loaded)."
    triage; t=$?; [ $rc -eq 0 ] && rc=$t; exit $rc ;;
  sanity)
    scene="${1:?Scene required}"
    new_run sanity "$scene"
    with_lock batch run_batch -- -nographics -executeMethod GlorifiedBody.Editor.Automation.WorldSanity.Run \
      -gbScene "$scene" -gbRunDir "$RUN"; rc=$?
    cat "$RUN"/sanity-*.json 2>/dev/null | grep -E '"pass"|"fail"|"warn"' | head -60
    triage; t=$?; [ $rc -eq 0 ] && rc=$t; exit $rc ;;
  shots)
    scene="${1:?Scene required}"
    new_run shots "$scene"
    with_lock batch run_batch -- -executeMethod GlorifiedBody.Editor.Automation.ShotCapture.Run \
      -gbScene "$scene" -gbRunDir "$RUN"; rc=$?
    ls "$RUN/shots" 2>/dev/null
    triage; t=$?; [ $rc -eq 0 ] && rc=$t; exit $rc ;;
  play|record)
    level="${1:?Level scene required}"; start="${2:-Main_Menu}"; bot="${3:-structural}"; tmo="${4:-900}"
    record=0; [ "$mode" = record ] && record=1
    new_run "$mode" "$level" "$start" "$bot"
    play_batch() {
      batch_guard; wait_ram || return 4
      # Direct editor call: unity run always adds -quit, which would stop Play mode at once.
      timeout --kill-after=30 "$((tmo + 300))" "$UNITY_BIN" -batchmode -projectPath "$PROJ" -logFile "$RUN/editor.log" \
        -executeMethod GlorifiedBody.Editor.Automation.PlaythroughRunner.Run \
        -gbRunDir "$RUN" -gbScene "$start" -gbLevel "$level" -gbBot "$bot" -gbTimeout "$tmo" -gbRecord "$record" \
        > "$RUN/cli.out" 2>&1
      local rc=$?
      echo "[unity.sh] editor exit code: $rc  [legend: 0 = bot pass, 1 = bot fail, 2 = bot timeout, 124 = hard timeout]"
      return $rc
    }
    encode_clips() {  # frames/<clip>/00001.png -> clips/<clip>.mp4 (1080p30), then the frames go
      local out="$RUN/clips" dest="${GB_CLIPS_DIR:-$HOME/glorified-body-renders/devlog/clips}" stamp
      stamp=$(date +%Y%m%d-%H%M)
      mkdir -p "$out"
      for d in "$RUN"/frames/*/; do
        [ -d "$d" ] || continue
        local c n; c=$(basename "$d"); n=$(find "$d" -maxdepth 1 -name '*.png' | wc -l)
        [ "$n" -ge 2 ] || { echo "[record] $c: $n frames, skipped"; continue; }
        if ffmpeg -loglevel error -y -framerate 30 -i "$d/%05d.png" -c:v libx264 -crf 18 -preset medium \
             -pix_fmt yuv420p -movflags +faststart "$out/$c.mp4"; then
          echo "[record] $c: $n frames -> clips/$c.mp4 ($(awk "BEGIN{printf \"%.1f\", $n/30}") s)"
          rm -rf "$d"
        else
          echo "[record] $c: ffmpeg failed, frames kept in $d"
        fi
      done
      ls "$out"/*.mp4 >/dev/null 2>&1 || { echo "[record] no clips made"; return 1; }
      mkdir -p "$dest/$stamp" "$dest/latest"
      cp "$out"/*.mp4 "$dest/$stamp/"; cp "$out"/*.mp4 "$dest/latest/"
      cp "$RUN/frames/clips.json" "$dest/$stamp/" 2>/dev/null
      echo "[record] clips in $dest/latest/ and $dest/$stamp/"
    }
    with_lock batch play_batch; rc=$?
    [ -f "$RUN/playthrough.json" ] && python3 -c "import json,sys; d=json.load(open(sys.argv[1])); print('[play]', 'PASS' if d['pass'] else 'FAIL', d.get('duration_s', '?'), 's, max frame', d.get('max_frame_ms', '?'), 'ms'); [print('  ', 'ok  ' if s['ok'] else 'FAIL', s['name'], s['detail']) for s in d['steps']]" "$RUN/playthrough.json"
    [ "$record" = 1 ] && encode_clips
    triage; t=$?; [ $rc -eq 0 ] && rc=$t; exit $rc ;;
  import)
    pkg="${1:?.unitypackage path required}"
    [ -f "$pkg" ] || { echo "[unity.sh] ERROR: no such file: $pkg"; exit 2; }
    new_run import "$(basename "$pkg")"
    LOG="$HOME/.config/unity3d/Editor.log"
    off=$(stat -c %s "$LOG" 2>/dev/null || echo 0)
    import_batch() {
      batch_guard; wait_ram || return 4
      # unity assets import has no --timeout or --log-file. Guard it with timeout and slice the default editor log.
      timeout --kill-after=60 "${GB_IMPORT_TIMEOUT:-3600}" unity assets import "$pkg" --project "$PROJ" > "$RUN/cli.out" 2>&1
      local rc=$?
      echo "[unity.sh] unity assets import exit code: $rc  [legend: 124 = hard timeout]"
      [ $rc -ne 0 ] && tail -5 "$RUN/cli.out"
      return $rc
    }
    with_lock batch import_batch; rc=$?
    tail -c +"$((off + 1))" "$LOG" > "$RUN/editor.log" 2>/dev/null
    triage; t=$?; [ $rc -eq 0 ] && rc=$t; exit $rc ;;
  triage)
    RUN="${1:?run dir}"; triage; exit $? ;;
  *)
    sed -n '2,21p' "$0"; exit 2 ;;
esac
