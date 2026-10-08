#!/usr/bin/env bash
# Stop hook: Claude may not end a turn while code changes are not checkpointed
# (resume point + build log updated). See core-instructions/16-session-protocol.md §D.
# Blocks at most once per turn: if Claude stops again right after being blocked, it is allowed (stop_hook_active).
input="$(cat)"
if printf '%s' "$input" | grep -qE '"stop_hook_active"[[:space:]]*:[[:space:]]*true'; then exit 0; fi
cd "${CLAUDE_PROJECT_DIR:-.}" 2>/dev/null || exit 0
git rev-parse --is-inside-work-tree >/dev/null 2>&1 || exit 0

paths="$(git status --porcelain 2>/dev/null | sed -E 's/^.. //; s/^"//; s/"$//; s/.* -> //')"
[ -z "$paths" ] && exit 0

code="$(printf '%s\n' "$paths" | grep -E '^(server/|shared/|client/CityBuilder/(Assets|Packages|ProjectSettings)/|tools/)' || true)"
[ -z "$code" ] && exit 0

branch="$(git branch --show-current 2>/dev/null)"
block() {
  local reason="$1"
  reason="${reason//\\/\\\\}"; reason="${reason//\"/\\\"}"
  printf '{"decision":"block","reason":"%s"}\n' "$reason"
  exit 0
}

if [ "$branch" = "main" ] || [ "$branch" = "master" ]; then
  block "There are uncommitted code changes on $branch. Move them to a task branch (git switch -c task/<ID>-<slug>), then checkpoint per core-instructions/16-session-protocol.md §D."
fi

progress="$(printf '%s\n' "$paths" | grep -E '^core-instructions/13-progress\.md$' || true)"
log="$(printf '%s\n' "$paths" | grep -E '^docs/build-log/' || true)"
if [ -z "$progress" ] || [ -z "$log" ]; then
  block "Uncommitted code changes are not checkpointed. Update the Resume point (core-instructions/13-progress.md) and the task's build log entry (docs/build-log/), then commit and push (/checkpoint). If these changes aren't yours (e.g. Unity Editor files from the owner), say so instead of committing them."
fi
exit 0
