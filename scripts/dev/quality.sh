#!/usr/bin/env bash
# Runs everything CI runs, locally, in CI order, and prints a summary table (core-instructions/18, 11 and 16).
# Steps: docs (check-docs.sh --base origin/main) · commits (subject format, as the CI job `docs`) · handbook (docs-site.sh build) ·
#        format (dotnet format --verify-no-changes) · build (Release) · tests (fast suite) · integration (needs Docker) ·
#        doctools (DocTools check) · unity (needs UNITY_EDITOR).
# A step that cannot run here is SKIPPED with a message (no Docker, no Node.js, UNITY_EDITOR unset). A skip is not a pass:
# the summary names it, so say in your report what was not run. Output is never hidden.
# Usage: bash scripts/dev/quality.sh [--only a,b] [--skip a,b] [--base <ref>] [--list]
#   --only / --skip  step names, comma separated (for a quick run while working; the full run is the one that counts)
#   --base           the branch the commits are compared with (default origin/main)
# Exit code: 0 every step that ran passed · 1 a step failed · 2 the setup or an option is wrong.
set -uo pipefail

all_steps="docs commits handbook format build tests integration doctools unity"
only=""
skip=""
base="origin/main"

# Prints the usage line to stderr and ends the script with exit code 2.
usage_error() {
  echo "quality: $1" >&2
  echo "Usage: bash scripts/dev/quality.sh [--only a,b] [--skip a,b] [--base <ref>] [--list]   (steps: $all_steps)" >&2
  exit 2
}

# Fails (exit 2) when a comma separated list names a step that does not exist.
check_names() {
  local name
  for name in $(printf '%s' "$1" | tr ',' ' '); do
    case " $all_steps " in *" $name "*) ;; *) usage_error "unknown step '$name'" ;; esac
  done
}

while [ $# -gt 0 ]; do
  case "$1" in
    --only) [ $# -ge 2 ] || usage_error "--only needs a value"; only="$2"; shift 2 ;;
    --skip) [ $# -ge 2 ] || usage_error "--skip needs a value"; skip="$2"; shift 2 ;;
    --base) [ $# -ge 2 ] || usage_error "--base needs a value"; base="$2"; shift 2 ;;
    --list) echo "$all_steps" | tr ' ' '\n'; exit 0 ;;
    *) usage_error "unknown option '$1'" ;;
  esac
done
check_names "$only"
check_names "$skip"

root="$(git rev-parse --show-toplevel 2>/dev/null)" || { echo "quality: run this inside the repository." >&2; exit 2; }
cd "$root" || exit 2

names=()     # the steps that were considered, in order
results=()   # PASS | FAIL | SKIP, one per name
seconds=()   # the time of each step
notes=()     # why a step was skipped or failed

# True when the step is selected by --only / --skip.
selected() {
  if [ -n "$only" ]; then
    case ",$only," in *",$1,"*) ;; *) return 1 ;; esac
  fi
  case ",$skip," in *",$1,"*) return 1 ;; esac
  return 0
}

# Records the outcome of a step: name, PASS|FAIL|SKIP, seconds, note.
record() {
  names+=("$1"); results+=("$2"); seconds+=("$3"); notes+=("$4")
}

# Runs one step: run_step <name> <title> <command...>. The command's output goes straight to the terminal.
run_step() {
  local name="$1" title="$2" started code
  shift 2
  selected "$name" || return 0
  echo
  echo "== [$name] $title"
  started=$SECONDS
  "$@"
  code=$?
  if [ "$code" -eq 0 ]; then
    record "$name" PASS $((SECONDS - started)) ""
  else
    record "$name" FAIL $((SECONDS - started)) "exit code $code"
  fi
}

# Records a skipped step with the reason and prints it: skip_step <name> <reason>.
skip_step() {
  selected "$1" || return 0
  echo
  echo "== [$1] SKIPPED: $2"
  record "$1" SKIP 0 "$2"
}

# The commit subject format of the CI job `docs` (core-instructions/18) for every commit of this branch since $base.
check_commit_subjects() {
  local pattern='^(feat|fix|test|docs|refactor|perf|build|ci|chore|wip)\((M[0-9]+-[0-9]{2}[a-z]?|CR-[0-9]{4}(-T[0-9]+)?|repo|kit)\)!?: .{3,}$'
  local bad=0 subject
  if ! git rev-parse --verify -q "$base" >/dev/null; then
    echo "quality: the base '$base' does not exist here (git fetch origin)." >&2
    return 1
  fi
  while IFS= read -r subject; do
    case "$subject" in "Merge "*|"Revert "*) continue ;; esac
    printf '%s' "$subject" | grep -qE "$pattern" || { echo "Bad commit subject: $subject"; bad=1; }
  done < <(git log --format=%s "$base..HEAD")
  [ "$bad" -eq 0 ] && echo "commit subjects: OK"
  return "$bad"
}

run_step docs "documentation and process checks" bash scripts/dev/check-docs.sh --base "$base"
run_step commits "commit subject format against $base" check_commit_subjects

if command -v node >/dev/null 2>&1; then
  run_step handbook "the handbook site builds" bash scripts/dev/docs-site.sh build
else
  skip_step handbook "Node.js is not installed (the handbook build needs Node.js 20+)"
fi

if [ -f server/CityBuilder.slnx ] && ! command -v dotnet >/dev/null 2>&1; then
  echo "quality: dotnet is not installed; the .NET steps cannot run." >&2
  exit 2
fi
run_step format "dotnet format --verify-no-changes" bash -c 'dotnet restore server/CityBuilder.slnx --locked-mode && dotnet format server/CityBuilder.slnx --verify-no-changes --no-restore'
run_step build "dotnet build (Release)" dotnet build server/CityBuilder.slnx --no-restore -c Release
run_step tests "fast tests" dotnet test server/CityBuilder.slnx --no-build -c Release --filter "Category!=Integration"

if docker info >/dev/null 2>&1; then
  run_step integration "integration tests (Docker)" dotnet test server/CityBuilder.slnx --no-build -c Release --filter "Category=Integration"
else
  skip_step integration "Docker is not running (start Docker Desktop; docs/testing/README.md)"
fi

run_step doctools "generated docs are current (DocTools check)" dotnet run --project tools/DocTools -c Release -- check

if [ -n "${UNITY_EDITOR:-}" ]; then
  run_step unity "Unity tests (close the Unity Editor first)" bash scripts/dev/test-unity.sh
else
  skip_step unity "UNITY_EDITOR is not set (client/README.md → Run the tests)"
fi

echo
echo "== Quality summary"
printf '%-12s %-6s %6s  %s\n' "step" "result" "sec" "note"
failed=0
skipped=0
for i in "${!names[@]}"; do
  printf '%-12s %-6s %6s  %s\n' "${names[$i]}" "${results[$i]}" "${seconds[$i]}" "${notes[$i]}"
  [ "${results[$i]}" = FAIL ] && failed=$((failed + 1))
  [ "${results[$i]}" = SKIP ] && skipped=$((skipped + 1))
done
if [ "${#names[@]}" -eq 0 ]; then
  echo "quality: no step was selected." >&2
  exit 2
fi
if [ "$failed" -ne 0 ]; then
  echo "FAILED: $failed step(s) did not pass." >&2
  exit 1
fi
if [ "$skipped" -ne 0 ]; then
  echo "OK, but $skipped step(s) were skipped: that is not what CI runs. Say so in your report."
else
  echo "OK: every step passed."
fi
