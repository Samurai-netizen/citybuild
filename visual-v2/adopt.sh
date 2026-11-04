#!/usr/bin/env bash
# Copies visual v2's overlay (visual-v2/unity) into the Unity project (client/CityBuilder/Assets/_Project): the models, the palette, the UI
# files, fonts, glyphs and icons, the client code and its tests. A file that exists is replaced and keeps its .meta file; a new file gets
# its .meta on Unity's next import (ArtImportPostprocessor gives models, icons and glyphs their settings). It does not wire the new code
# into the views: the branch claude/visual-v2-fit-test shows those few edits (README.md, "Adopting it"). Run from the repository root.
set -euo pipefail

src="visual-v2/unity"
dst="client/CityBuilder/Assets/_Project"
if [ ! -d "$src" ] || [ ! -d "$dst" ]; then
  echo "adopt.sh: run it from the repository root (need $src and $dst)" >&2
  exit 1
fi

cp -R "$src/." "$dst/"
echo "Copied $(find "$src" -type f | wc -l | tr -d ' ') files from $src into $dst."
