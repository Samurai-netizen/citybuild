#!/usr/bin/env bash
# Makes a development player of client/CityBuilder headless (M0-82, docs/BUILD.md): Bootstrap scene, Development flag, output under
# client/CityBuilder/Builds/<platform>/ (git ignores it). The work is done by the Editor script CityBuilder.Client.Editor.DevBuild.
# Usage: UNITY_EDITOR=<path to the Unity executable> bash scripts/dev/build-client.sh <macos|windows|android|ios>
#   The Editor must be closed for this project (a Unity project can be open only once). The platform's build support module must be
#   installed in Unity Hub (Windows Build Support for windows; Android Build Support with SDK, NDK and OpenJDK for android; iOS for ios).
# Unity's log goes to client/CityBuilder/Logs/build-<platform>.log (git ignores it).
# Exit code: 0 built · 1 the build failed · 2 the setup is wrong (UNITY_EDITOR, platform, project).
set -uo pipefail

root="$(git rev-parse --show-toplevel 2>/dev/null)" || { echo "build-client: run this inside the repository." >&2; exit 2; }
project="$root/client/CityBuilder"

case "${1:-}" in
  macos)   target="OSXUniversal" ;;
  windows) target="Win64" ;;
  android) target="Android" ;;
  ios)     target="iOS" ;;
  *) echo "Usage: UNITY_EDITOR=<Unity program> bash scripts/dev/build-client.sh <macos|windows|android|ios>" >&2; exit 2 ;;
esac
platform="$1"

if [ -z "${UNITY_EDITOR:-}" ]; then
  echo "build-client: the environment variable UNITY_EDITOR is not set (the Unity program; see scripts/dev/test-unity.sh)." >&2
  exit 2
fi
unity="$UNITY_EDITOR"
if [ -d "$unity" ] && [ -x "$unity/Contents/MacOS/Unity" ]; then
  unity="$unity/Contents/MacOS/Unity"
fi
if [ ! -f "$unity" ] || [ ! -x "$unity" ]; then
  echo "build-client: UNITY_EDITOR is not an executable file: $UNITY_EDITOR" >&2
  exit 2
fi

mkdir -p "$project/Logs"
log="$project/Logs/build-$platform.log"
echo "build-client: building $platform (this takes minutes; log: $log)"
"$unity" -batchmode -nographics -quit -projectPath "$project" -buildTarget "$target" \
  -executeMethod CityBuilder.Client.Editor.DevBuild.BuildFromCommandLine -logFile "$log"
code=$?

grep -E "^DevBuild:|error CS[0-9]+" "$log" | head -20
if [ "$code" -ne 0 ]; then
  echo "build-client: the $platform build failed (exit $code); read $log." >&2
  exit 1
fi
# A build rewrites the URP assets' shader prefiltering and some Player settings; they are not part of a change (docs/BUILD.md).
echo "build-client: done. A build rewrites files under client/CityBuilder/Assets/Settings and ProjectSettings: restore those you did not change on purpose (git status, then git checkout -- <file>)."
