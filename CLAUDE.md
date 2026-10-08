@AGENTS.md
@core-instructions/02-principles.md
@core-instructions/13-progress.md

# Claude Code specifics

The files above are loaded at session start. `AGENTS.md` is the shared entry point for every AI tool.
This section only adds what's specific to Claude Code.

## Skills (workflow commands)
| Skill | Use |
|---|---|
| `/resume` | Orient at session start or after an interruption; read-only report and proposed next step |
| `/start-task <ID>` | Begin a plan task or CR task (preconditions, branch, build log, resume point) |
| `/checkpoint` | After every green step and before pausing or asking a question |
| `/finish-task` | Definition of Done, docs, review, PR |
| `/change-request <idea>` | Turn an owner idea into a CR file (Proposed); no code |
| `/docs-sync` | Audit docs against code and fix drift |

## Subagents
- `reviewer`: independent, read-only review of the task branch. Run it in `/finish-task` for `[O]` tasks.
- `docs-auditor`: read-only check of docs vs code. Used by `/docs-sync` and at milestone ends.

## Guardrails in `.claude/settings.json` (don't work around them)
- **Hooks:**
  - session start prints the repo state;
  - the Bash guard blocks `--no-verify`, commits on `main` and pushes to `main`;
  - the Stop hook blocks ending a turn while code changes aren't checkpointed.
  If the Stop hook blocks you, checkpoint (`/checkpoint`). Don't argue with it.
- **Permissions:**
  - deny force-push, push to `main`, hand edits of generated docs, and reading `.env`;
  - ask before destructive git commands and edits to protected files (`15`).

## Model and mode
- `[O]` tasks: `/model` → Opus 5.5. `[S]` tasks: Sonnet 5.5. Name the model in the build log session header.
- `[PLAN]` tasks: work in Plan Mode until the owner approves the plan.

## Memory
Don't rely on auto memory or on earlier conversations. Other AI tools will continue this work.
Anything worth remembering goes into the repo (build log, resume point, decisions, standing directives).

## Path-scoped rules
`.claude/rules/` holds rules for `server/`, `shared/`, `client/`, tests and docs. They load automatically when you touch those files.
