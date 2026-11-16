# CR-0012 — Courier stamina as a research

- **Status:** Proposed
- **Type:** Design change
- **Requested by:** owner · **Date:** 2026-11-17
- **Milestone / era:** M1 or later / Era I (owner, 2026-11-17)
- **Conflicts with pillars (`22`) or the era rule (`25`):** none. A research effect, not a new mechanic.

<!-- Status values: Proposed | Approved | In progress | Done | Rejected | Superseded.
     Only the owner moves a CR to Approved. No code before that. -->

## Problem / goal
Balance report 1 (`docs/balance/REPORT-mvp-1.md`, P3) found that the 11–12 h night absence and once-a-day play run out of courier stamina
(4 h at Courier Station level 1, 8 h at level 2). Doubling the stamina was the only lever that moved them (once a day: 35 → 68 coins/h).
On M0-68's report the owner said: "P3 should be a research for later". So the longer stamina is earned by research, and the stamina
numbers of levels 1 and 2 don't change.

## Proposal
The owner's requirements (2026-11-17): "It should be era 1 research and it should be costly, also it should require courier station to be
level 3".
- **A new research effect type,** `staminaBonus` (permille, added up like `walkSpeedBonus`): every courier station's `staminaSeconds` is
  raised by the sum, with the engine's integer helpers.
- **An Era I research node that grants it, and it is costly:** its coins and goods are above every other Era I node (today the dearest is
  Frame Saw, 400 coins + 30 planks), and its duration is near the top of the Era I range (`25`: 10 min–4 h). The exact numbers and the
  permille (the report measured about +1000 ‰, 4 h / 8 h → 8 h / 12 h) come from a balance run.
- **A Courier Station level 3** in the content: upgrade cost (about 1.6–2.0× level 2's, `24` → Pacing), worker slots (+50–100 %), carry
  limit and stamina at least level 2's (the content rules), from the same balance run.
- **A new prerequisite kind, a building at a level:** the node can only be started while the city has a completed Courier Station at
  level 3 or more. Prerequisites today are research ids only (`04` §4 StartResearch, §14), so the engine, the content loader and the
  research screen learn it. The command stays `RESEARCH_LOCKED`, and the client shows the reason ("needs a Courier Station at level 3").
  Downgrading doesn't exist, so a started or done research is never taken back.
- **The client** shows the station's stamina with the bonus (display only) and the new lock reason.

## Acceptance criteria
- [ ] The content loader accepts `staminaBonus` and rejects a bad amount with its own error code (`04` §14).
- [ ] The content loader accepts a building-level prerequisite and rejects an unknown building or a level above its maximum, each with its
  own error code.
- [ ] `StartResearch` for the node returns `RESEARCH_LOCKED` without a completed Courier Station at level 3, and starts with one.
- [ ] The node's coins and goods are above every other Era I node's, and its Era is I.
- [ ] With the research done, a courier works for the raised stamina while the player is away; without it, for the level's stamina.
- [ ] A balance run with the new node is in a balance report, with the night absence and once a day before and after.

## Out of scope
- Changing `staminaSeconds` in the content (the owner chose a research instead).
- Re-energizing couriers or any other stamina rule.

## Impact analysis
| Area | Impact | Files / docs |
|---|---|---|
| Engine (`04`) | New effect type, applied where stamina is read; new prerequisite kind (building at a level) | `shared/Simulation/Runtime/Content/`, `04` §4, §6 and §14 |
| Content (`23`, JSON) | One research node, Courier Station level 3, a new content version | `server/content/mvp-content.json`, `23`, `25`, golden fixture (protected) |
| API (`05`) | None for the command (`RESEARCH_LOCKED` stays); the content DTO gains the prerequisite kind | `05` |
| Data / migration (`06`) | None | — |
| Security (`07`) | None | — |
| Client (`09`) | Station inspector shows the bonus stamina; the research card shows the building-level lock; a level-3 station model | `client/`, an asset request (`27`) |
| Docs (recipes in `19`) | Add an effect type; content change | `docs/reference/generated/` (regenerated) |
| Tests (`08`) | Effect unit tests, loader case, property tests stay green | `server/tests/` |

## Risks and alternatives
- It weakens the stamina decision (check in, or upgrade the station). The high cost and the level-3 station keep it a late Era I goal.
- The level-3 station needs a model (`27`: an asset request one task ahead); until then it runs on the level-2 placeholder.
- Alternative: raise `staminaSeconds` in the content (report P3). The owner chose against it on 2026-11-17.

## Tasks (filled in after approval)
| ID | Title | Model | Status |
|---|---|---|---|
| CR-0012-T1 | | O/S | Todo |

## Owner approval
- Approved on: —
