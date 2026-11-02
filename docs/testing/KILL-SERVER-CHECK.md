# Kill-the-server check

Proves that a server killed in the middle of requests leaves no duplicate: a command whose answer was lost counts once when the client sends it again
with the same request id. It runs the real server on the real dev database, no Unity needed, and takes about 15 seconds. Run it after a change to the
command pipeline, the receipts or the transaction around them. The rules it checks are in `core-instructions/05-api-contract.md` → Idempotency rules.

The automated tests of the same rule are `Commands/LostResponseDatabaseTests` (server) and `Tests/EditMode/Net/LostResponseTests` (client). They can't
cut the power on a process, so this check does.

## What you need
- Docker running with the dev database up: `docker compose up -d postgres` (`.env` made from `.env.example`).
- The API's two secrets (`docs/testing/LOCAL-E2E.md` → What you need) and the .NET SDK.

A server you already run on port 8080 can stay up: the check starts its own on port 8090 (another one: set `KILL_CHECK_PORT`) and never touches the other one.

## Run it
```
bash docs/testing/kill-server-check.sh
```
1. It builds and starts the server, and logs in 60 new players named `killcheck-<time>-<number>`, each with a new city.
2. It sends all 60 a placement of one house at once, and at the first few answers it kills the server with `kill -9`, so no shutdown code runs.
3. It compares the answers it got with the receipts in the database.
4. It starts the server again and sends all 60 commands again, each with its own request id and body.
5. It counts the rows of the 60 cities.

## Reading the result
A good run ends like this (the numbers of the first line change from run to run):
```
   answered 200: 7 · applied in the database: 8 · applied with the answer lost: 1 · not applied: 52
== Starting the server again and sending every command again with the same request id
== Counting the rows: buildings | COST_PLACE rows | receipts | coins | players
   1|1|1|650|60
OK: no duplicates; commands that lost their answer after their commit: 1, and their retries got the stored answer.
```
- **applied with the answer lost** is the case the check is for: the commit happened and the client heard nothing.
- **`1|1|1|650|60`** means one group of 60 cities with the same counts: one building, one `COST_PLACE` row, one receipt, and 650 coins (the starter 700 less the house's 50, with today's content).

| Exit code | Last line | Meaning |
|---|---|---|
| 0 | `OK: …` | No duplicate, and at least one command lost its answer after its commit |
| 1 | `FAILED: …` | A retry was not 200, was not the bytes of the first answer, or the counts are wrong. **A real bug:** keep the server log it names |
| 2 | a line about the setup | The database is down, the port is taken, or the build or the server's start failed |
| 3 | `NO DUPLICATES, but…` | Nothing wrong, but no command was caught between its commit and its answer, so the run proved less. Run it again |

## Cleaning up
Each run leaves its 61 players in the dev database (60 and one that warms the server up). This removes them, and only players whose name starts with `killcheck-`:
```
bash docs/testing/kill-server-check.sh --clean
```
