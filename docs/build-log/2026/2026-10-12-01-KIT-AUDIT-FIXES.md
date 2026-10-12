# BL-20261012-01 · KIT-AUDIT-FIXES — Kit audit fixes: A1, A2, A3, A9, A15, A16

- **Status:** Done
- **Task:** KIT-AUDIT-FIXES: owner-approved follow-ups A1, A2, A3, A16 (Session 1) and A9, A15 (Session 2) of the M0-01 kit audit
  (no plan task id; maintenance work, commits are `<type>(kit)` / `<type>(repo)`)
- **Branch / PR:** `task/kit-audit-fixes` / PR #5
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
- **Done (cont.):** step 3 (A3) — `scripts/dev/**` added to the protected files in `15`, to the `ask` rules in
  `.claude/settings.json` and to the protected-file notice in `scripts/dev/check-docs.sh`.
- **Done (cont.):** step 4 (A2) — regression test first (old guard: 39 passed, 9 failed), new `guard-bash.sh` developed in the
  scratchpad and installed only after 48/48 passed; guard description in `18`; CHANGELOG line.
- **Done (cont.):** step 5 — `check-docs.sh --base origin/main`, resume point IDLE, PR opened (not merged).
- **Stopped at / next:** task finished; waiting for the owner to merge the PR. Next task: M0-02.

### Session 2 — 2026-10-12 · Claude Code / claude-sonnet-5-5
- **Why:** the owner answered the two audit items left open in Session 1 (see "Owner input"). PR #5 was still open and
  clean, so the work goes on the same branch and the PR is updated; a second PR would conflict on the resume point,
  the index and the CHANGELOG (decision 1).
- **Plan:**
  1. Write-ahead: this section, the resume point IN PROGRESS, push.
  2. **A15** `docs(kit)`: record the owner's decision as standing directive OD-8 in `15` (no LICENSE file for now,
     the README keeps its general "Proprietary, all rights reserved" line); drop A15 from the open questions in `13`.
  3. **A9** `fix(kit)`: `scripts/` counts as code in `scripts/dev/check-docs.sh` (§7) and `.claude/hooks/stop-checkpoint.sh`;
     name the code paths in `17` §5. Verify before/after in a scratch clone (both scripts).
  4. Finish: CHANGELOG line, PR title and body, resume point IDLE, `check-docs.sh --base origin/main`, push.
- **Done:** step 1 (write-ahead, `88dca68`); step 2 (A15) — OD-8 added to `15`; A15 dropped from the open questions in `13`.
  The README is unchanged: its License line already says "Proprietary, all rights reserved (owner to decide before any public release)".
- **Done (cont.):** step 3 (A9) — `scripts/` added to the code paths in `scripts/dev/check-docs.sh` (§7) and
  `.claude/hooks/stop-checkpoint.sh`; the code paths are now named in `17` §5. Verified before/after in scratch clones.
- **Done (cont.):** step 4 — CHANGELOG line, `check-docs.sh --base origin/main`, resume point IDLE, PR #5 title and body updated
  (not merged).
- **Stopped at / next:** task finished; waiting for the owner to merge PR #5. Next task: M0-02.

