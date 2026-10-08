#!/usr/bin/env bash
# PreToolUse hook for Bash: blocks commands that break the git workflow (core-instructions/18-git-and-ci.md).
# Complements the permission rules in .claude/settings.json, which can't see the current branch.
# Uses no jq/python so it runs in Git Bash on Windows, macOS and Linux.
input="$(cat)"
cd "${CLAUDE_PROJECT_DIR:-.}" 2>/dev/null || exit 0

# Extract the "command" string from the tool input JSON (handles escaped quotes).
cmd="$(printf '%s' "$input" | grep -oE '"command"[[:space:]]*:[[:space:]]*"([^"\\]|\\.)*"' | head -1 \
      | sed -E 's/^"command"[[:space:]]*:[[:space:]]*"//; s/"$//')"
[ -z "$cmd" ] && exit 0

# Remove quoted text (commit messages etc.) so words inside quotes don't trigger the checks.
nq="$(printf '%s' "$cmd" | awk 'BEGIN { sq = sprintf("%c", 39) }
  { out = ""; inq = 0; insq = 0; n = length($0)
    for (i = 1; i <= n; i++) {
      c = substr($0, i, 1)
      if (!insq && c == "\\" && substr($0, i + 1, 1) == "\"") { inq = !inq; i++; continue }
      if (!inq && c == sq) { insq = !insq; continue }
      if (!inq && !insq) out = out c
    }
    print out }')"

deny() {
  local reason="$1"
  reason="${reason//\\/\\\\}"; reason="${reason//\"/\\\"}"
  printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"%s"}}\n' "$reason"
  exit 0
}

branch="$(git branch --show-current 2>/dev/null)"

if printf '%s' "$nq" | grep -qE -- '--no-verify|git[[:space:]]+commit[^&|;]*[[:space:]]-[a-zA-Z]{0,2}n[a-zA-Z]{0,2}([[:space:]]|$)'; then
  deny "Bypassing git hooks is not allowed (core-instructions/18-git-and-ci.md). Fix what the hook reports instead."
fi
if printf '%s' "$nq" | grep -qE 'ALLOW_MAIN_COMMIT'; then
  deny "ALLOW_MAIN_COMMIT is reserved for the owner. Work on a task branch (core-instructions/16-session-protocol.md §B)."
fi
if printf '%s' "$nq" | grep -qE 'git[[:space:]]+commit' && { [ "$branch" = "main" ] || [ "$branch" = "master" ]; }; then
  deny "Committing on $branch is not allowed. Create a task branch first: git switch -c task/<ID>-<slug>."
fi
if printf '%s' "$nq" | grep -qE 'git[[:space:]]+push'; then
  if printf '%s' "$nq" | grep -qE -- '(--force|--force-with-lease|[[:space:]]-f([[:space:]]|$)|[[:space:]]\+[A-Za-z])'; then
    deny "Force-pushing is not allowed (core-instructions/18-git-and-ci.md)."
  fi
  if printf '%s' "$nq" | grep -qE '(:|[[:space:]])(main|master)([[:space:]]|$)'; then
    deny "Pushing to main is not allowed. Push your task branch and open a PR."
  fi
  if { [ "$branch" = "main" ] || [ "$branch" = "master" ]; }; then
    deny "You are on $branch. Pushing from main is not allowed; work on a task branch."
  fi
fi
exit 0
