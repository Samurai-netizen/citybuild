# BL-20261014-02 · KIT-AGENT-RULE — Standing rule: no agents unless really necessary

- **Status:** In progress
- **Task:** KIT-AGENT-RULE — write down the owner's standing rule about agents (a process change, like `KIT-AUDIT-FIXES`: no plan row, commits use the scope `kit`)
- **Branch / PR:** `task/kit-agent-rule` (stacked on `task/M0-04-scene-and-packages`, PR #9, because both change the resume point, the changelog and the log index) / —
- **Started:** 2026-10-14 · **First agent:** Claude Code / claude-sonnet-5-5

<!-- Status values: In progress | Done | Partial | Blocked. One entry per task; every session appends a section below.
     Once merged, this file is append-only: corrections go into a new entry. -->

## Sessions

### Session 1 — 2026-10-14 · Claude Code / claude-sonnet-5-5
- **Start state:** PR #9 is still open. This branch starts from its tip (`d150875`), so the PR for this change can use `task/M0-04-scene-and-packages`
  as its base and shows only this change. Nothing else is uncommitted.
- **What the rule says, in the owner's words:** "Do not use agents until it is really nesessary, and even in that case, use only Sonnet 5.5 model or
  equivalent, and not more than 1 agent per task". It follows the owner's earlier "Don't use agents" (M0-04-FOLLOWUP, where the open question was
  whether that applies to every task from now on: it does, with this exception).
- **How I read it (the owner should confirm; see decision 2):** "agents" means subagents, Workflows and any multi-agent orchestration, also when a
  session setting such as "ultracode" asks for it; "really necessary" is judged by the coder, who logs the reason in the build log before starting
  the agent; the model limit is about the agent, not about the model that the owner picks for the main session (`11`, `CLAUDE.md`); "1 agent per task"
  counts the whole task, all sessions.
- **Plan:**
  1. Write-ahead: this entry, resume point `IN PROGRESS`; commit and push.
  2. The rule, with one home and short pointers (`17` §2): OD-9 and its rules of use in `15` (protected: the owner's instruction is the approval);
     rule 27 in `02` (loaded at every session start); the Subagents section of `CLAUDE.md` (it told the coder to run `reviewer`).
  3. Everything that now contradicts it, so the next coder is not told two things: `16` §E.4, `11` (Reviewer role, Definition of Done, model guidance),
     `17` and `18` (the `reviewer` and `docs-auditor` mentions), `/finish-task` and `/docs-sync`, the PR template, the legend of the plan's task specs,
     and `.claude/agents/reviewer.md` (`model: opus` becomes `sonnet`).
  4. Decision row W10 in `12`, CHANGELOG, resume point `IDLE`, `check-docs.sh --base origin/main`, PR (base: PR #9's branch).
- **Done:** step 1 in progress (this entry).
- **Stopped at / next:** commit `chore(kit): start the agent rule task`, push, then step 2.

## Decisions
<!-- Every decision, when it is made, including small ones. Scope: local | project-wide (→ 12-decisions.md row) | architectural (→ ADR). -->
| # | Decision | Alternatives considered | Why | Scope |
|---|---|---|---|---|
| 1 | A separate entry, branch and PR (scope `kit`), stacked on PR #9 | Add it to PR #9 (that PR is about the scene and the packages); wait for #9 to merge and branch from `main` (the owner asked for the rule now); a new branch from `main` (would conflict with #9 on the resume point, the changelog and the log index) | One change per PR (`02` rule 12); protected process files get their own review; the stack avoids the conflicts | local |

## Owner input
<!-- Approvals, answers and instructions from the owner, quoted, with where they were persisted. -->
- "Yes, I want you to write down the new rule: Do not use agents until it is really nesessary, and even in that case, use only Sonnet 5.5 model or equivalent, and not more than 1 agent per task" (2026-10-14, chat; the answer to the question in the M0-04-FOLLOWUP resume point). It is a standing directive: `15` OD-9, with `02` rule 27 and decision row W10. Editing the protected files `02`, `15` and `CLAUDE.md` for it is covered by this instruction.

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
