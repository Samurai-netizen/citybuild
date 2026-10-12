#!/usr/bin/env bash
# PreToolUse hook for Bash: blocks commands that break the git workflow (core-instructions/18-git-and-ci.md).
# Complements the permission rules in .claude/settings.json, which can't see the current branch.
# Uses no jq/python so it runs in Git Bash on Windows, macOS and Linux. Tested by scripts/dev/test-guard-bash.sh.
#
# A command is analysed in five steps (best effort: it catches mistakes, it does not stop a determined bypass):
#   1. decode the JSON escapes of the command string;
#   2. drop here-document bodies, so text written into files or commit messages is never inspected;
#   3. drop quoted text (it may span lines) and comments;
#   4. split what is left into segments at && || ; | & ( ) { } and backticks;
#   5. test every segment on its own, so a flag in one command never counts against another command.
input="$(cat)"
cd "${CLAUDE_PROJECT_DIR:-.}" 2>/dev/null || exit 0

# Extract the "command" string from the tool input JSON (still JSON-escaped here).
cmd="$(printf '%s' "$input" | grep -oE '"command"[[:space:]]*:[[:space:]]*"([^"\\]|\\.)*"' | head -1 \
      | sed -E 's/^"command"[[:space:]]*:[[:space:]]*"//; s/"$//')"
[ -z "$cmd" ] && exit 0

# 1. JSON escapes -> real characters. "\\" goes through a placeholder first, so "\\n" stays a backslash and an n.
decode_json() {
  awk 'function repl(s, from, to,   out, i) {
         out = ""
         while ((i = index(s, from)) > 0) { out = out substr(s, 1, i - 1) to; s = substr(s, i + length(from)) }
         return out s
       }
       { ph = sprintf("%c", 1)
         s = repl($0, "\\\\", ph)
         s = repl(s, "\\n", "\n"); s = repl(s, "\\t", "\t"); s = repl(s, "\\r", "")
         s = repl(s, "\\\"", "\""); s = repl(s, "\\/", "/")
         s = repl(s, "\\u0026", "&"); s = repl(s, "\\u003c", "<"); s = repl(s, "\\u003e", ">")
         s = repl(s, "\\u003C", "<"); s = repl(s, "\\u003E", ">"); s = repl(s, "\\u0027", "\047")
         print repl(s, ph, "\\") }'
}

# 2. Remove the bodies of here-documents (<<WORD or <<-WORD, the word quoted or not), up to the line that is just WORD.
drop_heredocs() {
  awk -v sq="'" 'BEGIN { re = "<<-?[ \t]*[\"" sq "]?[A-Za-z_][A-Za-z0-9_]*[\"" sq "]?"; n = 0 }
    n > 0 { line = $0
            if (tab[1]) sub(/^\t+/, "", line)
            if (line == word[1]) { for (j = 1; j < n; j++) { word[j] = word[j + 1]; tab[j] = tab[j + 1] }; n-- }
            next }
    { print
      rest = $0; off = 0
      while (match(rest, re)) {
        start = off + RSTART; tok = substr(rest, RSTART, RLENGTH)
        rest = substr(rest, RSTART + RLENGTH); off = start + RLENGTH - 1
        if (start > 1 && substr($0, start - 1, 1) == "<") continue   # here-string <<<, not a here-document
        w = tok; sub(/^<<-?[ \t]*/, "", w); gsub("[\"" sq "]", "", w)
        n++; word[n] = w; tab[n] = (substr(tok, 3, 1) == "-")
      } }'
}

# 3. Remove quoted text (the quote state carries over to the next line) and comments.
drop_quoted() {
  awk -v sq="'" -v dq='"' 'BEGIN { inq = 0; insq = 0 }
    { out = ""; n = length($0)
      for (i = 1; i <= n; i++) {
        c = substr($0, i, 1)
        if (insq) { if (c == sq) insq = 0; continue }
        if (inq) { if (c == "\\") i++; else if (c == dq) inq = 0; continue }
        if (c == "\\") { out = out c substr($0, i + 1, 1); i++; continue }
        if (c == sq) { insq = 1; continue }
        if (c == dq) { inq = 1; continue }
        if (c == "#" && (out == "" || substr(out, length(out), 1) ~ /[ \t;&|(]/)) break
        out = out c
      }
      print out }'
}

# 4. One command per line. Redirections such as 2>&1 are not separators.
split_segments() {
  awk '{ gsub(/[0-9]*>&[0-9-]*/, " "); gsub(/&>>?/, ">"); gsub(/&&|\|\||[;|&(){}`]/, "\n"); print }'
}

deny() {
  local reason="$1"
  reason="${reason//\\/\\\\}"; reason="${reason//\"/\\\"}"
  printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"%s"}}\n' "$reason"
  exit 0
}

branch="$(git branch --show-current 2>/dev/null)"
on_main=0
{ [ "$branch" = "main" ] || [ "$branch" = "master" ]; } && on_main=1

# 5. Test each segment on its own.
segments="$(printf '%s' "$cmd" | decode_json | drop_heredocs | drop_quoted | split_segments)"
while IFS= read -r seg; do
  case "$seg" in *git*|*--no-verify*|*ALLOW_MAIN_COMMIT*) ;; *) continue ;; esac   # cheap filter before the greps
  if printf '%s' "$seg" | grep -qE -- '--no-verify|git[[:space:]]+commit[^&|;]*[[:space:]]-[a-zA-Z]{0,2}n[a-zA-Z]{0,2}([[:space:]]|$)'; then
    deny "Bypassing git hooks is not allowed (core-instructions/18-git-and-ci.md). Fix what the hook reports instead."
  fi
  if printf '%s' "$seg" | grep -qE 'ALLOW_MAIN_COMMIT'; then
    deny "ALLOW_MAIN_COMMIT is reserved for the owner. Work on a task branch (core-instructions/16-session-protocol.md §B)."
  fi
  if [ "$on_main" = 1 ] && printf '%s' "$seg" | grep -qE 'git[[:space:]]+commit'; then
    deny "Committing on $branch is not allowed. Create a task branch first: git switch -c task/<ID>-<slug>."
  fi
  if printf '%s' "$seg" | grep -qE 'git[[:space:]]+push'; then
    if printf '%s' "$seg" | grep -qE -- '(--force|--force-with-lease|[[:space:]]-f([[:space:]]|$)|[[:space:]]\+[A-Za-z])'; then
      deny "Force-pushing is not allowed (core-instructions/18-git-and-ci.md)."
    fi
    if printf '%s' "$seg" | grep -qE '(:|[[:space:]])(main|master)([[:space:]]|$)'; then
      deny "Pushing to main is not allowed. Push your task branch and open a PR."
    fi
    if [ "$on_main" = 1 ]; then
      deny "You are on $branch. Pushing from main is not allowed; work on a task branch."
    fi
  fi
done <<< "$segments"
exit 0
