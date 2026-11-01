# Change Requests (CRs)

Every feature, design change, process change or bug that isn't part of the current plan task enters the project
as a CR. That's how the owner keeps control over what gets built (`core-instructions/15-change-control.md`).

**Flow:**
1. The owner has an idea.
2. The AI writes `CR-NNNN-<slug>.md` from `TEMPLATE.md` (status Proposed, no code). Claude Code: `/change-request <idea>`.
3. The owner approves (status Approved) or rejects it.
4. The AI adds tasks `CR-NNNN-T1…` inside the CR.
5. Each task runs through the session protocol (`/start-task CR-NNNN-T1`).
6. When every task is merged, the CR is Done.

`check-docs.sh` requires every CR file to be listed here and to have a valid status line.

## Index
| CR | Title | Type | Status |
|---|---|---|---|
| [CR-0001](CR-0001-start-pass-repeats.md) | A start frees cap room: the start pass repeats until nothing starts | Bug | Done |
| [CR-0002](CR-0002-docs-site.md) | Project handbook: a Docusaurus site over the docs, kept current by every task | Process | Done |
| [CR-0003](CR-0003-code-documentation.md) | Code documentation: every file and every declaration described, and checked | Process | Done |
| [CR-0004](CR-0004-code-audit-fixes.md) | Whole-code audit: bugs, edge cases and security fixes | Bug | Done |
| [CR-0005](CR-0005-era1-art-in-client.md) | Era I art in the client: models, palette and UI icons | Feature | Approved |
