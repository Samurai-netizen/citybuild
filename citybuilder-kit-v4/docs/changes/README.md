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
| — | none yet | | |
