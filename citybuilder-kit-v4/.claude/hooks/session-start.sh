#!/usr/bin/env bash
# SessionStart hook: prints the repository state. Claude Code adds this output to the session context.
# Read-only. Must never fail the session, so it always exits 0.
cd "${CLAUDE_PROJECT_DIR:-.}" 2>/dev/null || exit 0
if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  echo "Not a git repository yet. Follow Day 0 in docs/plan/M0-PLAN.md."
  exit 0
fi

branch="$(git branch --show-current 2>/dev/null)"
echo "=== Repository state at session start ==="
echo "Branch: ${branch:-'(detached HEAD)'}"
case "$branch" in
  main|master) echo "Note: you are on $branch. Never edit code here; /start-task creates a task branch." ;;
esac

changes="$(git status --porcelain 2>/dev/null)"
if [ -n "$changes" ]; then
  count="$(printf '%s\n' "$changes" | wc -l | tr -d ' ')"
  echo "Uncommitted changes: $count file(s). A previous session may have been interrupted:"
  printf '%s\n' "$changes" | head -15
  echo "→ Read the Resume point in core-instructions/13-progress.md and follow core-instructions/16-session-protocol.md §F before changing anything."
fi

upstream="$(git rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>/dev/null || true)"
if [ -n "$upstream" ]; then
  ahead="$(git rev-list --count "$upstream"..HEAD 2>/dev/null || echo 0)"
  [ "${ahead:-0}" -gt 0 ] && echo "Unpushed commits: $ahead (push at the next checkpoint)."
elif [ -n "$branch" ] && [ "$branch" != "main" ] && [ "$branch" != "master" ]; then
  echo "This branch has no upstream yet (git push -u origin HEAD at the next checkpoint)."
fi

echo "Last commits:"
git log --oneline -5 2>/dev/null | sed 's/^/  /'

if [ "$(git config core.hooksPath 2>/dev/null)" != ".githooks" ]; then
  echo "WARNING: git hooks are not enabled. Run: bash scripts/dev/setup-hooks.sh"
fi

state_line="$(grep -m1 -E '^- \*\*State:\*\*' core-instructions/13-progress.md 2>/dev/null || true)"
state_line="$(printf '%s' "$state_line" | sed -E 's/<!--.*-->//; s/\*\*//g; s/^- //; s/[[:space:]]+$//')"
[ -n "$state_line" ] && echo "Resume point → $state_line"
echo "Protocol: core-instructions/16-session-protocol.md · Skills: /resume /start-task /checkpoint /finish-task /change-request /docs-sync"
exit 0
