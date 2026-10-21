#!/usr/bin/env bash
# Documentation and process checks (core-instructions/17-documentation-system.md §5).
# Runs in the git pre-commit hook, in CI, and by AI coders before finishing a task.
#
# Usage:
#   bash scripts/dev/check-docs.sh                    # static checks
#   bash scripts/dev/check-docs.sh --base origin/main # + checks on the changes since <base> (PRs)
set -uo pipefail

base=""
while [ $# -gt 0 ]; do
  case "$1" in
    --base) base="${2:-}"; shift 2 ;;
    -h|--help) sed -n '2,8p' "$0"; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

root="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
cd "$root"
errors=0
fail() { echo "✗ $*"; errors=$((errors + 1)); }
note() { echo "• $*"; }

# Markdown files known to git (tracked + untracked, excluding ignored), or all if not a git repo.
list_md() {
  if git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    { git ls-files '*.md'; git ls-files --others --exclude-standard '*.md'; } | sort -u
  else
    find . -name '*.md' -not -path './.git/*' | sed 's|^\./||' | sort
  fi
}
list_files() {  # $1 = glob pattern for git ls-files
  { git ls-files "$1" 2>/dev/null; git ls-files --others --exclude-standard "$1" 2>/dev/null; } | sort -u
}
regex_escape() { printf '%s' "$1" | sed -e 's/[][\.*^$+?(){}|]/\\&/g'; }

# Rows of a build log entry's "## Docs impact" table, without its header and separator lines.
impact_table_rows() {
  awk '/^## Docs impact/ { p = 1; next } /^## / { p = 0 } p && /^\|/' "$1" | grep -vE '^\|[[:space:]|:-]*$' | tail -n +2
}

