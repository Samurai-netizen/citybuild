#!/usr/bin/env bash
# Builds or previews the handbook site (website/README.md). Needs Node.js 20 or newer (CI uses 24).
#
# Usage:
#   bash scripts/dev/docs-site.sh build     # as CI: fails on broken links and anchors, and on pages missing from the sidebar
#   bash scripts/dev/docs-site.sh start     # live preview at http://localhost:3000/citybuild/ (restart after adding a page)
#   bash scripts/dev/docs-site.sh serve     # serve the last build at http://localhost:3000/citybuild/
#   bash scripts/dev/docs-site.sh install   # npm ci: the exact versions of website/package-lock.json
set -euo pipefail

cmd="${1:-build}"
root="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
site="$root/website"

case "$cmd" in
  -h|--help) sed -n '2,8p' "$0"; exit 0 ;;
  build|start|serve|install) ;;
  *) echo "docs-site: unknown command '$cmd' (build | start | serve | install)" >&2; exit 2 ;;
esac

if ! command -v node >/dev/null 2>&1; then
  echo "docs-site: Node.js 20 or newer is required (https://nodejs.org); see docs/getting-started.md." >&2
  exit 1
fi
major="$(node -p 'process.versions.node.split(".")[0]')"
if [ "$major" -lt 20 ]; then
  echo "docs-site: Node.js $(node --version) found; 20 or newer is required." >&2
  exit 1
fi

install() { (cd "$site" && npm ci --no-audit --no-fund); }

# npm writes node_modules/.package-lock.json on every install: an older one means the lockfile changed since.
if [ "$cmd" = "install" ] || [ ! -f "$site/node_modules/.package-lock.json" ] \
  || [ "$site/package-lock.json" -nt "$site/node_modules/.package-lock.json" ]; then
  install
fi

case "$cmd" in
  build) (cd "$site" && npm run build) ;;
  start) (cd "$site" && npm run start) ;;
  serve) (cd "$site" && npm run serve) ;;
esac
