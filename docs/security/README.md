# Security reports

What the system as built protects and which tests show it. The rules are in `core-instructions/07-security-model.md`; a report here
summarises them against the tests at a milestone and names what is not covered.

| Report | Milestone | Task | What it covers |
|---|---|---|---|
| [MVP-SECURITY-BOUNDARIES.md](MVP-SECURITY-BOUNDARIES.md) | M0 | M0-78 (M0-B) | Validation, auth, idempotency, locking, time, workers, rate limits, logging, attack surface, limitations, the list before the market |

A report is written by its task and updated by a later task that changes a boundary (its `Last updated` line).

Last updated: M0-78 (M0-B)
