# BL-20261012-01 · KIT-AUDIT-FIXES — Kit audit fixes: A1, A2, A3, A16

- **Status:** In progress
- **Task:** KIT-AUDIT-FIXES: owner-approved follow-ups A1, A2, A3 and A16 of the M0-01 kit audit
  (no plan task id; maintenance work, commits are `<type>(kit)` / `<type>(repo)`)
- **Branch / PR:** `task/kit-audit-fixes` / —
- **Started:** 2026-10-12 · **First agent:** Claude Code / claude-sonnet-5-5

<!-- Status values: In progress | Done | Partial | Blocked. One entry per task; every session appends a section below.
     Once merged, this file is append-only: corrections go into a new entry. -->

## Sessions

### Session 1 — 2026-10-12 · Claude Code / claude-sonnet-5-5
- **Model / mode:** Sonnet 5.5, auto mode. The owner switched the model to Sonnet 5.5 for this task. No plan task
  exists, so the tag is the owner's choice. Source of the work: M0-01 audit table (`docs/build-log/2026/2026-10-08-01-M0-01.md`).
- **Plan:**
  1. **A16** `chore(repo)`: untrack `.DS_Store` (`git rm --cached`; the file stays on disk, `.gitignore` already lists it).
  2. **A1** `fix(kit)`: `CLAUDE.md` "Model and mode": `[PLAN]` tasks do the write-ahead first, then present the plan and wait
     for approval. Remove the matching open-issue line from `13`.
  3. **A3** `fix(kit)`: `scripts/dev/**` joins the protected files: list in `15`, ask rule in `.claude/settings.json`,
     the protected-file notice in `scripts/dev/check-docs.sh`.
  4. **A2** `fix(kit)`: rewrite the command analysis in `.claude/hooks/guard-bash.sh` (decode the JSON escapes, drop heredoc
     bodies, drop quoted text across lines, test each command segment on its own) plus a regression test
     `scripts/dev/test-guard-bash.sh`; update the guard description in `18`.
  5. Finish per `16` §E: docs, CHANGELOG, resume point, `check-docs.sh --base origin/main`, PR. Never merge.
- **Done:** write-ahead (branch, this entry, resume point).
- **Done (cont.):** step 1 (A16) — `.DS_Store` untracked; the file stays on disk, ignored (`481aff4`).
- **Done (cont.):** step 2 (A1) — `CLAUDE.md` "Model and mode" reworded; the matching open-issue line removed from `13`.
- **Stopped at / next:** step 3 (A3).

## Decisions
<!-- Every decision, when it is made, including small ones. Scope: local | project-wide (→ 12-decisions.md row) | architectural (→ ADR). -->
| # | Decision | Alternatives considered | Why | Scope |
|---|---|---|---|---|
| 1 | One branch, one build log entry and one PR for A1, A2, A3 and A16; commits typed `fix(kit)` / `chore(repo)` | Two PRs (a kit PR and a `chore(repo)` PR, as the M0-01 follow-up table suggested) | It is one owner instruction. Parallel PRs would both edit the resume point, the build log index and the CHANGELOG, so the second merge would conflict. The owner can ask for a split | local |
| 2 | Build log id `KIT-AUDIT-FIXES`, branch `task/kit-audit-fixes`, commit id `kit` / `repo` | Reuse the id `KIT` | The `KIT` entry is merged and append-only (`check-docs.sh` rejects edits); `new-log-entry.sh KIT` would only point at it | local |
| 3 | Edit protected files with the Edit/Write tools, not with `sed` or heredocs in Bash | `sed -i` in Bash (auto mode prefers Bash) | The `ask` rules in `.claude/settings.json` only fire for the Edit/Write tools; `CLAUDE.md` says not to work around them. The owner's approval is quoted under "Owner input", and every protected-file edit stays visible | local |
| 4 | Write text that mentions force flags, the override variable or a hook bypass with the Write tool until A2 is fixed | Bash heredocs | `guard-bash.sh` still has the A2 false positives; the Write tool is not affected | local |
| 5 | A1 wording: the approved proposal sentence plus one clause saying why ("Plan Mode forbids the write-ahead's file writes, so don't use it for these tasks") | The proposal sentence alone | The audit's finding is that Plan Mode and the write-ahead conflict; a coder who reads only the first sentence could still enter Plan Mode and hit the conflict. The owner can drop the clause | local |

## Owner input
<!-- Approvals, answers and instructions from the owner, quoted, with where they were persisted. -->
- 2026-10-12 · "Approve A1/A2/A3 fixes, do A16" → A1, A2, A3 as proposed in the M0-01 audit table (A3: the owner's decision is
  "yes, `scripts/dev/**` is protected"); A16 as proposed (untrack `.DS_Store`).
  Protected-file edits covered by this answer: `CLAUDE.md` (A1), `.claude/hooks/guard-bash.sh` (A2),
  `.claude/settings.json` and `core-instructions/15-change-control.md` (A3).
  Not covered, so not touched: `AGENTS.md` (see Follow-ups), `.github/workflows/**`, A8, A9.

## Changes
- **Code:** —
- **Tests:** —
- **Docs:** —

## Verification
| Command | Result |
|---|---|
| Step 1 · `git rm --cached .DS_Store` | staged as `D`; no other `.DS_Store` was tracked (`git ls-files` showed only the root one) |
| Step 1 · `ls -la .DS_Store`, `git check-ignore -v .DS_Store` | file still on disk (10244 bytes); ignored by `.gitignore:21` |
| Step 1 · `git status --short` after the commit | empty (the Finder change no longer shows up) |

## Problems and surprises
- Starting state: local `main` was 3 commits behind `origin/main`. PR #4 (M0-01) and the Dependabot PRs #1 and #2 were already
  merged by the owner, so the old branch `task/M0-01-kit-audit` is obsolete (its remote branch is gone). A local copy remains.
- `.DS_Store` showed as modified at session start (Finder). It is being untracked, so the change is not committed.

## Follow-ups
- —
