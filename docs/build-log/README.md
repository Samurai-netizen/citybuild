# Build Log

The chronological record of how this project was built: one entry per task, with **every decision**,
owner input, verification results and follow-ups. Written by AI coders (and the owner) as they work.
Rules: `core-instructions/17-documentation-system.md` §4.

- Create an entry with `bash scripts/dev/new-log-entry.sh <TASK-ID> "<title>" "<tool / model>"`. It uses `TEMPLATE.md`.
- One entry per task. Every later session on the same task appends a `### Session N` section.
- Entries are **append-only after merge** (CI enforces this). Corrections go into a new entry that links the old one.
- Project-wide decisions are also copied to `core-instructions/12-decisions.md`; architectural ones get an ADR in `docs/adr/`.

## Entries
<!-- entries: newest first -->
- [BL-20261031-08 · M0-46 · Placement flow](2026/2026-10-31-08-M0-46.md) — In progress
- [BL-20261031-07 · M0-45 · Build menu](2026/2026-10-31-07-M0-45.md) — Done
- [BL-20261031-06 · M0-44 · Building views from state](2026/2026-10-31-06-M0-44.md) — Done
- [BL-20261031-05 · M0-43 · Grid and camera](2026/2026-10-31-05-M0-43.md) — Done
- [BL-20261031-04 · M0-42 · End-to-end checkpoint](2026/2026-10-31-04-M0-42.md) — In progress
- [BL-20261031-03 · CR-0004-T1 · The fixes of the whole-code audit](2026/2026-10-31-03-CR-0004-T1.md) — Done
- [BL-20261031-02 · M0-41 · Polling and lifecycle](2026/2026-10-31-02-M0-41.md) — In progress
- [BL-20261031-01 · M0-40 · Command sender and retry policy](2026/2026-10-31-01-M0-40.md) — In progress
- [BL-20261030-04 · M0-39 · Client city store and display prediction](2026/2026-10-30-04-M0-39.md) — In progress
- [BL-20261030-03 · M0-38 · Auth and content on the client](2026/2026-10-30-03-M0-38.md) — In progress
- [BL-20261030-02 · M0-37 · API client and DTOs](2026/2026-10-30-02-M0-37.md) — Done
- [BL-20261030-01 · M0-36 · Server golden E2E over HTTP](2026/2026-10-30-01-M0-36.md) — Done
- [BL-20261029-03 · M0-35 · Logging, correlation, dev time scale](2026/2026-10-29-03-M0-35.md) — Done
- [BL-20261029-02 · M0-34 · Error catalogue, rate limits, input limits](2026/2026-10-29-02-M0-34.md) — Done
- [BL-20261029-01 · M0-33 · StartResearch](2026/2026-10-29-01-M0-33.md) — Done
- [BL-20261028-02 · M0-32 · AssignWorkers](2026/2026-10-28-02-M0-32.md) — Done
- [BL-20261028-01 · M0-31 · UpgradeBuilding](2026/2026-10-28-01-M0-31.md) — Done
- [BL-20261027-08 · M0-30 · API attack review](2026/2026-10-27-08-M0-30.md) — Done
- [BL-20261027-07 · M0-29 · PlaceBuilding](2026/2026-10-27-07-M0-29.md) — Done
- [BL-20261027-06 · M0-28 · Content and city endpoints](2026/2026-10-27-06-M0-28.md) — Done
- [BL-20261027-05 · M0-27 · Idempotency integration tests](2026/2026-10-27-05-M0-27.md) — Done
- [BL-20261027-04 · M0-26 · Command pipeline with idempotency](2026/2026-10-27-04-M0-26.md) — Done
- [BL-20261027-03 · M0-25 · Development authentication](2026/2026-10-27-03-M0-25.md) — Done
- [BL-20261027-02 · M0-24 · Concurrency tests and persistence review](2026/2026-10-27-02-M0-24.md) — Done
- [BL-20261027-01 · M0-23 · Settle use case end to end](2026/2026-10-27-01-M0-23.md) — Done
- [BL-20261026-05 · M0-22 · Transaction boundary and locking](2026/2026-10-26-05-M0-22.md) — Done
- [BL-20261026-04 · M0-21 · Testcontainers integration base](2026/2026-10-26-04-M0-21.md) — Done
- [BL-20261026-03 · M0-20 · Repository and mapping](2026/2026-10-26-03-M0-20.md) — In progress
- [BL-20261026-02 · M0-19 · EF Core model, migrations, Docker](2026/2026-10-26-02-M0-19.md) — Done
- [BL-20261026-01 · DEP-PR28 · Handbook-site npm bump (react 19.3.1)](2026/2026-10-26-01-DEP-PR28.md) — Done
- [BL-20261025-01 · CR-0003-T1 · Code documentation: every file and declaration](2026/2026-10-25-01-CR-0003-T1.md) — Done
- [BL-20261020-01 · CR-0002-T1 · Handbook site and docs-as-code workflow](2026/2026-10-20-01-CR-0002-T1.md) — Done
- [BL-20261019-08 · M0-18 · Spec sync and ADRs](2026/2026-10-19-08-M0-18.md) — Done
- [BL-20261019-07 · M0-17 · Display helpers](2026/2026-10-19-07-M0-17.md) — Done
- [BL-20261019-06 · M0-16 · Performance budget](2026/2026-10-19-06-M0-16.md) — Done
- [BL-20261019-05 · M0-15 · Invariant properties](2026/2026-10-19-05-M0-15.md) — Done
- [BL-20261019-04 · M0-14 · Property test harness](2026/2026-10-19-04-M0-14.md) — Done
- [BL-20261019-03 · M0-13 · Settlement window and production cap](2026/2026-10-19-03-M0-13.md) — Done
- [BL-20261019-02 · M0-12 · Engine review](2026/2026-10-19-02-M0-12.md) — Done
- [BL-20261019-01 · M0-11 · Golden scenario #1](2026/2026-10-19-01-M0-11.md) — Done
- [BL-20261018-02 · M0-10 · Engine rule tests](2026/2026-10-18-02-M0-10.md) — Done
- [BL-20261018-01 · M0-09 · Simulator.Advance](2026/2026-10-18-01-M0-09.md) — Done
- [BL-20261014-07 · M0-08 · City state and mutations](2026/2026-10-14-07-M0-08.md) — Done
- [BL-20261014-06 · M0-07b · Content model with research and eras: JSON file and server loading](2026/2026-10-14-06-M0-07b.md) — Done
- [BL-20261014-05 · M0-07a · Content model with research and eras: the engine](2026/2026-10-14-05-M0-07a.md) — Done
- [BL-20261014-04 · M0-06 · Day 1 checkpoint](2026/2026-10-14-04-M0-06.md) — Done
- [BL-20261014-03 · M0-05 · Unity test foundation](2026/2026-10-14-03-M0-05.md) — Done
- [BL-20261014-02 · KIT-AGENT-APPROVAL · Standing rule: ask the owner before using agents](2026/2026-10-14-02-KIT-AGENT-APPROVAL.md) — Done
- [BL-20261014-01 · M0-04-FOLLOWUP · Bootstrap scene and removal of the AI packages](2026/2026-10-14-01-M0-04-FOLLOWUP.md) — Done
- [BL-20261013-01 · M0-04 · Unity project skeleton](2026/2026-10-13-01-M0-04.md) — Done
- [BL-20261012-03 · M0-03 · Shared engine skeleton and architecture tests](2026/2026-10-12-03-M0-03.md) — Done
- [BL-20261012-02 · M0-02 · Server solution skeleton](2026/2026-10-12-02-M0-02.md) — Done
- [BL-20261012-01 · KIT-AUDIT-FIXES · Kit audit fixes: A1, A2, A3, A9, A15, A16](2026/2026-10-12-01-KIT-AUDIT-FIXES.md) — Done
- [BL-20261008-01 · M0-01 · Repository audit and kit verification](2026/2026-10-08-01-M0-01.md) — Done
- [BL-20261004-01 · KIT · Planning and instruction kit (claude.ai sessions)](2026/2026-10-04-01-KIT.md) — Done
