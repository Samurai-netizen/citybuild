# CR-0006 — Server panel: add or remove goods on the test account

- **Status:** Approved
- **Type:** Feature (dev tooling)
- **Requested by:** owner · **Date:** 2026-11-01
- **Milestone / era:** M0 / Era I
- **Conflicts with pillars (`22`) or the era rule (`25`):** none. It touches the spirit of principle 5 (`02`: "no endpoint may set state
  directly, not even for debugging"). This CR avoids it by adding **no endpoint and no client code**: a local script edits the dev
  database. The owner decides whether that is acceptable (see Risks).

<!-- Status values: Proposed | Approved | In progress | Done | Rejected | Superseded.
     Only the owner moves a CR to Approved. No code before that. -->

## Problem / goal
Testing the client by hand needs specific stock: enough planks to test a build, an empty store to test "not enough", coins at 0.
Today the only way is to wait for production or to delete the `devtest` player and start again (`docs/getting-started.md` → Database).
The owner wants a panel next to the existing server panel (`server-panel/`) to add or remove a chosen amount of a chosen item on the
test account from the command line.

## Proposal
Extend the server panel (`server-panel/panel.sh`, started by `Server Panel.command`) with a goods section:

- **Menu entry** "Goods (devtest)": pick an item from a numbered list, pick add or remove, type an amount, see the new stock.
- **One command instead**, like the other panel commands:
  `bash server-panel/panel.sh goods show` · `goods add <item> <amount>` · `goods remove <item> <amount>` · `goods set <item> <amount>`.
- **The item list comes from the content file** (`server/content/mvp-content.json`, today `coins`, `logs`, `planks`), so no item id is a
  literal in the script (`02` rule 8) and a new item appears by itself.
- **How it writes:** a SQL file (`server-panel/goods.sql`) run with `docker compose exec postgres psql` against the dev database only.
  In one transaction it (1) locks the `devtest` city row `FOR UPDATE` (`02` rule 10), (2) upserts `city_inventory` (never below 0, a
  remove larger than the stock stops at 0), (3) writes one `ledger_entries` row with reason `DEV_ADJUST` and `balance_after` equal to the
  new stock, so the ledger still reconciles with the stock (`02` rule 11), and (4) bumps `cities.state_version`, so a running API and
  the client see a new version at the next poll.
- **It refuses** to run for any player other than `devtest` (OD-14), when the database is not the local Docker one, and when the city
  does not exist yet (log in once with the client first).
- **No endpoint, no client change, no engine change.** The API is untouched.

## Acceptance criteria
- [ ] `panel.sh goods add planks 50` raises the `devtest` city's planks by 50, writes one `DEV_ADJUST` ledger row with the right
      `balance_after`, and the Unity client shows the new amount after its next poll.
- [ ] `goods remove` larger than the stock leaves 0 (never negative) and the ledger row's delta is the amount really removed.
- [ ] `goods set` and `goods show` work; an unknown item id or a non-numeric/negative amount is refused with a message and writes nothing.
- [ ] The tool changes only the `devtest` player's city; with no `devtest` city it changes nothing and says so.
- [ ] After any adjustment the last ledger row of the item equals the stock (the reconciliation invariant of `06`).
- [ ] Goods above the storage cap are allowed and the next settlement does not remove them (caps only limit production, `04`).
- [ ] The SQL has an integration test (Testcontainers) for the points above; the shell wrapper's argument checks have a script test.
- [ ] Docs updated (see impact); `check-docs.sh` and the handbook build pass.

## Out of scope
- Any HTTP endpoint or client button that sets state (`02` rule 5). The panel is a local developer script, not part of the game.
- Changing research, buildings, workers, era or the clock from the panel (only goods; a later CR if wanted).
- Other players, a hosted/staging database, Windows (`.ps1`) support (the panel is macOS/Linux bash today).

## Impact analysis
| Area | Impact | Files / docs |
|---|---|---|
| Engine (`04`) | none. Stock added between settlements is just a larger starting quantity for the next one | — |
| Content (`23`, JSON) | none; the script only reads the item ids | `server/content/mvp-content.json` (read) |
| API (`05`) | none | — |
| Data / migration (`06`) | no schema change. New ledger reason `DEV_ADJUST` (a text column; check no code switch rejects unknown reasons) | `06` ledger reasons, `docs/reference/data-model.md` |
| Security (`07`) | new row in the abuse table: a script with direct write access to the dev DB; mitigations: devtest only, local container only, nothing in the API | `07` |
| Client (`09`) | none | — |
| Docs (recipes in `19`) | `server-panel/README.md`, `docs/getting-started.md` (Run, Database), `docs/testing/LOCAL-E2E.md`, `CHANGELOG`, build log | as listed |
| Tests (`08`) | one Testcontainers integration test running `goods.sql`; one shell test of the wrapper | `server/tests/…`, `server-panel/` |

## Risks and alternatives
- **Principle 5.** A tool that sets stock is the thing rule 5 forbids as an endpoint. A local script is not reachable by any player,
  but it is the same idea; hence this CR. If the owner prefers a strict reading, the alternative is "no tool" and keeping the
  delete-and-restart script.
- **Alternative A, dev-only API endpoint (`/dev/give`)** behind `IsDevelopment()`: rejected, because rule 5 says "not even for debugging" and a
  Development-only route still ships in the binary.
- **Alternative B, a button in the Unity client:** rejected for the same reason (it needs A) and it puts cheat UI in the client.
- **Alternative C, the owner types SQL by hand:** works today, but skips the ledger and `state_version`, which is how the dev data goes
  inconsistent. The script is the safe version of it.
- **Running API caching:** the API reads the city from the database for each request, so no restart is needed. To be confirmed in T1.
- **The `devtest` player may not exist yet** (OD-14): the tool says "log in once first" instead of creating players itself.

## Tasks (filled in after approval)
| ID | Title | Model | Status |
|---|---|---|---|
| CR-0006-T1 | `goods.sql` + integration test; `panel.sh goods …` commands and menu entry; docs | S | Done |
| CR-0006-T2 | The panel works on the player the Unity client plays as (`dev1`), not on `devtest` | S | In review |

## Change after T1 (2026-11-01)
The owner reported that the panel changed goods of a player other than the one used in Unity. T1 had taken "the test account" to be `devtest`
(OD-14), but the client plays as `dev1` (`ApiClientSettings.asset`). T2: the panel takes the player from the client's settings, or `PLAYER=<name>`,
and prints the name on every run. The "devtest only" lines above describe T1.

## Owner approval
- Approved on: 2026-11-01. Owner's words: "approved" (reply to the proposal, including its principle-5 note: a local script, no endpoint).
