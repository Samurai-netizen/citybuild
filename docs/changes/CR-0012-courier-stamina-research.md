# CR-0012 — Courier stamina as a research

- **Status:** Proposed
- **Type:** Design change
- **Requested by:** owner · **Date:** 2026-11-17
- **Milestone / era:** M1 or later / Era I
- **Conflicts with pillars (`22`) or the era rule (`25`):** none. A research effect, not a new mechanic.

<!-- Status values: Proposed | Approved | In progress | Done | Rejected | Superseded.
     Only the owner moves a CR to Approved. No code before that. -->

## Problem / goal
Balance report 1 (`docs/balance/REPORT-mvp-1.md`, P3) found that the 11–12 h night absence and once-a-day play run out of courier stamina
(4 h at Courier Station level 1, 8 h at level 2). Doubling the stamina was the only lever that moved them (once a day: 35 → 68 coins/h).
On M0-68's report the owner said: "P3 should be a research for later". So the longer stamina is earned by research, and the content
numbers don't change.

## Proposal
- A new research effect type, `staminaBonus` (permille, added up like `walkSpeedBonus`): every courier station's `staminaSeconds` is raised
  by the sum, rounded with the engine's integer helpers.
- An Era I research node that grants it (name, cost, duration, prerequisites and the permille amount from a balance run, about +1000 ‰
  for the 8 h / 12 h that the report measured).
- The client shows the station's stamina with the bonus (display only).

## Acceptance criteria
- [ ] The content loader accepts `staminaBonus` and rejects a bad amount with its own error code (`04` §14).
- [ ] With the research done, a courier works for the raised stamina while the player is away; without it, for the level's stamina.
- [ ] A balance run with the new node is in a balance report, with the night absence and once a day before and after.

## Out of scope
- Changing `staminaSeconds` in the content (the owner chose a research instead).
- Re-energizing couriers or any other stamina rule.

## Impact analysis
| Area | Impact | Files / docs |
|---|---|---|
| Engine (`04`) | New effect type, applied where stamina is read | `shared/Simulation/Runtime/Content/`, `04` §6 and §14 |
| Content (`23`, JSON) | One research node | `server/content/mvp-content.json`, `23`, `25` |
| API (`05`) | None (effects reach the client in the content) | — |
| Data / migration (`06`) | None | — |
| Security (`07`) | None | — |
| Client (`09`) | Station inspector shows the bonus stamina | `client/` |
| Docs (recipes in `19`) | Add an effect type; content change | `docs/reference/generated/` (regenerated) |
| Tests (`08`) | Effect unit tests, loader case, property tests stay green | `server/tests/` |

## Risks and alternatives
- It weakens the stamina decision (check in, or upgrade the station). Keeping it a research with a real cost keeps the decision.
- Alternative: raise `staminaSeconds` in the content (report P3). The owner chose against it on 2026-11-17.

## Tasks (filled in after approval)
| ID | Title | Model | Status |
|---|---|---|---|
| CR-0012-T1 | | O/S | Todo |

## Owner approval
- Approved on: —
