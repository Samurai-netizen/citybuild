# Git, GitHub and CI

The repository on GitHub is the single place where all work and all context live.

## Branches
- `main` is protected (ruleset `main-protection`): always green and always releasable. Nobody pushes to it directly. Changes arrive only through PRs.
- One branch per task: `task/<ID>-<short-slug>`, e.g. `task/M0-15-invariant-properties` or `task/CR-0007-T1-cancel-research`.
- `rescue/<date>-<ID>` holds unexplained leftovers found during recovery (`16` §F).
- Start every task from an up-to-date `main`. Stacking a branch on an unmerged PR needs the owner's OK.

## Commits
- Format (checked by `.githooks/commit-msg`): `<type>(<ID>): <summary>`
  - `type` ∈ `feat fix test docs refactor perf build ci chore wip`; `ID` = task or CR id (`M0-15`, `CR-0007`, `CR-0007-T1`)
  - the owner may use `repo` or `kit` as the ID for maintenance
  - example: `feat(M0-32): keep labor progress when workers are reassigned`
- Each commit leaves the build green, except `wip(...)` checkpoint commits on task branches, which say what's broken in the body.
- Push at every checkpoint (`16` §D). Unpushed work is lost if the next coder runs elsewhere.
- Never: `--no-verify`, force-push, amending or rebasing pushed commits, committing secrets, or committing on `main`.

## Pull requests
- One PR per task, opened by the AI at the end of `/finish-task` with `gh pr create`.
  The title is the commit format (`feat(M0-15): …`), and the body fills `.github/pull_request_template.md`.
- To be mergeable, the PR needs: CI green, `reviewer` findings resolved (for `[O]` tasks), and the owner's approval.
- **The owner merges** with "Squash and merge" and deletes the branch. The AI never merges.
  The build log keeps the step-by-step history that squashing removes from `main`.

## Git hooks (any tool, local)
Run `bash scripts/dev/setup-hooks.sh` once per clone. It sets `core.hooksPath=.githooks`.
- `pre-commit`: blocks commits on `main` (the owner can override with `ALLOW_MAIN_COMMIT=1` for repo maintenance)
  and runs `scripts/dev/check-docs.sh`.
- `commit-msg`: enforces the commit format.
AI coders must never bypass hooks or set `ALLOW_MAIN_COMMIT`.

## Claude Code guardrails (`.claude/settings.json`)
- **Deny:** force-push, pushing to `main`, hand-editing generated docs, reading `.env`.
- **Ask:** destructive git (`reset --hard`, `clean`, `rebase`, `merge`), `gh pr merge`, `rm -rf`, dropping databases or volumes, and edits to protected files.
- **Hooks:**
  - session start prints the repo state;
  - a Bash guard blocks `--no-verify`, commits on `main` and pushes to `main`;
  - a Stop hook refuses to end a turn while code changes are not checkpointed in the resume point and build log.

## CI (`.github/workflows/ci.yml`, runs on every PR and on `main`)
| Job | Steps | Active from |
|---|---|---|
| `docs` | `check-docs.sh` (+ PR checks against the base branch) | Day 0 |
| `server` | restore (locked) → `dotnet format --verify-no-changes` → build → fast tests → integration tests (Docker) | M0-02 (skips until `server/CityBuilder.slnx` exists) |
| `docs-generated` | `dotnet run --project tools/DocTools -- check` | M0-61 (skips until DocTools exists) |
Unity tests are not in CI (they need a Unity license on the runner). They run locally via `scripts/dev/test-unity.sh`
and their results go in the build log. Revisit in M3.

## GitHub settings (Day 0; applied by M0-01 via `gh api`)
- Ruleset `main-protection` on the default branch (Settings → Rules → Rulesets):
  - require a pull request before merging (0 approvals, because the owner's account opens the AI's PRs), squash only;
  - require status checks `docs` and `server` to pass;
  - block force pushes and deletions; require linear history.
  - No bypass actors, so even the owner changes `main` through a PR. `ALLOW_MAIN_COMMIT` only lifts the local hook.
- Settings → General → Pull Requests: squash merging only (title = PR title); automatically delete head branches.
- Security: secret scanning with push protection, Dependabot alerts and security updates.
- The repository is **public** (owner directive OD-7 in `15`). Never commit secrets, tokens or personal data;
  local configuration stays in ignored files (`.env`, `*.local.json`).
- Check the current state: `gh api repos/{owner}/{repo}/rules/branches/main` and `gh api repos/{owner}/{repo}`.

## Line endings and large files
`.gitattributes` normalizes text to LF (`.ps1` and `.bat` stay CRLF) and marks binaries. Unity scenes and assets are
text (Force Text). Large art and audio will use Git LFS from M1 (decision when the first real art arrives).
