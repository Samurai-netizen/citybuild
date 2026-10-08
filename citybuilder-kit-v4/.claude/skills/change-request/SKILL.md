---
name: change-request
description: Turn an owner idea (new feature, design change, bug outside the current task) into a CR file with impact analysis. No code.
disable-model-invocation: true
argument-hint: "<short description of the idea>"
---

# Change request: $ARGUMENTS

## Existing CRs
!`ls docs/changes/ 2>/dev/null`

## Do this (`core-instructions/15-change-control.md` → feature intake)
1. Pick the next free number `CR-NNNN` (4 digits). Create `docs/changes/CR-NNNN-<slug>.md` from `docs/changes/TEMPLATE.md`.
2. Fill in:
   - problem, proposal, testable acceptance criteria, out of scope;
   - the impact table, by reading the relevant core-instructions (`04`, `05`, `06`, `07`, `09`, `19`, `22`, `25`);
   - risks and alternatives.
   Flag any conflict with the owner's pillars (`22`) or the era rule (`25`) at the top.
3. Status `Proposed`. Leave "Tasks" empty until approval.
4. Add a row to the index in `docs/changes/README.md`.
5. If you are on a task branch, commit there as `docs(<current ID>): propose CR-NNNN`. Otherwise create
   `task/CR-NNNN-proposal` from `main`, commit `docs(CR-NNNN): propose`, push and open a PR.
6. Ask the owner to approve, reject or change it. **Do not implement anything.**
