#!/usr/bin/env bash
# Runs the Unity tests of client/CityBuilder headless: EditMode first, then PlayMode (core-instructions/08 and 09).
# Usage: UNITY_EDITOR=<path to the Unity executable> bash scripts/dev/test-unity.sh
#   macOS:   /Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity   (the Unity.app folder also works)
#   Windows: C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe   (Git Bash; test-unity.ps1 is the PowerShell version)
#   Linux:   ~/Unity/Hub/Editor/<version>/Editor/Unity
# The Editor must be closed for this project: a Unity project can be open only once.
# Results (NUnit XML) and Unity's log go to client/CityBuilder/Logs/test-unity/ (git ignores it).
# Exit code: 0 every test passed · 1 a test failed, no test ran, or Unity failed · 2 the setup is wrong (UNITY_EDITOR, project).
set -uo pipefail

root="$(git rev-parse --show-toplevel 2>/dev/null)" || { echo "test-unity: run this inside the repository." >&2; exit 2; }
project="$root/client/CityBuilder"
out="$project/Logs/test-unity"

if [ ! -f "$project/ProjectSettings/ProjectVersion.txt" ]; then
  echo "test-unity: no Unity project at $project" >&2
  exit 2
fi
wanted="$(sed -n 's/^m_EditorVersion: //p' "$project/ProjectSettings/ProjectVersion.txt")"

if [ -z "${UNITY_EDITOR:-}" ]; then
  echo "test-unity: the environment variable UNITY_EDITOR is not set." >&2
  echo "  Set it to the Unity executable of editor $wanted, for example (macOS):" >&2
  echo "  export UNITY_EDITOR=/Applications/Unity/Hub/Editor/$wanted/Unity.app/Contents/MacOS/Unity" >&2
  exit 2
fi
unity="$UNITY_EDITOR"
if [ -d "$unity" ] && [ -x "$unity/Contents/MacOS/Unity" ]; then
  unity="$unity/Contents/MacOS/Unity"
fi
if [ ! -f "$unity" ] || [ ! -x "$unity" ]; then
  echo "test-unity: UNITY_EDITOR is not an executable file: $UNITY_EDITOR" >&2
  echo "  It must point to the Unity program of editor $wanted, not to the Unity Hub." >&2
  exit 2
fi

mkdir -p "$out"

# attr "<tag text>" <name>: the whole number in name="N". A leading space keeps `total` from matching `testcasecount`.
attr() { printf '%s' "$1" | sed -n "s/.* $2=\"\\([0-9][0-9]*\\)\".*/\\1/p"; }

failed_runs=0
summary=""

run_platform() {
  local platform="$1" code tag total passed failed skipped line names
  local results="$out/$platform.xml" log="$out/$platform.log"
  rm -f "$results" "$log"  # a result file from an earlier run must never be mistaken for this one
  echo "== Unity $platform tests (log: $log)"
  "$unity" -batchmode -projectPath "$project" -runTests -testPlatform "$platform" -testResults "$results" -logFile "$log"
  code=$?

  tag=""
  [ -f "$results" ] && tag="$(grep -o -m1 '<test-run [^>]*>' "$results")"
  total="$(attr "$tag" total)"; passed="$(attr "$tag" passed)"; failed="$(attr "$tag" failed)"; skipped="$(attr "$tag" skipped)"

  if [ -z "$total" ] || [ -z "$passed" ] || [ -z "$failed" ] || [ -z "$skipped" ]; then
    line="$platform: FAILED, Unity exit code $code and no readable result file ($results)"
    if grep -qi "another unity instance" "$log" 2>/dev/null; then
      line="$line; close the Unity Editor that has this project open"
    elif grep -q "error CS" "$log" 2>/dev/null; then
      line="$line; the scripts do not compile:"$'\n'"$(grep -m5 'error CS' "$log")"
    fi
    failed_runs=$((failed_runs + 1))
  elif [ "$total" -eq 0 ]; then
    line="$platform: FAILED, no test ran (Unity exit code $code)"
    failed_runs=$((failed_runs + 1))
  elif [ "$failed" -gt 0 ] || [ "$code" -ne 0 ]; then
    line="$platform: FAILED, passed $passed of $total, failed $failed, skipped $skipped (Unity exit code $code)"
    names="$(grep -o '<test-case [^>]*result="Failed"' "$results" | sed -n 's/.* fullname="\([^"]*\)".*/    failed: \1/p')"
    [ -n "$names" ] && line="$line"$'\n'"$names"
    failed_runs=$((failed_runs + 1))
  else
    line="$platform: passed $passed of $total, skipped $skipped"
  fi
  summary="$summary$line"$'\n'
}

started=$SECONDS
run_platform EditMode
run_platform PlayMode

echo
echo "== Unity test summary ($((SECONDS - started)) s)"
printf '%s' "$summary"
if [ "$failed_runs" -ne 0 ]; then
  echo "FAILED: $failed_runs of 2 runs did not pass. Details: $out" >&2
  exit 1
fi
echo "OK: all Unity tests passed."