## Decisions
<!-- Every decision, when it is made, including small ones. Scope: local | project-wide (→ 12-decisions.md row) | architectural (→ ADR). -->
| # | Decision | Alternatives considered | Why | Scope |
|---|---|---|---|---|
| 1 | One branch, one build log entry and one PR for A1, A2, A3 and A16; commits typed `fix(kit)` / `chore(repo)` | Two PRs (a kit PR and a `chore(repo)` PR, as the M0-01 follow-up table suggested) | It is one owner instruction. Parallel PRs would both edit the resume point, the build log index and the CHANGELOG, so the second merge would conflict. The owner can ask for a split | local |
| 2 | Build log id `KIT-AUDIT-FIXES`, branch `task/kit-audit-fixes`, commit id `kit` / `repo` | Reuse the id `KIT` | The `KIT` entry is merged and append-only (`check-docs.sh` rejects edits); `new-log-entry.sh KIT` would only point at it | local |
| 3 | Edit protected files with the Edit/Write tools, not with `sed` or heredocs in Bash | `sed -i` in Bash (auto mode prefers Bash) | The `ask` rules in `.claude/settings.json` only fire for the Edit/Write tools; `CLAUDE.md` says not to work around them. The owner's approval is quoted under "Owner input", and every protected-file edit stays visible | local |
| 4 | Write text that mentions force flags, the override variable or a hook bypass with the Write tool until A2 is fixed | Bash heredocs | `guard-bash.sh` still had the A2 false positives; the Write tool is not affected. Confirmed in this session: the old hook denied two of my own test commands that only contained such words | local |
| 5 | A1 wording: the approved proposal sentence plus one clause saying why ("Plan Mode forbids the write-ahead's file writes, so don't use it for these tasks") | The proposal sentence alone | The audit's finding is that Plan Mode and the write-ahead conflict; a coder who reads only the first sentence could still enter Plan Mode and hit the conflict. The owner can drop the clause | local |
| 6 | A3 is applied in all three places that encode the protected list: the list in `15`, the `ask` rules in `.claude/settings.json`, the notice regex in `check-docs.sh` | Only the list in `15` | `15` says Claude Code asks before editing protected files, so the rule must match the list; the notice makes such edits visible in every PR. The `Bash(bash scripts/dev/*)` allow rule stays, because running a script is not editing it | project-wide (protected-file list is in `15`; no `12` row, it is a list change, not a design decision) |
| 7 | Leave A8 (golden fixtures missing from the notice regex) alone although the same line was edited | Fix it in passing | Not in the owner's approval; the fixtures do not exist until M0-11 (G-Scope) | local |
| 8 | Test first: write `scripts/dev/test-guard-bash.sh` (48 cases, allow and deny, on a task branch and on `main`) and run it against the old guard before changing the guard | Fix the guard, then test by hand as in M0-01 | `02` rule 15: a bug fix gets a regression test. The run reproduced A2 (6 false positives) and found 3 missed denials | local |
| 9 | Rewrite the analysis as a pipeline: decode the JSON escapes, drop here-document bodies, drop quoted text and comments, split into segments, test each segment | Only strip heredoc bodies and keep the old one-line quote stripper (the audit's minimal proposal) | The old stripper ran on the still-escaped one-line JSON string, so quotes could not span lines, and an apostrophe in a comment or heredoc text flipped its state and hid later commands (see Problems). Decoding first makes every later step line-based and correct. Same tools as before (grep, sed, awk; no jq/python), so it still runs in Git Bash | local |
| 10 | Segment separators: `&&`, `\|\|`, `;`, `\|`, `&`, `(`, `)`, `{`, `}` and backticks; redirections (`2>&1`, `>&2`, `&>`) are neutralised first | Only `&&`, `;`, `\|\|` | A subshell such as `(git push -f)` went unseen; a redirection must not split a push from its flags | local |
| 11 | Develop the new guard in the scratchpad, test it there (the test script takes the guard path as an argument), install it only when green | Edit the live hook in place | The hook runs on every Bash call; a syntax error exits 2, which blocks every Bash call, including the ones needed to repair it | local |
| 12 | Add `### Fixed` to the CHANGELOG for this task | Keep it under "Added" like M0-01 decision 18 | These are fixes, and Keep a Changelog has the heading; M0-01 avoided a heading only because it had a single line | local |
| 13 | Document the guard and its test in `18` (Claude Code guardrails), not in `AGENTS.md` | Add the command to the `AGENTS.md` list (`16` §E.5) | `AGENTS.md` is protected and the owner's approval does not cover it. One home for the fact is `18` anyway (`17` §2). Asked in Follow-ups | local |
| 14 | Do not fix the guard gaps that are not A2 (see Problems), only list them | Fix quoted flags, `git -C dir push`, `HEAD:refs/heads/main` | G-Scope; the approved A2 proposal was heredoc bodies and per-segment checks. The `ask`/`deny` rules and the GitHub ruleset on `main` remain the backstops | local |
| 15 | A15: record the owner's answer as standing directive OD-8 in `15` (no `LICENSE` file for now; README keeps "Proprietary, all rights reserved") | Only note it in this entry | It is a rule for every future coder ("don't add a `LICENSE` file or pick a license yourself"), and `15` says standing directives go in that table. Owner input is quoted above | project-wide (`15` OD-8; no `12` row, it is a standing directive) |
| 16 | Leave the README License line unchanged | Reword it | The owner asked to keep the general statement. Note for when the license is chosen: "before any public release" can be misread, because the repository is already public (OD-7); presumably it means a release of the product | local |
| 17 | A9: the whole `scripts/` directory counts as code | Only `scripts/dev/`; also `.githooks/` and `.claude/hooks/` (the other process machinery) | `scripts/dev/` is the only subfolder today, but the intent is "script changes need a log entry". The hooks were not part of the question (G-Scope); they are protected, so their edits already show up as notices in every PR. The owner can extend the list | local |
| 18 | The code-path regex stays duplicated in `check-docs.sh` and `stop-checkpoint.sh`; both edited, and the paths are now named in `17` §5 | One shared definition that both scripts read | The hook must run standalone (Git Bash, no repo scripts assumed) and two copies of one regex do not justify an abstraction (`02` rule 13). A future change must touch both places | local |
| 19 | Verify A9 before/after in throwaway clones; no new automated test for the PR rules or the Stop hook | Add test scripts like `test-guard-bash.sh` for both | Same method as M0-01 for these two scripts; two more scripts are outside the approval. This is a gap against `02` rule 15 and is listed in Follow-ups | local |

## Owner input
<!-- Approvals, answers and instructions from the owner, quoted, with where they were persisted. -->
- 2026-10-12 · "Approve A1/A2/A3 fixes, do A16". Read as: A1 and A2 exactly as proposed in the M0-01 audit table;
  A3 as "yes, `scripts/dev/**` becomes a protected path" (my reading of "approve the A3 fix", since the table's proposal was
  that decision); A16 as proposed (untrack `.DS_Store`, a `chore(repo)` change).
  Protected-file edits covered by this answer: `CLAUDE.md` (A1), `.claude/hooks/guard-bash.sh` (A2),
  `.claude/settings.json` and `core-instructions/15-change-control.md` (A3).
  Also touched under the same answer, so the owner should look at them: `scripts/dev/check-docs.sh` (a protected path from A3 on) and the
  new file `scripts/dev/test-guard-bash.sh`.
  Not covered, so not touched: `AGENTS.md`, `.github/workflows/**`, A8. (A9 and A15 were answered afterwards, see below.)
- 2026-10-12 · "I approve a9. For a15, We will decide on exact license later. For now use just general license, I think that we can use no license file for a little bit more time now"
  → **A9** approved as proposed in my reply: `scripts/` counts as code, so a script-only change needs a build log entry,
  a resume point update and a CHANGELOG line (the Stop hook and `check-docs.sh` both enforce it). Protected-file edits covered by
  this answer: `.claude/hooks/stop-checkpoint.sh` and `scripts/dev/check-docs.sh`.
  → **A15**: no LICENSE file for now; the exact license is decided later by the owner. Read "just general license" as: keep the
  README's general statement ("Proprietary, all rights reserved"). Persisted as standing directive OD-8 in `15`; `15` says the AI
  appends standing directives as soon as the owner gives them. Say if "general license" meant something else.

## Changes
- **Code:** `.claude/hooks/guard-bash.sh` (rewritten, 112 lines), `scripts/dev/check-docs.sh` (protected-file notice regex;
  Session 2: `scripts/` counts as code), `.claude/hooks/stop-checkpoint.sh` (Session 2: same), `.claude/settings.json`
  (`Edit(/scripts/dev/**)` ask rule); `.DS_Store` untracked.
- **Tests:** `scripts/dev/test-guard-bash.sh` (new): 48 cases.
- **Docs:** `CLAUDE.md` (A1), `core-instructions/15-change-control.md` (A3; OD-8 for A15), `core-instructions/17-documentation-system.md` (A9),
  `core-instructions/18-git-and-ci.md` (guard description),
  `core-instructions/13-progress.md`, `CHANGELOG.md`, this entry and its index line.

## Verification
| Command | Result |
|---|---|
| Step 1 · `git rm --cached .DS_Store` | staged as `D`; no other `.DS_Store` was tracked (`git ls-files` showed only the root one) |
| Step 1 · `ls -la .DS_Store`, `git check-ignore -v .DS_Store` | file still on disk (10244 bytes); ignored by `.gitignore:21` |
| Step 1 · `git status --short` after the commit | empty (the Finder change no longer shows up) |
| Step 3 · `python3 -I -c 'import json; json.load(open(".claude/settings.json"))'` | valid; 22 `ask` rules, `Edit(/scripts/dev/**)` present (`plutil -lint` cannot parse JSON) |
| Step 3 · `bash -n scripts/dev/check-docs.sh`, `check-docs.sh --base origin/main` after the commit | syntax ok; notices list `.claude/settings.json`, `CLAUDE.md`, `15`, `scripts/dev/check-docs.sh`; `check-docs: OK` |
| Step 4 · `bash scripts/dev/test-guard-bash.sh` on the OLD guard | **39 passed, 9 failed**: 6 false positives (push then `git switch main`; push then `rm -f`; comment, and 3 heredoc bodies mentioning a force push, the override variable, a bypass) and 3 missed denials (`(git push -f)`; push to main after a heredoc with an apostrophe; force push after a comment with an apostrophe) |
| Step 4 · same script on the new guard in the scratchpad | **48 passed, 0 failed** |
| Step 4 · `cmp` scratchpad candidate vs `.claude/hooks/guard-bash.sh`, `bash -n`, test script on the installed file | identical; syntax ok; 48 passed, 0 failed |
| Step 4 · stress script (scratchpad): 137 KB command with a 2000-line heredoc, then a normal push / a forced push | 0.10 s; allowed / denied; stderr empty (BSD awk has no record-length problem here) |
| Step 4 · 600 `echo` segments + a forced push | 0.05 s, denied |
| Step 4 · `&&` written as `&&` in the JSON; empty input; JSON without `command` | denied; allowed silently; allowed silently |
| Step 4 · live, through the real hook: a Bash call whose heredoc mentions a force push, the override variable and a bypass | allowed (before the fix it would have been denied) |
| Step 4 · live: a force push attempt written as a dry run (`--dry-run`, so nothing could be sent) | denied by the hook with the force-push message |
| Step 5 · `bash scripts/dev/check-docs.sh --base origin/main` | `check-docs: OK`; notices for the six protected paths changed: `guard-bash.sh`, `.claude/settings.json`, `CLAUDE.md`, `15`, `check-docs.sh`, `test-guard-bash.sh` (all covered by "Owner input") |
| Step 5 · `bash scripts/dev/test-guard-bash.sh` (final) | 48 passed, 0 failed |
| Session 2 · A9 · `check-docs.sh --base origin/main` in throwaway clones, old (HEAD before the edit) vs new | T1 script-only change: old `OK`, **new 3 problems** (no build log entry, `13` not updated, CHANGELOG not updated). T2 the same plus log entry, `13` and CHANGELOG: new `OK`. T3 README only: old and new `OK` |
| Session 2 · A9 · Stop hook in throwaway clones, old vs new | S1 uncommitted new script, nothing logged: old allowed, **new blocks**. S2 plus `13` and a log entry: new allows. S3 `stop_hook_active: true`: new allows. S4 uncommitted script on `main`: new blocks ("uncommitted code changes on main"). S5 README only: new allows |
| Session 2 · `bash -n` on both scripts; `bash scripts/dev/test-guard-bash.sh` | syntax ok; 48 passed (unaffected) |
| Session 2 · final `bash scripts/dev/check-docs.sh --base origin/main` | `check-docs: OK`; seven protected-file notices: `guard-bash.sh`, `stop-checkpoint.sh`, `.claude/settings.json`, `CLAUDE.md`, `15`, `check-docs.sh`, `test-guard-bash.sh` (all covered by "Owner input"; `15` for OD-8 under its own rule) |
| Environment | macOS, bash 3.2.57, BSD awk 20200816, git 2.54.0 |
| Not verified | Git Bash on Windows, Linux (gawk/mawk, GNU grep/sed): the script uses only POSIX features and `$'...'`/`<<<` of bash 3.2, but it was run on macOS only |
| Not run | Independent `reviewer` pass: not an `[O]` task and the owner did not ask for one, although the guard is a safety control (`16` §E.4 says "any task touching … security"; the owner can request it on this branch). No `dotnet`/Unity checks: nothing under `server/`, `shared/`, `client/` or `tools/` changed |
| Not applicable | Plan board row: this is not a plan task. The M0-01 row stays `In review` until M0-02 sets it to `Done` |

## Problems and surprises
- Starting state: local `main` was 3 commits behind `origin/main`. PR #4 (M0-01) and the Dependabot PRs #1 and #2 were already
  merged by the owner, so the old branch `task/M0-01-kit-audit` is obsolete (its remote branch is gone). A local copy remains.
- `.DS_Store` showed as modified at session start (Finder). It is untracked now, so the change is not committed.
- The old hook denied two of my own commands in this session because their text contained the words it looks for (A2 again);
  I moved the stress checks into a script file in the scratchpad.
- The old guard was worse than the audit said: besides the false positives it **missed** real violations when an apostrophe
  appeared in a comment or in heredoc text, because that flipped its quote state and hid everything after it (a force push, a push
  to `main`), and when a push was inside a subshell. These came out of the new tests; the rewrite fixes them.
- Gaps that remain, as before (not part of A2, decision 14): quoted flags (`git push "--force"`) are dropped with the rest of the
  quoted text; `git -C <dir> push` and `git --git-dir=… push` do not match `git push`; a refspec like `HEAD:refs/heads/main` is not
  seen as a push to `main`; text inside `$(…)` within quotes is not inspected; an arithmetic expression like `$((a<<b))` can be read
  as a here-document start and hide the lines after it. The guard is best effort; pushes to `main` and force pushes to `main`
  are also refused by GitHub (ruleset `main-protection`).

## Follow-ups
| Item | Owner / where |
|---|---|
| Add `bash scripts/dev/test-guard-bash.sh` to the `AGENTS.md` command list (protected; not covered by the approval) | Owner OK, then the next kit PR |
| Run the guard test in CI (`.github/workflows/ci.yml`, protected) | Owner decision; or via `quality.sh` in M0-62 (which "runs everything CI runs") |
| Close the guard gaps listed under Problems (quoted flags, `git -C dir push`, `HEAD:refs/heads/main`) | Owner decision (G-Scope); small follow-up task |
| Local branch `task/M0-01-kit-audit` is obsolete (squash-merged; remote branch deleted) | Owner, optional: `git branch -D task/M0-01-kit-audit` (asks for confirmation) |
| Automated tests for the PR rules in `check-docs.sh` and for the Stop hook (verified by hand in throwaway clones, decision 19) | Owner decision; natural place: `quality.sh` in M0-62 |
| Pick a license and add a `LICENSE` file (OD-8); then reword the README License line | Owner, when ready |
| Still open from the M0-01 audit: A4, A5, A7, A8, A10–A14, A18 (A9, A15 closed in Session 2) | see the M0-01 entry → Follow-ups |
