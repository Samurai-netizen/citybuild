# CR-0013 — Builder queue

- **Status:** Approved
- **Type:** Design change
- **Requested by:** owner · **Date:** 2026-11-23
- **Milestone / era:** M1 (built in M1-02, part A, owner 2026-11-23: "option 2") / Era I
- **Conflicts with pillars (`22`) or the era rule (`25`):** none expected. It brings no new screen and no new kind of decision, only a
  wait in place of a refusal, so by `25` → Major vs minor it is a minor mechanic. Like the Chronicle (CR-0011), it would join Era I's base
  set, so the owner's approval also confirms that classification. `21` → the check-in loop already names "queue a build, upgrade or
  research". A related point about monetization (`22`): an extra builder slot stays worth buying, because queued orders still wait.

<!-- Status values: Proposed | Approved | In progress | Done | Rejected | Superseded.
     Only the owner moves a CR to Approved. No code before that. -->

## Problem / goal
Today a placement or upgrade with every builder busy is refused (`NO_FREE_BUILDER`, `04` §8). The player has to come back when a
builder frees up, which goes against the check-in rhythm of `21` (a few short visits a day). M1-02's spec also expects "a waiting
construction starts" at the second Guild of Masons (`builderSlotBonus`) completes. The owner chose a real queue for that (M1-02 build
log, decision 3).

## Proposal
**For the player:** when every builder is busy, a placement or upgrade still goes through. It waits in a **builder queue** of
`builderQueueLength` places (content rule; Era I: 2). It starts at the exact second a builder frees up: a construction or upgrade
completes, or a research adds a builder slot. The inspector shows "Queued (n)", and the HUD's builders show the queue.

**Rules (engine, `04`):**
1. `PlaceBuilding` and `UpgradeBuilding` check every rule they check today. Where they now return `NO_FREE_BUILDER`, they instead queue
   the order when the queue has room. They return `NO_FREE_BUILDER` only when the queue is full too (the code and its meaning are kept
   so the API contract doesn't break).
2. **The coins are paid when the order is queued**, as today, so a queued order can never fail later. The goods of its cost become the
   project's needs only when it starts (§16).
3. A **queued construction** is placed: it has an `Id`, takes its tile and counts toward `maxPerCity`. Its phase is `Queued`. It has no
   timer, no store and no needs, and accepts no deliveries (`ITEM_NOT_ACCEPTED`).
4. A **queued upgrade** leaves its building as it is: the building keeps producing, and its workers and couriers keep working. It can't
   be ordered again while it waits (`BUILDING_BUSY`, as for an upgrade waiting for goods).
5. **Start:** in `StartEligible`, before cycles start (`04` §6, step 5), queued orders start in the order they were made while a builder
   is free. A construction becomes a construction site, or starts its timer when its cost has no goods. An upgrade becomes an upgrade
   waiting for goods, or starts. Because this runs in every `StartEligible`, it covers a building completion, a research that adds a
   slot, and a mutation that frees a builder (later: demolish, M1-06) the same way.
6. **Busy builders** stay `Constructing` + `Upgrading` + upgrades waiting for goods. Queued orders are not busy builders.
7. New invariants (`04` §10): queued orders ≤ `builderQueueLength`; after an advance, a free builder and a queued order never exist
   together; queued orders start in the order they were made.

**Cancelling a queued order** is not part of this CR (open question 1).

## Acceptance criteria
- [ ] With every builder busy and room in the queue, `PlaceBuilding` and `UpgradeBuilding` pay the coins and queue the order. With the queue full, they return `NO_FREE_BUILDER` and change nothing.
- [ ] A queued order starts at the exact second a builder frees up (construction completion, upgrade completion, `builderSlotBonus` completion), mid-advance, in the order the orders were made.
- [ ] A queued construction takes its tile and counts toward `maxPerCity`; it accepts no deliveries; a queued upgrade's building keeps producing.
- [ ] Determinism, split invariance and timer invariance hold with queued orders (property harness extended); conservation holds; the three new invariants have tests.
- [ ] The queue is stored and loaded (migration). A snapshot shows each queued order's place and the builders' queue. The client shows both through the existing inspector and HUD.
- [ ] Content validation: `builderQueueLength` 0..`32767` (0 = no queue, which keeps M0 content's behaviour; the frozen M0 golden scenarios are unchanged).

## Out of scope
- Cancelling or reordering queued orders (open question 1).
- A research queue (one research slot, `25`).
- Paying to skip the queue, or monetization of any kind.

## Impact analysis
| Area | Impact | Files / docs |
|---|---|---|
| Engine (`04`) | `Queued` phase, queue order, start step in `StartEligible`, two mutations' checks, three invariants | `shared/Simulation/Runtime/` (State, Rules, Settlement); `04` §4, §6, §8, §10 |
| Content (`23`, JSON) | `rules.builderQueueLength` (`era1-1`: 2; M0 content: 0) | `28`, `server/content/era1-content.json` |
| API (`05`) | snapshot: building phase `queued` with its place, `builders.queued` and `builders.queueLength`; `NO_FREE_BUILDER` reworded. Additive, no v2 | `05`, the snapshot mapper and DTOs |
| Data / migration (`06`) | `buildings.queued_order int null` and `cities.next_queued_order int`; the phase check allows `Queued` | `06`, an EF migration |
| Security (`07`) | none new: the same checks; the queue length bounds stored state | `07` (no change expected) |
| Client (`09`) | inspector "Queued (n)", HUD builders with the queue; the place and upgrade buttons stay enabled while the queue has room | `CityBuilder.Client.Presentation`, `.Net` |
| Docs (recipes in `19`) | "New engine rule", "Database change"; `22` → Storage, builders, offline | module docs Simulation, Api, Infrastructure, Client.*; `COVERAGE-MATRIX.md` |
| Tests (`08`) | rule tests, mid-advance tests, property harness, persistence round trip, HTTP test | Simulation, Infrastructure and Integration tests |

## Risks and alternatives
- **Risk:** M1-02 grows (a migration, the snapshot, the client). Mitigation: it is a part of its own (A2) with its own checkpoint.
- **Alternative A (owner declined):** no queue; test that placement is refused one second before the slot and accepted at it.
- **Alternative B:** queued constructions don't take their tile until they start. That is simpler to store, but the tile could be
  taken in the meantime and the order would fail later. Rejected.
- **Alternative C:** the queue length scales with builder slots (one place per slot). That is more generous after Guild of Masons, but
  harder to explain. Left to M1-15 (balance) as a content number.

## Open questions for the owner (answered 2026-11-23: both as recommended)
1. **Cancel:** recommended **not in this CR**. M1-06 adds demolish and cancel-research (refund 500 ‰, G39, G41); a queued order's cancel
   joins it there, with a full refund because no builder worked on it. The alternative is a `CancelQueuedOrder` command now (a new
   command, error code and client button).
2. **Queue length:** recommended **2** in Era I (content, M1-15 can tune it).

## Tasks (filled in after approval)
| ID | Title | Model | Status |
|---|---|---|---|
| M1-02 (part A2) | The builder queue, built inside M1-02 (owner, 2026-11-23: "option 2") | O | In progress |

## Owner approval
- Approved on: 2026-11-23. To "approve it as written, or with changes?" and the two open questions, the owner answered "1. ok. 2. ok":
  approved as written, no cancel in this CR (it joins M1-06), queue length 2 in Era I.
