## Task
<!-- M0-15 · Title  — or CR-0007-T1 · Title -->

## Summary
<!-- 2–4 lines: what changed and why -->

## Acceptance criteria
- [ ] <!-- copy each criterion from the task spec; tick when met -->

## Definition of Done (core-instructions/11-workflow.md)
- [ ] Scope: only what the task requires (deviations approved and logged)
- [ ] Build green, zero warnings in `src/`, `dotnet format` clean
- [ ] Tests added for new behaviour; no test deleted, skipped or loosened (or approved under G-Tests)
- [ ] No game-number literals, no client authority, no era/research-specific `if`s
- [ ] Docs updated per change recipe (core-instructions/19), every Docs impact row closed; module docs show `Last updated:`
- [ ] `check-docs.sh --base origin/main` passes; the handbook builds (`bash scripts/dev/docs-site.sh build`); generated docs current (from M0-61)
- [ ] Build log entry complete (decisions, owner input, verification)
- [ ] Resume point, plan board, CHANGELOG updated
- [ ] Self-review done (line by line for [O] tasks), blocking findings fixed; any agent used was approved by the owner in advance (OD-9) and is in the build log

## Docs impact
<!-- The pages from the build log entry's Docs impact table: updated / new / deleted / no change (why) -->

## Build log entry
<!-- docs/build-log/YYYY/YYYY-MM-DD-NN-ID.md -->

## Verification
| Command | Result |
|---|---|
| | |

## Not verified / risks
<!-- e.g. Unity tests not run in this environment -->

## Owner actions
<!-- merge, [MANUAL] steps, decisions needed -->
