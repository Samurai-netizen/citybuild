# Owner queue — questions and manual checks waiting for the owner

Things only the owner can answer or do, that outlived the task that raised them. They moved here from the resume point
(`core-instructions/13-progress.md`) on 2026-11-16 (CR-0010): that file is loaded into every session, this one only when needed.
- A task that ends with an open question for the owner adds a row; the resume point keeps only the count.
- The owner answers in chat or in a PR; the coder records the answer where it belongs (the build log entry, `12`, `15`) and deletes the row.

| # | From | Question or manual check |
|---|---|---|
| 1 | M0-66 | "For the owner" in `docs/ARCHITECTURE-FITNESS-REVIEW.md` (approve URP, uGUI, IDE packages; npm alerts; Unity in CI). |
| 2 | M0-64 | the two Follow-ups in its build log (refund reasons, a CLI). |
| 3 | M0-61 | the stashed Unity Editor settings (`git stash list`), apply or drop; Follow-ups in its build log. |
| 4 | CR-0008-T3 | the `22` edit (decision 27) and the Follow-ups in its build log. |
| 5 | CR-0008-T1 | 8 Dependabot alerts on `main` (build log Follow-ups). |
| 6 | M0-58 | the [MANUAL] content-change check (`client/README.md`; no automated test covers the `GameRoot` rebuild). |
| 7 | CR-0006-T1 | `AGENTS.md` (protected) does not list the goods commands, say if I may add them; `test-goods.sh` is not in CI (workflow files are protected); the [MANUAL] try on the real dev database (Follow-ups). |
| 8 | M0-53 and older questions | their build log entries (M0-53: the [MANUAL] look at the welcome-back report, decisions 5, 7, 9, 11). |
| 9 | M0-55 | the [MANUAL] look at the connection states (`client/README.md`). |
| 10 | CR-0007-T1 | the [MANUAL] look at the new UI (`client/README.md`); the status label may be removed (build log follow-ups). |
| 11 | CR-0011 | Approve or change the M1 plan (`docs/plan/M1-PLAN.md`): its 16 decisions in §4 (§3, the M0 regrouping, approved 2026-11-17) |
| 12 | CR-0010 | Workflow v2 narrows what counts as a logged decision (`17` §4; CR-0010 → "What changes for earlier owner directives"): confirm, or say that every small one should be logged again |
| 13 | CR-0010 | `AGENTS.md`, `CLAUDE.md`, `02`, `15` and `scripts/dev/test.sh` (protected files) were changed by this CR on your request: read their diff in the PR |
| 14 | M0-68 (M0-A) | Approve or change CR-0012 (courier stamina as a research, `docs/changes/CR-0012-courier-stamina-research.md`) |
