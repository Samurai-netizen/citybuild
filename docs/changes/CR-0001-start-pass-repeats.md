# CR-0001 — A start frees cap room: the start pass repeats until nothing starts

- **Status:** Done
- **Type:** Bug
- **Requested by:** owner · **Date:** 2026-10-19
- **Milestone / era:** M0 / Era I
- **Conflicts with pillars (`22`) or the era rule (`25`):** none

<!-- Status values: Proposed | Approved | In progress | Done | Rejected | Superseded.
     Only the owner moves a CR to Approved. No code before that. -->

## Problem / goal
The split invariance property of M0-14 (`04` §10, invariant 2) fails on both property contents. At one second, `Simulator.Advance` runs the
start rule once over the buildings in ascending id (`04` §6). A start takes its inputs, and that can make room under a storage cap for a
producer with a **lower** id, which the pass has already skipped. That producer stays idle, although it could run and shows no stall reason,
until the next event of any kind, possibly hours later. A settle that ends at that second, and the next settle, which starts with a start
pass, restarts it at once. So the result depends on how time is split, and players lose production with no visible reason.

Smallest case found (golden-v1, seed 249, clock 629053): a woodcutter (id 1, 2 workers) is idle, logs are at 498 of 500, and a sawmill
(id 3) is producing until 629183. At 629125 the woodcutter fills logs to 500 and stops. At 629183 the sawmill completes, the pass skips the
woodcutter (500 + 1 > 500), and then the sawmill starts and takes 2 logs (498). The woodcutter waits until the sawmill's next completion at
629363. Details: build log `docs/build-log/2026/2026-10-19-04-M0-14.md`, P1.

## Proposal
The start step at a second repeats the pass in ascending id until a pass starts nothing. Within each pass, a lower id still gets scarce
inputs and output room first. A building starts at most once per second (once it starts it is `Producing`), so the loop ends after at most
one pass more than the number of starts. Nothing changes for players except that such a producer starts in the same second.

## Acceptance criteria
- [ ] A rule test (written first, failing before the fix): a lower-id producer blocked by its full output starts in the same second when a
      higher-id building's start takes that output as input.
- [ ] `SplitInvariancePropertyTests` (M0-14) passes on both contents over 500 seeds.
- [ ] Every other Simulation test passes unchanged, golden scenario #1 included.
- [ ] `04` §6 and §7 describe the repeated pass; the Simulation module doc matches.

## Out of scope
- Any other change to the start rule, the ordering of §6, or priorities between buildings.

## Impact analysis
| Area | Impact | Files / docs |
|---|---|---|
| Engine (`04`) | The start step repeats its pass; the order within a pass is unchanged | `shared/Simulation/Runtime/Settlement/Simulator.cs`, `04` §6–§7 |
| Content (`23`, JSON) | none | — |
| API (`05`) | none | — |
| Data / migration (`06`) | none (no stored field changes) | — |
| Security (`07`) | none | — |
| Client (`09`) | none now; the client runs the same engine for display (M0-17 onwards) | — |
| Docs (recipes in `19`) | Engine rule change: spec, module doc, CHANGELOG | `04`, `docs/modules/CityBuilder.Simulation.md`, `CHANGELOG.md` |
| Tests (`08`) | A new rule test; the property test turns green | `server/tests/CityBuilder.Simulation.Tests/EngineRules/StartRuleTests.cs` |

## Risks and alternatives
- Cost: one more pass over the buildings per second in which something started (an empty pass ends the loop). M0-16 measures the budget.
- Alternative: order the pass so consumers come before producers of the same item. Rejected: it would change the visible "ascending id"
  priority and still fail for chains with cycles of items.
- Alternative: loosen invariant 2. Rejected: the idle producer is a real loss for players, not just a test artefact.

## Tasks (filled in after approval)
| ID | Title | Model | Status |
|---|---|---|---|
| CR-0001-T1 | Rule test, fix and spec, inside M0-14's branch and PR (owner's choice) | O | Done |

## Owner approval
- Approved on: 2026-10-19 — asked whether to fix it in M0-14's PR, a separate CR task first, or only write the CR, the owner chose
  "Fix in this PR (Recommended)".
