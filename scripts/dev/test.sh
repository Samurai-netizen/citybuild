#!/usr/bin/env bash
# Focused, quiet test runs for the inner loop (core-instructions/08-testing-strategy.md → Test tiers).
# Builds and runs one area's tests and prints one line per area; on a failure it prints only the failing tests and their messages.
# The full output of the last run of each area is kept in .test-logs/<area>.log (git ignores the folder).
# Usage: bash scripts/dev/test.sh <area>[,<area>…] [filter]
#   area    sim · domain · app · repo · int (in-process, no Docker) · int-db (needs Docker) · doctools · fast (every area but int-db)
#           changed (the areas of the files changed since origin/main, working tree included)
#   filter  a test class or method name (becomes FullyQualifiedName~<filter>), or a full dotnet filter when it contains = or ~
# Examples: bash scripts/dev/test.sh sim CourierRouteTests        one class while writing it (tier T0)
#           bash scripts/dev/test.sh changed                      the areas a part touched, at its checkpoint (tier T1)
# Exit code: 0 every area passed · 1 a test or the build failed · 2 the usage is wrong.
set -uo pipefail

root="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
cd "$root"
[ -d "$HOME/.dotnet" ] && case ":$PATH:" in *":$HOME/.dotnet:"*) ;; *) PATH="$HOME/.dotnet:$PATH" ;; esac

all_areas="sim domain app repo int doctools"
log_dir=".test-logs"

# Prints the usage to stderr and ends the script with exit code 2.
usage_error() {
  echo "test: $1" >&2
  echo "Usage: bash scripts/dev/test.sh <area>[,<area>…] [filter]   (areas: $all_areas int-db fast changed)" >&2
  exit 2
}

# The test project of an area.
project_of() {
  case "$1" in
    sim) echo server/tests/CityBuilder.Simulation.Tests ;;
    domain) echo server/tests/CityBuilder.Domain.Tests ;;
    app) echo server/tests/CityBuilder.Application.Tests ;;
    repo) echo server/tests/CityBuilder.Repository.Tests ;;
    int|int-db) echo server/tests/CityBuilder.IntegrationTests ;;
    doctools) echo tools/DocTools.Tests ;;
    *) return 1 ;;
  esac
}

# The areas whose tests cover the files changed since origin/main (commits, staged, unstaged and untracked files), one per line.
changed_areas() {
  {
    git diff --name-only origin/main...HEAD 2>/dev/null
    git diff --name-only HEAD 2>/dev/null
    git ls-files --others --exclude-standard 2>/dev/null
  } | sort -u | while IFS= read -r f; do
    case "$f" in
      shared/Simulation/*) echo sim; echo app ;;
      server/content/*) echo sim; echo int; echo doctools ;;
      server/src/CityBuilder.Domain/*) echo domain; echo app ;;
      server/src/CityBuilder.Application/*) echo app ;;
      server/src/CityBuilder.Infrastructure/*|server/src/CityBuilder.Api/*) echo int ;;
      server/tests/CityBuilder.Simulation.Tests/*) echo sim ;;
      server/tests/CityBuilder.Domain.Tests/*) echo domain ;;
      server/tests/CityBuilder.Application.Tests/*) echo app ;;
      server/tests/CityBuilder.Repository.Tests/*) echo repo ;;
      server/tests/CityBuilder.IntegrationTests/*) echo int ;;
      tools/*) echo doctools ;;
      client/*) echo client ;;
    esac
    case "$f" in *.cs) echo repo ;; esac
  done | sort -u
}

# Runs one area's tests with the filter and prints its one-line result, or its failures. Returns 1 when it did not pass.
run_area() {
  local area="$1" filter="$2" project log expr summary started
  project="$(project_of "$area")" || usage_error "unknown area '$area'"
  log="$log_dir/$area.log"
  case "$area" in int) expr="Category!=Integration" ;; int-db) expr="Category=Integration" ;; *) expr="" ;; esac
  if [ -n "$filter" ]; then
    case "$filter" in *=*|*~*) ;; *) filter="FullyQualifiedName~$filter" ;; esac
    if [ -n "$expr" ]; then expr="($expr)&($filter)"; else expr="$filter"; fi
  fi
  started="$(date +%s)"
  if [ -n "$expr" ]; then
    dotnet test "$project" -v q --nologo --logger "console;verbosity=normal" --filter "$expr" > "$log" 2>&1
  else
    dotnet test "$project" -v q --nologo --logger "console;verbosity=normal" > "$log" 2>&1
  fi
  local code=$? seconds=$(( $(date +%s) - started ))
  summary="$(awk '/^Total tests:/ { t = $3 } /^ +Passed:/ { p = $2 } /^ +Failed:/ { f = $2 } /^ +Skipped:/ { s = $2 }
    END { if (t != "") printf "%d passed, %d failed, %d skipped of %d", p, f, s, t }' "$log")"
  if [ "$code" -eq 0 ] && [ -n "$summary" ]; then
    echo "$area: $summary (${seconds} s)"
    return 0
  fi
  if [ "$code" -eq 0 ]; then
    echo "$area: NO TESTS RAN (filter '${expr:-none}' matched nothing; ${seconds} s)"
    return 1
  fi
  echo "$area: FAILED ${summary:+- $summary }(${seconds} s) · full output: $log"
  if grep -qE ': error [A-Z]+[0-9]+' "$log"; then
    grep -E ': error [A-Z]+[0-9]+' "$log" | sed -E 's/ \[[^]]+\]$//' | sort -u | head -20
  else
    awk '/^ +Failed [^ ]/ { print; next } /^ +Error Message:/ { n = 4; next } /^ +Stack Trace:/ { n = 0 } n > 0 { print; n-- }' "$log" | head -60
  fi
  return 1
}

[ $# -ge 1 ] || usage_error "name an area"
[ $# -le 2 ] || usage_error "too many arguments"
areas="$1"
filter="${2:-}"
case "$areas" in
  fast) areas="$all_areas" ;;
  changed)
    areas="$(changed_areas | tr '\n' ' ')"
    case " $areas " in *" client "*)
      echo "client: changed - run scripts/dev/test-unity.sh (needs UNITY_EDITOR); not run here"
      areas="$(printf '%s' "$areas" | tr ' ' '\n' | grep -vx client | tr '\n' ' ')" ;;
    esac
    [ -n "${areas// /}" ] || { echo "changed: no area with tests is touched"; exit 0; } ;;
  *) areas="$(printf '%s' "$areas" | tr ',' ' ')" ;;
esac

mkdir -p "$log_dir"
failed=0
for area in $areas; do
  run_area "$area" "$filter" || failed=1
done
exit "$failed"
