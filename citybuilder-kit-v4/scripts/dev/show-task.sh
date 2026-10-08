#!/usr/bin/env bash
# Prints the spec of a task from docs/plan/*.md (e.g. M0-15) or a change request (CR-0007, CR-0007-T1).
# Usage: bash scripts/dev/show-task.sh <ID>
set -uo pipefail
id="${1:-}"
if [ -z "$id" ]; then echo "Usage: show-task.sh <ID>   e.g. M0-15 or CR-0007" >&2; exit 2; fi
root="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
cd "$root"

case "$id" in
  CR-*)
    cr="${id%%-T*}"                                   # CR-0007-T1 -> CR-0007
    file="$(ls docs/changes/"$cr"-*.md 2>/dev/null | head -1)"
    if [ -z "$file" ]; then echo "No change request file for $cr in docs/changes/" >&2; exit 1; fi
    echo "# Source: $file"
    cat "$file"
    ;;
  *)
    out="$(awk -v id="$id" '
      index($0, "#### " id " ") == 1 { p = 1; print; next }
      p && (/^#### / || /^### / || /^## / || /^---/) { exit }
      p { print }' docs/plan/*.md 2>/dev/null)"
    if [ -z "$out" ]; then echo "Task $id not found in docs/plan/*.md" >&2; exit 1; fi
    board="$(grep -hE "^\| $id \|" docs/plan/*.md 2>/dev/null | head -1)"
    [ -n "$board" ] && echo "Board: $board" && echo
    printf '%s\n' "$out"
    ;;
esac
