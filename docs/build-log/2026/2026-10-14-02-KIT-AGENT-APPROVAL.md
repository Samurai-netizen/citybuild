# BL-20261014-02 · KIT-AGENT-APPROVAL — Standing rule: ask the owner before using agents

- **Status:** In progress
- **Task:** KIT-AGENT-APPROVAL — write down the owner's standing rule about agents (a process change like `KIT-AUDIT-FIXES`: no plan row, commits use the scope `kit`)
- **Branch / PR:** `task/kit-agent-approval` / —
- **Started:** 2026-10-14 · **First agent:** Claude Code / claude-sonnet-5-5

<!-- Status values: In progress | Done | Partial | Blocked. One entry per task; every session appends a section below.
     Once merged, this file is append-only: corrections go into a new entry. -->

## Sessions

### Session 1 — 2026-10-14 · Claude Code / claude-sonnet-5-5
- **History of this rule (all in this chat, 2026-10-14):** (1) "Don't use agents" (during M0-04-FOLLOWUP; I stopped my review Workflow; that
  entry records it for that task only). (2) "Do not use agents until it is really nesessary, and even in that case, use only Sonnet 5.5 model or
  equivalent, and not more than 1 agent per task": written down on `task/kit-agent-rule` as PR #10 (KIT-AGENT-RULE). (3) The owner closed PR #10
  and changed their mind (below). Nothing of (2) reached `main`; the branch `task/kit-agent-rule` and its remote copy stay as they are.
- **Start state:** `origin/main` is `16ae20a` (PR #9 merged); PR #10 is closed and unmerged; this branch starts from `origin/main`; the tree is clean.
- **The rule, in the owner's words (chat, after closing PR #10):** "I want you to ask me each time when you want to use agents. You should specify
  why you need to use agents, how much of them and why. Make a couple of proposals and explain each option: no agents, small use of agents, big use
  of agents and so on. I will approve the plan each time". The owner's answers to three questions with options: the model of an approved agent is
  named in each proposal (Sonnet 5.5 is the default I propose); a fresh branch from `main`; a mechanical check as well: `ask` permission rules for
  the `Agent` and `Workflow` tools in `.claude/settings.json`.
- **Plan:**
  1. Write-ahead: this entry, resume point `IN PROGRESS`; commit and push.
  2. The rule, with one home and short pointers (`17` §2): OD-9 and its rules of use (what a proposal contains) in `15`; rule 27 in `02`; the
     Subagents section of `CLAUDE.md`. Protected files: edited with the Edit tool, so the permission rules apply; the owner's instruction covers them.
  3. Everything that told the coder to run an agent by itself: `16` §E.4, `11` (Reviewer role, Definition of Done, model guidance), `17`, `18`,
     `/finish-task`, `/docs-sync`, the PR template, the legend of the plan's task specs, and the two agent definitions.
  4. Enforcement: `ask` rules for `Agent` and `Workflow` in `.claude/settings.json` (protected).
  5. Decision row W10 in `12`, CHANGELOG, resume point `IDLE`, `check-docs.sh --base origin/main`, PR. No agent is used in this task.
- **Done:** step 1 in progress (this entry).
- **Stopped at / next:** commit `chore(kit): start the agent approval rule task`, push, then step 2.

## Decisions
<!-- Every decision, when it is made, including small ones. Scope: local | project-wide (→ 12-decisions.md row) | architectural (→ ADR). -->
| # | Decision | Alternatives considered | Why | Scope |
|---|---|---|---|---|
| 1 | A new entry, branch and PR (scope `kit`), from `origin/main`, instead of reopening PR #10 or continuing its branch | Reopen PR #10 and rewrite it; new PR from the old branch | The owner's choice ("Fresh branch from main"); the old text and its merge commit stay out of `main` | local |

## Owner input
<!-- Approvals, answers and instructions from the owner, quoted, with where they were persisted. -->
- Everything quoted under "History of this rule" and "The rule, in the owner's words" above (2026-10-14, chat). It is a standing directive: `15` OD-9, `02` rule 27, decision row W10. Editing the protected files `02`, `15`, `CLAUDE.md` and `.claude/settings.json` for it is covered by this instruction.

## Changes
- **Code:** —
- **Tests:** —
- **Docs:** —

## Verification
| Command | Result |
|---|---|
| | |

## Problems and surprises
- —

## Follow-ups
- —
