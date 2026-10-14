# BL-20261014-02 · KIT-AGENT-APPROVAL — Standing rule: ask the owner before using agents

- **Status:** Done
- **Task:** KIT-AGENT-APPROVAL — write down the owner's standing rule about agents (a process change like `KIT-AUDIT-FIXES`: no plan row, commits use the scope `kit`)
- **Branch / PR:** `task/kit-agent-approval` / PR #11
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
- **Done:** step 1 (write-ahead, pushed). Step 2: OD-9 and "OD-9 in practice" (the proposal format) in `15`, rule 27 under a new heading "Agents"
  in `02` (appended, so no rule number that other files cite changes), the "Subagents and Workflows" section of `CLAUDE.md`; the Edit tool was used
  for every protected file, so the permission rules applied. Step 3: `16` §E.4, `11` (Reviewer role, Definition of Done, model guidance), `17` (two
  places), `18`, `/finish-task` step 5, `/docs-sync` step 2, the PR template, the legend of the plan's task specs, `reviewer.md` (`model: sonnet`,
  description) and `docs-auditor.md` (description). `git grep` for "subagent" and "reviewer" outside the build logs, the plan's history and the code
  finds only the agent definitions and text that points to OD-9. Step 4: `"Agent"` and `"Workflow"` in `permissions.ask` of
  `.claude/settings.json` (valid JSON; 2 lines added). The Claude Code permissions page says that a bare tool name matches every use of the tool and
  that `Agent` is the tool name of subagent calls (`Agent(model:opus)` is an example there). Step 5: decision row W10, CHANGELOG, this entry,
  resume point `IDLE`, PR. No agent and no Workflow was used in this task.
- **Stopped at / next:** the owner merges the PR. No code, no tests: process documents and one settings file only.

## Decisions
<!-- Every decision, when it is made, including small ones. Scope: local | project-wide (→ 12-decisions.md row) | architectural (→ ADR). -->
| # | Decision | Alternatives considered | Why | Scope |
|---|---|---|---|---|
| 1 | A new entry, branch and PR (scope `kit`), from `origin/main`, instead of reopening PR #10 or continuing its branch | Reopen PR #10 and rewrite it; new PR from the old branch | The owner's choice ("Fresh branch from main"); the old text and its merge commit stay out of `main` | local |
| 2 | **How I read the instruction** (the owner can correct it): (a) "agents" = subagents (the `Agent` tool), Workflows and any other multi-agent run, also when a session setting such as "ultracode" asks for one; that setting is never an approval; (b) the owner approves every time and silence is not approval; an approval covers exactly the agents, number, model and purpose in the option picked, anything more is a new proposal; (c) a proposal has at least A no agents, B small, C big, each with why, how many and why that many, the model, what it covers and what it cannot catch, plus a recommendation (these fields come from the owner's wording: "why you need to use agents, how much of them and why", "a couple of proposals ... no agents, small use ..., big use ..."); (d) the channel is a chat question (`AskUserQuestion` in Claude Code) and the proposal and answer are logged before the agent starts; (e) the rule limits agents only, not the model of the main session | Fixed answers such as "max 1 agent"; letting the permission prompt alone serve as the approval | The owner wants the decision each time, with the options explained | local |
| 3 | The model is named per proposal (the owner's answer). `reviewer` and `docs-auditor` keep `model: sonnet` as their default (`reviewer` was `opus`); a proposal may name another model and the call can pass it | Keep `opus` for `reviewer`; remove the default | Sonnet is what I would propose first; the `Agent` tool takes a `model` parameter per call, so the file default does not limit the owner's choice | local |
| 4 | `permissions.ask` gets `"Agent"` and `"Workflow"` (owner's answer: yes). Limits: **not tested live**, because a test needs an agent start, which this very rule forbids without approval; the next approved agent start will show whether the prompt appears; a bare rule asks for every use, also for one the owner already approved in chat (a second confirmation, accepted); continuing an approved agent with `SendMessage` is not covered; a Claude Code version that still calls the tool `Task` would need that name added | `Agent(model:opus)`-style rules (would not cover Sonnet agents, which the proposals will mostly use); not enforcing | The documented bare-name form matches every use of a tool; the owner asked for the check | project-wide (row W10) |
| 5 | Nothing of the earlier version reaches `main`: PR #10 ("no agents unless really necessary, one Sonnet agent per task") stays closed; the branch `task/kit-agent-rule` and its remote copy are left as they are (deleting them is the owner's call). The merged M0-04-FOLLOWUP entry still says the question was open and "no subagent or Workflow is used for the rest of this task": that is history, and this entry answers it | Delete the old branch | A deletion nobody asked for | local |

## Owner input
<!-- Approvals, answers and instructions from the owner, quoted, with where they were persisted. -->
- Everything quoted under "History of this rule" and "The rule, in the owner's words" above (2026-10-14, chat). It is a standing directive: `15` OD-9, `02` rule 27, decision row W10. Editing the protected files `02`, `15`, `CLAUDE.md` and `.claude/settings.json` for it is covered by this instruction.

## Changes
- **Code:** none. `.claude/settings.json` (two `ask` rules), `.claude/agents/reviewer.md` (`model: sonnet`, description), `.claude/agents/docs-auditor.md` (description),
  `/finish-task` and `/docs-sync`.
- **Tests:** none (process documents and one settings file).
- **Docs:** `15` (OD-9 and its rules of use), `02` (rule 27), `CLAUDE.md`, `11`, `12` (row W10), `16`, `17`, `18`, the PR template, the legend of the plan's task specs, CHANGELOG, resume point, this entry.

## Verification
| Command | Result |
|---|---|
| `python3 -I -c "import json; json.load(open('.claude/settings.json'))"` | valid JSON; `Agent` and `Workflow` are in `permissions.ask` |
| Claude Code permissions page (code.claude.com/docs/en/permissions) | a bare tool name matches all uses of the tool; `Agent` is the tool name (`Agent(model:opus)` appears as an example); the live prompt was not tested (decision 4) |
| `bash scripts/dev/check-docs.sh --base origin/main` | OK (it lists the protected files that changed; the owner's instruction is recorded under Owner input) |
| `git grep -n -i -E "subagent|reviewer"` outside the build logs, the code and the plan's history | only the agent definitions, the rule and text that points to it |
| always-loaded file limits | `02` and `CLAUDE.md` well under their limits, `13-progress.md` ≤ 80 |

## Problems and surprises
- —

## Follow-ups
| Item | Owner / where |
|---|---|
| Watch the first real agent proposal: does the permission prompt appear for `Agent` / `Workflow` (decision 4)? If not, tell me and I will look at the rule | Owner, at the first proposal |
| M0-06 and M0-63 name the `reviewer` and `docs-auditor` agents in their specs; the plan's legend says OD-9 applies, the tasks are unchanged | M0-06, M0-63 |
| The old branch `task/kit-agent-rule` (PR #10, closed) can be deleted on GitHub | Owner |
