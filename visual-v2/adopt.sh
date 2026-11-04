#!/usr/bin/env bash
# Copies visual v2's overlay (visual-v2/unity) into the Unity project (client/CityBuilder/Assets/_Project) without undoing anyone's work:
# the package was made against main at BASE (3c0dcac), so a file the project changed after BASE is not overwritten blindly.
#   - new, or unchanged since BASE: copied;
#   - already the same as the package's: left alone;
#   - changed since BASE (or added by someone else under the same name): a model (.fbx), an icon or glyph (.png) or a font (.ttf) is
#     replaced by the package's on purpose (the package's art is the new art) and listed; the palette and every text file (C#, UXML,
#     USS, licence) are held back and listed for a hand merge (visual-v2/ADOPT.md section 3). Exit code 3 when anything is held back.
# A file that exists keeps its .meta file; a new file gets its .meta on Unity's next import (ArtImportPostprocessor gives models, icons
# and glyphs their settings). It does not wire the new code into the views (unity-wiring.patch, ADOPT.md). Run from the repository root.
# Plain bash 3.2 (macOS) and git; no arrays, so set -u is safe on old shells.
set -euo pipefail

base="3c0dcac"
src="visual-v2/unity"
dst="client/CityBuilder/Assets/_Project"
if [ ! -d "$src" ] || [ ! -d "$dst" ]; then
  echo "adopt.sh: run it from the repository root (need $src and $dst)" >&2
  exit 1
fi
if ! git cat-file -e "$base^{commit}" 2>/dev/null; then
  echo "adopt.sh: commit $base (the package's base) is not in this clone; fetch the full history (git fetch --unshallow)" >&2
  exit 1
fi

copied=0
same=0
replaced=""
held=""
while IFS= read -r f; do
  from="$src/$f"
  to="$dst/$f"
  if [ -e "$to" ] && cmp -s "$from" "$to"; then
    same=$((same + 1))
    continue
  fi
  if [ ! -e "$to" ] && ! git cat-file -e "HEAD:$to" 2>/dev/null; then
    unchanged=1
  elif git cat-file -e "$base:$to" 2>/dev/null && git diff --quiet "$base" -- "$to"; then
    unchanged=1
  else
    unchanged=0
  fi
  if [ "$unchanged" = 0 ]; then
    case "$f" in
      Art/Textures/*)
        held="$held  $f
"
        continue
        ;;
      *.fbx | *.png | *.ttf)
        replaced="$replaced  $f
"
        ;;
      *)
        held="$held  $f
"
        continue
        ;;
    esac
  fi
  mkdir -p "$(dirname "$to")"
  cp "$from" "$to"
  copied=$((copied + 1))
done < <(cd "$src" && find . -type f | sed 's|^\./||' | sort)

echo "Copied $copied files from $src into $dst ($same already the same)."
if [ -n "$replaced" ]; then
  echo "Replaced on purpose (changed or added after $base; the package's art wins):"
  printf '%s' "$replaced"
fi
if [ -n "$held" ]; then
  echo "Held back, changed after $base: merge by hand (ADOPT.md section 3), keeping the project's names and behaviour:"
  printf '%s' "$held"
  exit 3
fi
