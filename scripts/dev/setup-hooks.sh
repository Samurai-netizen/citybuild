#!/usr/bin/env bash
# Enables the repository's git hooks (.githooks/) for this clone. Run once after cloning.
set -euo pipefail
root="$(git rev-parse --show-toplevel)"
cd "$root"
git config core.hooksPath .githooks
chmod +x .githooks/* scripts/dev/*.sh .claude/hooks/*.sh 2>/dev/null || true
echo "Git hooks enabled: core.hooksPath=$(git config core.hooksPath)"
