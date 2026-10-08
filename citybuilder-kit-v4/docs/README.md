# Documentation Map

Everything about this project is written down, for people and for AI coders. Start with the row that matches your question.

| I want to… | Read |
|---|---|
| understand the game | `core-instructions/22-game-mechanics.md` (pillars + mechanics), `21-game-vision.md`, `25-eras-and-research.md` |
| see where the project stands right now | `core-instructions/13-progress.md` (resume point), `docs/plan/M0-PLAN.md` (task board) |
| set up and run the project | [getting-started.md](getting-started.md) |
| understand the architecture as built | [architecture/overview.md](architecture/overview.md), [modules/](modules/README.md) |
| understand the intended architecture and rules | `core-instructions/README.md` (index of all specs) |
| know why something is the way it is | [build-log/](build-log/README.md) (every decision), [adr/](adr/README.md), `core-instructions/12-decisions.md` |
| propose a new feature | [changes/](changes/README.md) (change requests) |
| make a common change | `core-instructions/19-change-recipes.md`, [how-to/](how-to/README.md) |
| look up API, errors, content, config, schema | [reference/](reference/README.md) |
| work on this repo as an AI coder | `AGENTS.md` (entry point), `core-instructions/16-session-protocol.md` |
| work on this repo as a person | `CONTRIBUTING.md` |

## Normative vs descriptive
- `core-instructions/` says what **must** be true (rules, design, contracts).
- `docs/` says what **is** (the system as built, how to use it, what happened and why).
- When they disagree, one of them is wrong. Fix it in the same PR (`core-instructions/17-documentation-system.md`).

## Folders
| Folder | Contents | Written by |
|---|---|---|
| `plan/` | Milestone plans with task specs and the task board | owner + AI (board updates) |
| `build-log/` | One entry per task: sessions, decisions, verification | AI coders, append-only |
| `changes/` | Change requests (CRs) | AI drafts, owner approves |
| `adr/` | Architecture decision records | AI, accepted with the owner |
| `architecture/` | The system as built, with diagrams | AI, kept current |
| `modules/` | One doc per project or assembly | AI, kept current (enforced) |
| `how-to/` | Worked guides for common changes | AI, kept current |
| `reference/` | Generated reference (from M0-61) | `tools/DocTools` |
| `testing/`, `security/`, `balance/` | Test checklists, security report, balance reports | AI, per task |
