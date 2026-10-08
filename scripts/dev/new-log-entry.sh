#!/usr/bin/env bash
# Creates the build log entry for a task (one entry per task) and adds it to the index.
# If an entry for the task already exists, prints its path instead (new sessions append a "Session N" section).
# Usage: bash scripts/dev/new-log-entry.sh <TASK-ID> "<title>" "<tool / model>"
set -euo pipefail
id="${1:?Usage: new-log-entry.sh <TASK-ID> \"<title>\" \"<tool / model>\"}"
title="${2:?title required}"
agent="${3:-unknown tool / model}"
root="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
cd "$root"

existing="$(ls docs/build-log/[0-9][0-9][0-9][0-9]/*-"$id".md 2>/dev/null | head -1 || true)"
if [ -n "$existing" ]; then
  echo "$existing"
  echo "(entry exists - append a new '### Session N' section to it)" >&2
  exit 0
fi

date="$(date -u +%Y-%m-%d)"
year="${date:0:4}"
dir="docs/build-log/$year"
mkdir -p "$dir"
n="$( (ls "$dir"/"$date"-*.md 2>/dev/null || true) | wc -l | tr -d ' ')"  # ls fails on no match; pipefail must not abort
nn="$(printf '%02d' $((n + 1)))"
file="$dir/$date-$nn-$id.md"
blid="BL-${date//-/}-$nn"

esc() { printf '%s' "$1" | sed -e 's/[\/&|]/\\&/g'; }
sed -e "s|{{BLID}}|$(esc "$blid")|g" \
    -e "s|{{TASK}}|$(esc "$id")|g" \
    -e "s|{{TITLE}}|$(esc "$title")|g" \
    -e "s|{{DATE}}|$(esc "$date")|g" \
    -e "s|{{AGENT}}|$(esc "$agent")|g" \
    docs/build-log/TEMPLATE.md > "$file"

index="docs/build-log/README.md"
line="- [$blid · $id · $title]($year/$(basename "$file")) — In progress"
marker='<!-- entries: newest first -->'
if ! grep -qF "$marker" "$index"; then echo "Marker '$marker' missing in $index" >&2; exit 1; fi
tmp="$(mktemp)"
awk -v l="$line" -v m="$marker" '{ print } index($0, m) == 1 { print l }' "$index" > "$tmp" && mv "$tmp" "$index"
echo "$file"
