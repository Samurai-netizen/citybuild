# Contributing

This repository is built by AI coders under the owner's direction. Humans follow the same process.
The rules live in `core-instructions/`; this page is the short version for people.

## The owner's loop
1. **Pick the next task** from the board in `docs/plan/M0-PLAN.md` (or an approved CR).
2. **Start an AI session** in the repo:
   - Claude Code: `/clear` → `/model` (Opus 5.5 for `[O]`, Sonnet 5.5 for `[S]`) → `/start-task M0-NN`.
   - Other tools: "Read AGENTS.md, then start task M0-NN following core-instructions/16-session-protocol.md."
3. **Approve plans** for `[PLAN]` tasks, answer questions, and do the `[MANUAL]` steps you're asked for.
4. **Review the PR** on GitHub: CI green, task report, build log entry, diff. Then **Squash and merge**.
5. **If a session ends early** (tokens ran out), start a new one with any tool and say `/resume` (or "resume").
   The resume point and build log tell it where to continue.

## New ideas, design changes, bugs
Don't ask the AI to "just add" something. Ask for a change request (`/change-request <idea>`). Read the CR, then
approve it (set `Status: Approved`, or say "approve CR-NNNN"), or reject it. Only approved CRs get built (`core-instructions/15`).

## Rules everyone follows
- Work on a `task/<ID>-<slug>` branch, never on `main`. Commit subjects look like `feat(M0-15): …` (the hook checks this).
- Every change comes with tests, docs per `core-instructions/19-change-recipes.md`, and a build log entry with every decision.
- Don't bypass hooks or CI. Don't commit secrets (`.env` and `*.local.json` are refused by the pre-commit hook).
- Owner-only: merging PRs, approving CRs and plans, editing protected files (`core-instructions/15`), and `ALLOW_MAIN_COMMIT=1` for repo maintenance.

## Where to look
`docs/README.md` maps all documentation. `AGENTS.md` is the AI entry point. `core-instructions/README.md` indexes all specs.
