# ADR-0010 — Goods live in building stores; costs pay coins at once and their goods are delivered

- **Status:** Accepted
- **Date:** 2026-11-04 · **Task:** CR-0008-T1 (part E) · **Decision row:** `core-instructions/12-decisions.md` #049 (with #051)

## Context
Until CR-0008 every item sat in one city-wide stock with a cap per item, so where a building stood meant nothing. The owner wants moving
goods to be part of the game from the first session (CR-0008, D1): buildings keep what they make, walkers carry goods along paths the
player shapes, and building work and research get their goods by delivery. The change touches the engine's state, every command, the
snapshot, the ledger and the database at once, and players' cities are stored in its shape: it is hard to reverse.

## Decision
Every item except the one currency lives in a building's **store**, with a limit per good set by the building's level (raised by storage
research); the currency is the city's **purse**. A cost's coins are paid when the work is ordered; its goods are never taken from a store
but become the needs of a **project** (a construction site, an upgrade that keeps the building working, a research at the one science
building), filled by **deliveries** of walkers (a manual trip now, couriers from part F), and the project's timer starts when nothing is
missing. A producer works only from its own store, so the start rule touches one store and needs no shared reservations (#051).
- Engine: `04` §6 and §16–§18; the city total of an item (stores, loads, the purse) is what the HUD shows and what the ledger's
  `balance_after` follows (`06`).
- Server: `city_stores`, `city_project_needs`, `city_research_needs`, `city_trips` and `cities.purse` replace `city_inventory`; the
  migration `LogisticsStoresTripsAndReset` resets every dev city (owner decision D10).
- API: the snapshot's `purse`, `totals`, stores, needs, projects and trips (`05`; decision 050: it stays `/api/v1`).

## Alternatives considered
| Option | Pros | Cons | Why not |
|---|---|---|---|
| Keep the city-wide stock and add carts as decoration | No migration, no API break | Layout and roads mean nothing; the owner asked for the opposite | Not the game the owner wants (`22`) |
| Stores, but costs still taken from any store at once | Simpler projects, no needs | No reason to move goods for building work; deliveries only matter for chains | D1: the owner chose delivery for building work and research |
| One total limit per store instead of a limit per good | Fewer numbers in content | Inputs could fill the room a building's outputs need, so a producer could block itself | Limits per good keep a producer's inputs and outputs apart (CR-0008 D13) |
| A starting stockpile building with today's goods | The first minutes feel like before | A building that exists only for the start; D2 chose coins only and rebalanced the first costs | Owner's D2 |

## Consequences
- Easier: logistics, the market (Era II) and later eras build on goods with a place; the ledger and conservation are checked over stores,
  loads and the purse; one start pass in id order.
- Harder: every content level of a producer must list its store (content rules 3 and 9); a first session needs manual trips until a
  courier station stands; the client shows stores and projects (CR-0008-T2).
- Watch: the balance of the first session (`24` goal 1; M0-67, M0-68); the path rule's cost per walk in the performance budget (part I).
- Reversing it would need another migration that sums every store into a city-wide stock and drops the needs, and an API break; no
  player's progress could keep its projects.
