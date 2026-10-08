---
name: docs-sync
description: Audit documentation against the code and fix drift - module docs, architecture overview, how-tos, specs, generated reference. Use at milestone ends, in M0-63, or when docs seem stale.
---

## Do this (`core-instructions/17-documentation-system.md`)
1. Run `bash scripts/dev/check-docs.sh`. Once DocTools exists, also run `dotnet run --project tools/DocTools -- check`.
2. Use the `docs-auditor` subagent to compare `docs/` and `core-instructions/` with the code. It reports; it doesn't edit.
3. Fix each confirmed finding in the doc that owns the fact (`17` §2). If the **code** is wrong and the spec right,
   don't change the code here: log it as a follow-up or a CR.
4. Regenerate generated docs. Update `Last updated:` lines.
5. Record what changed in the current task's build log entry (or a `docs/` task if the owner started one), then `/checkpoint`.
