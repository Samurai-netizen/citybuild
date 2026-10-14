# BL-20261014-02 · KIT-AGENT-RULE — Standing rule: no agents unless really necessary

- **Status:** Done
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
- **Done:** step 1 (write-ahead, pushed). Step 2: OD-9 and "OD-9 in practice" in `15`, rule 27 under a new heading "Agents" in `02` (48 lines, limit
  70; appended at the end so no rule number that other files cite changes), the "Subagents and Workflows" section of `CLAUDE.md` (46 lines, limit 80);
  the Edit tool was used for the three protected files, so the permission rules applied. Step 3: `16` §E.4, `11` (Reviewer role, Definition of Done,
  model guidance), `17` (two places), `18` (merge condition), `/finish-task` step 5, `/docs-sync` step 2, the PR template, the legend of the plan's task
  specs, `reviewer.md` (`model: sonnet`, description) and `docs-auditor.md` (description). `git grep` for "subagent" and "reviewer" finds only
  the agent definitions themselves and text that points to OD-9. Step 4: decision row W10, the CHANGELOG line, `check-docs.sh --base origin/main`, PR.
- **Stopped at / next:** the owner merges PR #9 and then this PR (or this one into #9's branch first). No code, no tests: process documents only.

## Decisions
<!-- Every decision, when it is made, including small ones. Scope: local | project-wide (→ 12-decisions.md row) | architectural (→ ADR). -->
| # | Decision | Alternatives considered | Why | Scope |
|---|---|---|---|---|
| 1 | A separate entry, branch and PR (scope `kit`), stacked on PR #9 | Add it to PR #9 (that PR is about the scene and the packages); wait for #9 to merge and branch from `main` (the owner asked for the rule now); a new branch from `main` (would conflict with #9 on the resume point, the changelog and the log index) | One change per PR (`02` rule 12); protected process files get their own review; the stack avoids the conflicts | local |
| 2 | **How the instruction is read** (the owner can correct any of these): (a) "agents" = subagents, Workflows and any multi-agent run, also when a session setting such as "ultracode" asks for one; (b) the limit "Sonnet 5.5 or equivalent" is about the agent, not about the model that the owner picks for the main session (`11`, `CLAUDE.md` keep Opus 5.5 for `[O]` tasks); (c) "1 agent per task" counts the whole task, all sessions, and a Workflow of several agents is never allowed; (d) the coder decides what "really necessary" is; the owner asking for an agent always qualifies | Treat "Sonnet 5.5" as the limit for the main session as well (the owner chose that model for this session by hand, but `11` makes `[O]` tasks Opus on purpose); let the owner judge each case (blocks the work) | The text says "use only Sonnet 5.5 model or equivalent" right after "agents"; it is the smallest reading that keeps every other rule intact | local |
| 3 | Two additions that the owner did not ask for, both small: the reason for an agent is written in the build log **before** it starts and named in the task report (it makes "really necessary" checkable and costs one line); the owner starting a fresh session is named as the independent-review option (it already was in `16` as "ask a fresh session to review") | No logging rule | Rule 19 of `02` says every decision is logged; an agent is a decision with a cost | local |
| 4 | `.claude/agents/reviewer.md` changes from `model: opus` to `model: sonnet`; both agent descriptions say "use only when really necessary" | Delete the agent files; leave `opus` and rely on the rule | The files stay for the rare permitted case and must not contradict the rule; the old `opus` setting would make the one allowed agent a violation | local |
| 5 | The rule is written down, not enforced by a hook or a permission rule (for example `ask` on the `Agent` and `Workflow` tools in `.claude/settings.json`, which would make each start the owner's call) | Add the permission rule now | The owner asked to write the rule down; a settings change is protected and has side effects on every session. Offered in the report | local |

## Owner input
<!-- Approvals, answers and instructions from the owner, quoted, with where they were persisted. -->
- "Yes, I want you to write down the new rule: Do not use agents until it is really nesessary, and even in that case, use only Sonnet 5.5 model or equivalent, and not more than 1 agent per task" (2026-10-14, chat; the answer to the question in the M0-04-FOLLOWUP resume point). It is a standing directive: `15` OD-9, with `02` rule 27 and decision row W10. Editing the protected files `02`, `15` and `CLAUDE.md` for it is covered by this instruction.

## Changes
- **Code:** none. `.claude/agents/reviewer.md` (`model: sonnet`), the descriptions of both agent files, `/finish-task` and `/docs-sync`.
- **Tests:** none (process documents only).
- **Docs:** `15` (OD-9 and its rules of use), `02` (rule 27), `CLAUDE.md`, `11`, `12` (row W10), `16`, `17`, `18`, the PR template, the legend of the plan's task specs, CHANGELOG, resume point, this entry.

## Verification
| Command | Result |
|---|---|
| `bash scripts/dev/check-docs.sh --base origin/main` | OK |
| `git grep -n -i -E "subagent|reviewer"` outside the build logs, the code and the plan's history | only the agent definitions, the new rule and text that points to it |
| line limits of the always-loaded files | `02` 48/70, `CLAUDE.md` 46/80, `AGENTS.md` 82/150, `13-progress.md` ≤ 80 |

## Problems and surprises
- The build log entries and the plan's history still say "reviewer subagent" where they describe what happened (M0-03 used a Workflow of reviewers, M0-04 a Workflow of 9 agents): that is history, not the rule, and merged entries are append-only. The owner's "Don't use agents" came after M0-04's Workflow; the M0-04 follow-up stopped its own Workflow.

## Follow-ups
| Item | Owner / where |
|---|---|
| Confirm the reading in decision 2 (agents only; whole-task count; coder decides "really necessary") or correct it | Owner |
| Optional: enforce the rule mechanically, for example `ask` permission rules for the `Agent` and `Workflow` tools in `.claude/settings.json` (protected), so that every start needs the owner's yes | Owner decides |
| M0-06 and M0-63 name the `reviewer` and `docs-auditor` agents in their specs: the legend in the plan says OD-9 applies; the tasks themselves are unchanged | M0-06, M0-63 |
