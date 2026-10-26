#!/usr/bin/env bash
# Regression tests for .claude/hooks/guard-bash.sh (core-instructions/18-git-and-ci.md, "Claude Code guardrails").
# Feeds the hook the JSON a Bash tool call produces and checks "allow" or "deny", on a task branch and on main.
# Usage: bash scripts/dev/test-guard-bash.sh [path/to/guard-bash.sh]    (default: .claude/hooks/guard-bash.sh)
set -uo pipefail

root="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
guard="${1:-$root/.claude/hooks/guard-bash.sh}"
[ -f "$guard" ] || { echo "guard not found: $guard" >&2; exit 2; }

# Two throwaway repositories, so the hook sees "main" in one and a task branch in the other. No commits are needed.
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
mkdir "$tmp/main" "$tmp/task"
git init -q "$tmp/main" && git -C "$tmp/main" symbolic-ref HEAD refs/heads/main
git init -q "$tmp/task" && git -C "$tmp/task" symbolic-ref HEAD refs/heads/task/x

# The text as the inside of a JSON string: backslashes and quotes escaped, line breaks as \n.
json_escape() { printf '%s' "$1" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g' | awk 'BEGIN { ORS = "" } NR > 1 { print "\\n" } { print }'; }

passed=0
failed=0
# Runs the guard on one command in the main or the task test repository and counts a pass when it decides as wanted.
check() {  # check <allow|deny> <main|task> "<what is tested>" "<the Bash command>"
  local want="$1" where="$2" what="$3" cmd="$4" json out got
  json="{\"session_id\":\"t\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"$(json_escape "$cmd")\",\"description\":\"test\"}}"
  out="$(printf '%s' "$json" | CLAUDE_PROJECT_DIR="$tmp/$where" bash "$guard" 2>&1)"
  case "$out" in
    '') got=allow ;;
    *'"permissionDecision":"deny"'*) got=deny ;;
    *) got="unexpected output: $out" ;;
  esac
  if [ "$got" = "$want" ]; then
    passed=$((passed + 1))
  else
    failed=$((failed + 1))
    echo "FAIL [$where] $what: want $want, got $got"
    printf '     command: %s\n' "$cmd"
  fi
}

# --- allowed: ordinary work on a task branch ---------------------------------------------------------
check allow task "status"                              'git status'
check allow task "push of the current branch"          'git push -u origin HEAD'
check allow task "plain push"                          'git push'
check allow task "push, then switch to main"           'git push -u origin HEAD && git switch main'
check allow task "push, then rm -f"                    'git push && rm -f build.log'
check allow task "push with redirect and pipe"         'git push origin task/x 2>&1 | tail -2'
check allow task "commit, then push"                   'git commit -m "feat(M0-01): x" && git push -u origin HEAD'
check allow task "diff against main"                   'git diff --stat origin/main...HEAD'
check allow task "fetch main"                          'git fetch origin main'
check allow task "switch to main"                      'git switch main'
check allow task "comment mentions a forced push"      $'# never force-push: git push --force\ngit status'
# text inside quotes is not inspected
check allow task "double-quoted message"               'git commit -m "explain why --no-verify and git push --force are denied"'
check allow task "single-quoted message"               "git commit -m 'mentions --no-verify'"
check allow task "multi-line quoted message"           $'git commit -m "first line\nmentions git push -f and main"'
# here-document bodies are not inspected
check allow task "heredoc mentions a forced push"      $'cat > notes.md <<\'EOF\'\nnever run git push --force here\nEOF'
check allow task "heredoc mentions the override"       $'cat > notes.md <<\'EOF\'\nthe owner sets ALLOW_MAIN_COMMIT=1 locally\nEOF'
check allow task "heredoc mentions a bypass"           $'cat <<\'EOF\'\ngit commit --no-verify is blocked\nEOF'
check allow task "heredoc with an apostrophe, then push" $'cat > f <<\'EOF\'\ndon\'t touch main\nEOF\ngit push -u origin HEAD'
check allow task "commit message from a heredoc"       $'git commit -m "$(cat <<\'EOF\'\nfeat: x\n\nmentions git push --force and ALLOW_MAIN_COMMIT\nEOF\n)"'
# on main, only reading and creating a branch
check allow main "status on main"                      'git status'
check allow main "create a task branch"                'git switch -c task/y'
check allow main "log on main"                         'git log --oneline -5'

# --- denied --------------------------------------------------------------------------------------------
check deny task "force push"                           'git push --force'
check deny task "short force push"                     'git push -f origin task/x'
check deny task "force with lease"                     'git push --force-with-lease'
check deny task "plus refspec"                         'git push origin +task/x'
check deny task "push to main"                         'git push origin main'
check deny task "push HEAD:main"                       'git push origin HEAD:main'
check deny task "push with --no-verify"                'git push --no-verify'
check deny task "commit with --no-verify"              'git commit --no-verify -m "x"'
check deny task "commit -n after a message"            'git commit -m "x" -n'
check deny task "commit -nm"                           'git commit -nm "x"'
check deny task "commit -a -n"                         'git commit -a -n -m "x"'
check deny task "--no-verify after a quoted message"   'git commit -m "x" --no-verify'
check deny task "override variable as prefix"          'ALLOW_MAIN_COMMIT=1 git commit -m "x"'
check deny task "override variable exported"           'export ALLOW_MAIN_COMMIT=1'
check deny task "force push in a later segment"        'git add -A && git push --force-with-lease'
check deny task "force push after a semicolon"         'git push -u origin HEAD; git push -f'
check deny task "force push after a pipe"              'echo ok | git push --force'
check deny task "force push in a subshell"             '(git push -f)'
check deny task "push to main in a later segment"      'git push origin task/x && git push origin main'
check deny task "redirect before the flag"             'git push 2>&1 --force'
check deny task "force push after a heredoc"           $'cat > f <<\'EOF\'\nharmless\nEOF\ngit push --force'
check deny task "push to main after a heredoc"         $'cat > f <<\'EOF\'\ndon\'t\nEOF\ngit push origin main'
check deny task "force push after a comment"           $'# don\'t forget\ngit push --force'
check deny task "force push after a heredoc message"   $'git commit -m "$(cat <<\'EOF\'\nfeat: x\nEOF\n)" && git push --force'
check deny main "push from main"                       'git push'
check deny main "commit on main"                       'git commit -m "x"'

echo "guard-bash tests: $passed passed, $failed failed (guard: $guard)"
[ "$failed" -eq 0 ]
