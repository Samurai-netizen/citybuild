# CR-0010 — Workflow v2: tests first, focused test runs, a token budget, asset requests

- **Status:** In progress
- **Type:** Process
- **Requested by:** owner · **Date:** 2026-11-16
- **Milestone / era:** M0 (from M0-67) and every later milestone / —
- **Conflicts with pillars (`22`) or the era rule (`25`):** none

<!-- Status values: Proposed | Approved | In progress | Done | Rejected | Superseded.
     Only the owner moves a CR to Approved. No code before that. -->

## Problem / goal
The M0 workflow made the work resumable and traceable, at a price. A task of about an hour carried a full ceremony; a checkpoint
(resume point, build log, commit, push) followed every step; build log entries grew to 30–130 KB (1.5 MB for 86 tasks), most of it
written by the model; the full suite ran at every checkpoint; the resume point, loaded into every session, carried a 2 KB list of old
questions; and the AI coder also wrote the Blender scripts that make the art. Tests were first only for engine rules.

The owner wants bigger tasks (CR-0009) and a workflow built for them: smart use of tokens, tests before code, only the tests of the
part being written with occasional full runs, and no assets made by the coder: a list of the assets each next task needs, for another
AI model to generate.

## Proposal
1. **Units:** a task (one PR) is made of **parts** (one context each), a part of **steps** (one red → green cycle).
2. **Tests first, everywhere a test can exist** (OD-20): the task spec lists the behaviours; the test is written and seen failing
   before its code; pure looks get a `[MANUAL]` line instead. The red state is saved in git (`test(<ID>): red, …`).
3. **Test tiers** (OD-21): T0 the focused class in the loop · T1 the touched areas at a checkpoint · T2 everything once at the finish
   and at the points a spec names · T3 CI. A new script, `scripts/dev/test.sh <area> [filter]`, prints one line per area and only the
   failing tests.
4. **Save and checkpoint** are two things: a save is a commit and a push after every green step; a checkpoint (area tests, resume
   point, build log) ends a part.
5. **A token budget** (OD-19): read by section; run quietly; a build log part is five lines and a decision one row, with a definition
   of what a decision is; reports of about 40 lines; one part per context; the baseline is trusted when `HEAD` is a merged commit;
   questions to the owner are batched; old owner questions move from the resume point to `docs/owner-queue.md`.
6. **Assets** (OD-22): a new spec, `core-instructions/27-asset-pipeline.md`. AI coders make no models, pictures, audio or fonts. A
   task's assets are rows of an **asset request** (`docs/assets/requests/AR-<ID>.md`), written one task ahead, so the owner can have
   them generated in the meantime; code runs on placeholders made in code; the coder checks a delivery against its spec and never
   repairs an asset. A new skill, `/asset-request <ID>`.

## Acceptance criteria
- [x] `16` describes tasks, parts and steps, the test-first loop, save against checkpoint, and the token budget.
- [x] `08` has the test-first loop with the first test per kind of work, and the test tiers with when an early full run is worth it.
- [x] `scripts/dev/test.sh` runs one area or the changed areas, prints one line per area on success and only the failing tests or
      the compile errors on failure (tried by hand for each outcome: pass, fail, compile error, no test matched, wrong usage).
- [x] `27`, `docs/assets/README.md`, `docs/assets/TEMPLATE.md` and the `/asset-request` skill exist.
- [x] `02`, `11`, `14`, `15` (OD-19 … OD-22), `17` §4, `AGENTS.md`, `CLAUDE.md`, the build log template, the skills and
      `.claude/rules/tests.md` agree with the above.
- [x] The always-loaded files stay inside their limits; the resume point's old questions are in `docs/owner-queue.md`.
- [x] `check-docs.sh` passes and the handbook builds.

## Out of scope
- `check-assets.sh` (the delivery checker): M1-09 builds it, when the first requests exist.
- Changes to the hooks, the permissions and CI: none were needed. The Stop hook already lets a turn end after a save.
- A filter option for `test-unity.sh`.
- Rewriting the remaining M0 task specs into parts with test lists: the regrouping is proposed in `docs/plan/M1-PLAN.md` §3.

## Impact analysis
| Area | Impact | Files / docs |
|---|---|---|
| Engine (`04`) | none | |
| Content (`23`, JSON) | none | |
| API (`05`) | none | |
| Data / migration (`06`) | none | |
| Security (`07`) | none | |
| Client (`09`) | none now; from M1 every asset loads with a placeholder fallback (`27`) | |
| Docs (recipes in `19`) | `02`, `08`, `11`, `14`, `15`, `16`, `17`, new `27`, `docs/assets/`, `docs/owner-queue.md`, `AGENTS.md`, `CLAUDE.md`, the skills, the build log template, the handbook sidebar | this CR's task |
| Tests (`08`) | no test changed; a new script for running them | `scripts/dev/test.sh` |

## What changes for earlier owner directives
- **OD-4 (every decision logged) stays.** New: `17` §4 says what a decision is (a choice the next coder or the owner could have made
  differently and needs to know). A local name or the order of two lines is no longer logged. Say so if every small one should return.
- **OD-5, OD-11, OD-12 (docs with every task, every declaration documented) stay** as they are.
- **OD-6 (the repo is the only memory) stays:** saves are pushed after every step, more often than checkpoints were.
- **The art made by AI coders (CR-0005) stays**; `art/blender/` is frozen, and new or changed art is an asset request.

## Risks and alternatives
- A session that dies between a save and a checkpoint leaves no log line for the last steps: the commit subjects carry them, and `/resume`
  reads the last commits.
- Shorter logs lose detail somebody may want later: the code, the tests and the commits hold what was done; the log keeps why.
- Fewer full runs let a cross-area break live until the finish: CI is the net, and a spec's "Full run" line marks the risky parts.
- Generated assets may not match each other: one style page, the owner's eye in a `[MANUAL]` step, and reasons on rejected rows.

## Tasks (filled in after approval)
| ID | Title | Model | Status |
|---|---|---|---|
| CR-0010-T1 | Workflow v2 in the kit: specs, skills, `test.sh`, the asset pipeline | O | In review |

## Owner approval
- Approved on: 2026-11-16 — the owner's request in chat: "Also fully rework development workflow to take into consideration smart token
  usage, smart test implementation (tests before code), (do not spam with full test suite after each change, test only part you are
  currently writing, with occasional full test runs where needed), (don't make game assets by yourself. make a list of needed assets for
  each next task when needed (other ai model will generate it)) and so on."
