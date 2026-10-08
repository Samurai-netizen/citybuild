# Core Principles (non-negotiable)

These rules apply to every task and every AI coder. If a request conflicts with them, stop and say so.

## Authority
1. The Unity client is untrusted. It **proposes** commands; the server decides and applies them.
2. The client never sends balances, costs, rates, levels, timers, worker totals, timestamps or player ids.
3. Server time (`TimeProvider`, UTC, whole seconds) is the only clock that drives progress.
4. Every request that touches a city **settles** it to `now` first, inside the same transaction.
5. No endpoint may set state directly (`give-coins`, `set-level`, `finish-research`), not even "for debugging".

## Simulation
6. Production, workers, research and timers come from the deterministic engine in `shared/Simulation/`:
   integers only, no floats, no `DateTime`, no randomness, no dependence on dictionary order.
7. The client runs the same engine **for display only** and snaps to every server snapshot.
8. Game numbers live in content data (`server/content/*.json`), never as literals in code.

## Integrity
9. Every state-changing command carries a `requestId`, and retries reuse it. It never applies twice.
10. Every write runs in one transaction that row-locks **only the acting player's city**. Cross-player effects go to a mailbox.
11. Every resource change is auditable through ledger entries.

## Engineering
12. Inspect before you change; keep each change inside the current task.
13. Simple, explicit code (`14`). Add an abstraction only when it removes real duplication or crosses a layer boundary.
14. Dependency direction is fixed (`03`); architecture tests enforce it.
15. New rule → new test; bug fix → regression test. Never weaken, skip or delete a test to get green (G-Tests).
16. A task with failing build or tests is not done. Never commit secrets.

## Process (the repo is the only memory)
17. Work only on a task from `docs/plan/` or an **approved** CR (`15`). New ideas become CRs, not code.
18. Follow the session protocol (`16`): write ahead, small green steps, **checkpoint and push after every step**.
19. **Log every decision** in the task's build log entry when you make it; escalate project-wide ones (`17` §4).
20. **Docs change in the same PR as the code**, following the change recipe (`19`). Undocumented work is unfinished work.
21. Owner instructions given in chat are written into the repo in the same session (build log; standing ones in `15`).
22. Never work on or push to `main`, never bypass hooks (`--no-verify`), never force-push, never merge your own PR.

## Game design guardrails
23. **No XP, player levels or city levels**, ever. Progress = research, building levels, land.
24. **Major mechanics only arrive with a new era** (`25`). The owner's pillars in `22` override other design text.
25. Don't make choices that block eras, research effects or the open market (`26` → "What M0 must already respect").

## Honest reporting
26. Report what was actually run and passed, and name what you couldn't verify. Never claim "cheat-proof".
