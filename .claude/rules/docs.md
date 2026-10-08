---
paths:
  - "docs/**"
  - "core-instructions/**"
  - "*.md"
---

# Documentation rules

System: `core-instructions/17-documentation-system.md`. Short form:
- Every fact has one home (`17` §2). Link to it; don't copy it.
- `core-instructions/` = what must be true (rules, design, contracts). `docs/` = what exists, how to use it, what happened.
- Build log entries (`docs/build-log/<YYYY>/…`) are append-only once merged. Correct them with a new entry.
  Keep `docs/build-log/README.md` listing every entry.
- Never edit `docs/reference/generated/**`; change the generator in `tools/DocTools`.
- Module docs follow `docs/modules/TEMPLATE.md` and end with `Last updated: <task id>`.
- CRs and ADRs keep their `- **Status:** …` line valid (`check-docs.sh` checks it).
- Mermaid for diagrams. Glossary words (`31`). Mark unbuilt things as *(planned, Mx)*.
- Keep files under ~150 lines; split instead of growing. `13-progress.md` ≤ 80 lines.
- Protected files (`15`) need the owner's approval for every edit.