# The module doc of a source file: the nearest .csproj or .asmdef above it names the module (17 §2); tests have none.
module_doc_for() {
  local f="$1" dir proj name
  case "$f" in *.meta) return ;; esac
  case "$f" in
    shared/Simulation/*|shared/Simulation.Build/*) name="CityBuilder.Simulation" ;;
    client/CityBuilder/Assets/_Project/Resources/*) name="CityBuilder.Client.Presentation" ;;  # its UI assets (decision 024)
    server/*|client/CityBuilder/Assets/_Project/*|tools/*)
      dir="$(dirname "$f")"
      while [ "$dir" != "." ] && [ -z "${proj:-}" ]; do
        proj="$(printf '%s\n' "$project_files" | awk -v d="$dir/" 'index($0, d) == 1 && substr($0, length(d) + 1) !~ /\//' | head -1)"
        dir="$(dirname "$dir")"
      done
      [ -n "${proj:-}" ] || return
      name="$(basename "${proj%.*}")" ;;
    *) return ;;
  esac
  case "$name" in *Tests*) return ;; esac
  [ -f "docs/modules/$name.md" ] && echo "docs/modules/$name.md"
}

# Pages that describe the present. History (build log entries, CHANGELOG), records of the time (ADRs, CRs) and the plan keep old names.
living_docs=('*.md' ':!docs/build-log/[0-9]*' ':!docs/adr/ADR-*' ':!docs/changes/CR-*' ':!docs/plan/*' ':!CHANGELOG.md')

# 1. Required files ---------------------------------------------------------------------------
required=(README.md AGENTS.md CLAUDE.md CONTRIBUTING.md CHANGELOG.md
  core-instructions/README.md core-instructions/02-principles.md core-instructions/13-progress.md
  core-instructions/15-change-control.md core-instructions/16-session-protocol.md
  docs/README.md docs/getting-started.md docs/plan/M0-PLAN.md
  docs/build-log/README.md docs/build-log/TEMPLATE.md
  docs/changes/README.md docs/changes/TEMPLATE.md
  docs/adr/README.md docs/adr/TEMPLATE.md
  docs/modules/README.md docs/modules/TEMPLATE.md
  .github/pull_request_template.md)
for f in "${required[@]}"; do [ -f "$f" ] || fail "Required file missing: $f"; done

# 2. Relative links in Markdown resolve -------------------------------------------------------
tmp_links="$(mktemp)"
while IFS= read -r md; do
  [ -f "$md" ] || continue
  dir="$(dirname "$md")"
  # drop fenced code blocks and inline code spans, then extract ](target) links
  awk 'BEGIN{f=0} /^[[:space:]]*```/{f=!f; next} !f{print}' "$md" \
    | sed -E 's/`[^`]*`//g' \
    | grep -oE '\]\([^)[:space:]]+([[:space:]]+"[^"]*")?\)' \
    | sed -E 's/^\]\(//; s/\)$//; s/[[:space:]]+".*"$//' \
    | while IFS= read -r target; do
        case "$target" in http://*|https://*|mailto:*|\#*|'') continue ;; esac
        path="${target%%#*}"
        [ -z "$path" ] && continue
        case "$path" in /*) resolved="${path#/}" ;; *) resolved="$dir/$path" ;; esac
        [ -e "$resolved" ] || echo "BROKEN $md -> $target"
      done
done < <(list_md) > "$tmp_links" 2>/dev/null
while IFS= read -r line; do fail "Broken link: ${line#BROKEN }"; done < "$tmp_links"
rm -f "$tmp_links"

# 3. Build log: entries indexed, required sections present -----------------------------------
index="docs/build-log/README.md"
if [ -f "$index" ]; then
  for entry in docs/build-log/[0-9][0-9][0-9][0-9]/*.md; do
    [ -f "$entry" ] || continue
    rel="${entry#docs/build-log/}"
    grep -qF "($rel)" "$index" || fail "Build log entry not listed in $index: $entry"
    for section in '## Decisions' '## Verification' '- **Status:**'; do
      grep -qF -- "$section" "$entry" || fail "Build log entry $entry lacks '$section'"
    done
  done
fi

# 4. CR and ADR status lines -------------------------------------------------------------------
for cr in docs/changes/CR-[0-9][0-9][0-9][0-9]-*.md; do
  [ -f "$cr" ] || continue
  grep -qE '^- \*\*Status:\*\* (Proposed|Approved|In progress|Done|Rejected|Superseded)' "$cr" \
    || fail "$cr: missing or invalid '- **Status:** …' line"
  grep -qF "$(basename "$cr")" docs/changes/README.md || fail "$cr not listed in docs/changes/README.md"
done
for adr in docs/adr/ADR-[0-9][0-9][0-9][0-9]-*.md; do
  [ -f "$adr" ] || continue
  grep -qE '^- \*\*Status:\*\* (Proposed|Accepted|Deprecated|Superseded)' "$adr" \
    || fail "$adr: missing or invalid '- **Status:** …' line"
  grep -qF "$(basename "$adr")" docs/adr/README.md || fail "$adr not listed in docs/adr/README.md"
done

# 5. A module doc for every project and assembly ----------------------------------------------
while IFS= read -r proj; do
  [ -n "$proj" ] || continue
  case "$proj" in */tests/*|*Tests*) continue ;; esac
  name="$(basename "$proj" .csproj)"
  [ -f "docs/modules/$name.md" ] || fail "Missing module doc docs/modules/$name.md for $proj"
done < <(list_files '*.csproj')
while IFS= read -r asm; do
  [ -n "$asm" ] || continue
  case "$asm" in */Tests/*|*Tests*) continue ;; esac
  case "$asm" in client/*/Assets/_Project/*|shared/*) ;; *) continue ;; esac
  name="$(basename "$asm" .asmdef)"
  [ -f "docs/modules/$name.md" ] || fail "Missing module doc docs/modules/$name.md for $asm"
done < <(list_files '*.asmdef')
for doc in docs/modules/*.md; do
  case "$(basename "$doc")" in README.md|TEMPLATE.md) continue ;; esac
  grep -qE '^Last updated: ' "$doc" || fail "$doc: missing 'Last updated: <task id>' line"
done

# 6. Always-loaded files stay small (context budget) -----------------------------------------
check_lines() { [ -f "$1" ] && [ "$(wc -l < "$1")" -gt "$2" ] && fail "$1 has $(wc -l < "$1") lines (limit $2)"; }
check_lines AGENTS.md 150
check_lines CLAUDE.md 80
check_lines core-instructions/02-principles.md 70
check_lines core-instructions/13-progress.md 80

# 7. Change checks against a base (pull requests) -----------------------------------------------
if [ -n "$base" ]; then
  if ! git rev-parse --verify --quiet "$base" >/dev/null; then
    fail "Base ref '$base' not found (fetch it first, e.g. git fetch origin main)"
  else
    changes="$(git diff --name-status "$base"...HEAD)"
    names="$(printf '%s\n' "$changes" | awk '{print $NF}')"
    project_files="$(list_files '*.csproj'; list_files '*.asmdef')"
    if printf '%s\n' "$names" | grep -qE '^(server/|shared/|client/CityBuilder/(Assets|Packages|ProjectSettings)/|tools/|scripts/|website/)'; then
      printf '%s\n' "$names" | grep -qE '^docs/build-log/[0-9]{4}/' || fail "Code changed but no build log entry was added or updated"
      printf '%s\n' "$names" | grep -qx 'core-instructions/13-progress.md' || fail "Code changed but core-instructions/13-progress.md was not updated"
      printf '%s\n' "$names" | grep -qx 'CHANGELOG.md' || fail "Code changed but CHANGELOG.md was not updated"
    fi
    # merged build log entries are append-only
    while IFS=$'\t' read -r status path rest; do
      case "$status" in
        M|D|R*) case "$path" in docs/build-log/[0-9][0-9][0-9][0-9]/*.md)
          git cat-file -e "$base:$path" 2>/dev/null && fail "Merged build log entry changed ($status): $path — add a new entry instead" ;;
        esac ;;
      esac
    done <<< "$changes"

    # Docs impact (17 §5): the "## Docs impact" tables of the build log entries this change adds or updates
    changed_logs="$(printf '%s\n' "$names" | grep -E '^docs/build-log/[0-9]{4}/.+\.md$' || true)"
    impact_rows=""
    while IFS= read -r entry; do
      [ -f "$entry" ] || continue
      impact_rows+="$(impact_table_rows "$entry")"$'\n'
    done <<< "$changed_logs"

    # a new entry plans its Docs impact; a Done one has a result in every row
    while IFS=$'\t' read -r status path rest; do
      [ "$status" = "A" ] || continue
      case "$path" in docs/build-log/[0-9][0-9][0-9][0-9]/*.md) ;; *) continue ;; esac
      [ -f "$path" ] || continue
      rows="$(impact_table_rows "$path")"
      if [ -z "$rows" ]; then
        fail "$path: no '## Docs impact' table with at least one row (core-instructions/17-documentation-system.md §5)"
      elif grep -qE '^- \*\*Status:\*\* Done' "$path"; then
        while IFS= read -r open_row; do
          [ -n "$open_row" ] && fail "$path is Done but this Docs impact row has no result (updated, new, deleted or 'no change: <why>'): $open_row"
        done < <(printf '%s\n' "$rows" | awk -F'|' '{ r = $(NF-1); gsub(/^[ \t]+|[ \t]+$/, "", r); if (r == "" || tolower(r) ~ /^(planned|todo|…|\.\.\.)$/) print }')
      fi
    done <<< "$changes"

    # a module's code changed: its module doc changed too, or a Docs impact row names it ("no change: <why>")
    while IFS= read -r doc; do
      [ -n "$doc" ] || continue
      printf '%s\n' "$names" | grep -qxF "$doc" && continue
      printf '%s\n' "$impact_rows" | grep -qF "$doc" && continue
      fail "Code of module $(basename "$doc" .md) changed but $doc did not: update it, or give it a Docs impact row ('no change: <why>') in the build log entry"
    done < <(printf '%s\n' "$names" | while IFS= read -r f; do [ -n "$f" ] && module_doc_for "$f"; done | sort -u)

    # a deleted or renamed file (or emptied folder) is no longer named by the living docs (history is exempt)
    while IFS=$'\t' read -r status old rest; do
      case "$status" in D|R*) ;; *) continue ;; esac
      [ -e "$old" ] && continue
      gone="$old"; dir="$(dirname "$old")"
      while [ "$dir" != "." ] && [ ! -e "$dir" ]; do gone+=$'\n'"$dir/"; dir="$(dirname "$dir")"; done
      what="deleted"; [ "${status#R}" != "$status" ] && what="renamed to $rest"
      while IFS= read -r g; do
        # a folder counts only when written as a path (`tools/`, `tools/DocTools`), never as the bare word
        case "$g" in */) pattern="$(regex_escape "$g")" ;; *) pattern="$(regex_escape "$g")([^A-Za-z0-9_-]|$)" ;; esac
        while IFS= read -r hit; do
          [ -n "$hit" ] && fail "${hit%%:*} still names '$g', which this change $what (line ${hit#*:})"
        done < <(git grep -n -E -e "(^|[^A-Za-z0-9_./-])$pattern" -- "${living_docs[@]}" 2>/dev/null)
      done <<< "$gone"
    done <<< "$changes"

    # protected files: allowed, but must be visible to the owner
    protected='^(AGENTS\.md|CLAUDE\.md|core-instructions/02-principles\.md|core-instructions/15-change-control\.md|core-instructions/22-game-mechanics\.md|\.claude/settings\.json|\.claude/hooks/|\.githooks/|scripts/dev/|\.github/workflows/)'
    printf '%s\n' "$names" | grep -E "$protected" | while IFS= read -r p; do note "Protected file changed (owner approval must be recorded in the build log): $p"; done
  fi
fi

if [ "$errors" -gt 0 ]; then
  echo "check-docs: $errors problem(s). See core-instructions/17-documentation-system.md."
  exit 1
fi
echo "check-docs: OK"
