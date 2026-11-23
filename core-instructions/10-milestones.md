# Milestones

Durations assume one developer with Claude Code, about 4–6 focused hours a day. They are estimates,
not commitments; each milestone ends only when its exit criteria pass.

## M0 — Technical vertical slice of Era I (14 days, realistically 16–20) ← done (M0-84)
Prove the spine: server-authoritative, deterministic, real-time production with workers and research.
- Content: 3 items, 4 buildings (levels 1–2), 4 research nodes (`23-mvp-content.md`), plus the logistics base set of CR-0008: the
  Courier Station, the science building, the Warehouse, road levels 1–2 and two more research nodes.
- Engine: labor model (workers speed up production, progress-preserving reassignment), auto-staffing, research
  timer and effects, builder slots, `era` in state; from CR-0008: building stores with limits and the purse, projects waiting for
  deliveries, roads and the path search, manual trips, couriers and routes, courier stamina (it replaces the offline production cap
  and its dormant tail).
- Server: dev auth, settle-on-access, 4 commands (place, upgrade, assign workers, start research) and the 6 of CR-0008 (roads, trips,
  routes, re-energize), presence from request times, idempotency, row locks, ledger, rate limits.
- Client: grid, placement, building inspector with worker stepper, research screen, HUD with rates,
  welcome-back report, display prediction; from CR-0008: stores and projects in the inspector, the trip flow, road mode, the courier
  station and route editor, walkers on the map, the reworked welcome-back report.
- Tools: balance simulator CLI with research-aware strategies; DocTools (generated reference docs).
- Process: AI workflow kit in use from Day 0 (session protocol, build log, CRs, git hooks, CI with docs checks).

**Exit:**
- golden scenarios #1 and #2 (CR-0008) work over HTTP, and #1 on a dev build;
- state survives a server restart;
- the attack, concurrency, determinism, split- and timer-invariance suites pass;
- clean-clone setup is documented;
- every project has a current module doc, every task has a build log entry, and CI is green on `main`.

**Exit checked (M0-84):** met; the evidence for each line is `docs/MVP-AUDIT.md` → Definition of Done, the handoff is
`docs/HANDOFF-M0.md`. Golden #1 on a dev build is met only in part: the macOS player reached the city over HTTP (M0-82), and #1 passes over HTTP in the
tests; playing #1 through in a player on a screen or a phone is the owner's check (`docs/BUILD.md`).

## M1 — Era I complete: from a tech demo to a game (≈ 9–12 weeks) ← next
Plan: `docs/plan/M1-PLAN.md` (18 tasks, approved 2026-11-23, CR-0011; decisions G24–G39). Content: `28-era1-content.md`.
Make Era I worth playing and put it in front of real players.
- **Purpose:** a premise, four advisors and the **Chronicle** (five chapters of goals with rewards) leading to the **Town Charter**.
- **Content:** the full Era I: three chains (wood, stone, bread), about 13 buildings with three levels, 14–16 research nodes in four
  branches, the remaining effect types (`25`: max level, land, builder slots, bridge, charter), a landmark.
- **Variety and map:** merchant orders and a daily caravan at the Trading Post; terrain that matters, mist and Land Survey.
- **Ownership and feel:** town name, move, demolish, decorations, cancel research; music, sound, animation, moments, townsfolk.
- **First session and return:** a guided chapter 1; local notifications from the timers.
- **Product (moved from M3):** Russian first, English second; a UI skin and settings; guest accounts with recovery and deletion
  (replacing dev login); a hosted server in a Russian cloud with managed PostgreSQL; a release Android build; server-side analytics
  (CR-0009) and a client error report; a privacy policy and a **RuStore closed alpha**.
**Exit:** 10 or more external testers play the RuStore alpha for 7 days; no P0 or P1 open; the balance simulator matches the observed
pacing within ±25 %; crash-free sessions ≥ 99 %; cold start to the town ≤ 6 s and a stable 30 fps on the reference phone, a Google
Pixel 5 (G32). Full Definition of Done: the plan's §2.

## M2 — Era II: era transition, open market, guilds (≈ 6–8 weeks)
The game becomes multiplayer. This is the riskiest milestone.
- Era transition system and ceremony; Era II content (iron, tools, cloth…, ~16 research nodes).
- Real accounts (platform sign-in), account-age gate.
- Open market (`26`): order book, escrow, mailbox deliveries in `Advance`, fees, price bands, NPC floor/ceiling,
  price history, order expiry job, ledger auditor across players, per-item kill switch.
- Guilds: create/join, members, guild projects fed by contributions.
- Operator tools: economy dashboard (money supply, price index), moderation, ledger-based rollback for one player.
**Exit:** a closed test with ≥ 50 concurrent players for 2 weeks; no duplication or ledger mismatches;
the order book is never empty for basic goods (NPC liquidity works).

## M3 — Soft-launch readiness (≈ 4 weeks)
Hosting, the release build, guest accounts with deletion and server-side analytics moved to M1 (G24). Left here: CD pipeline, Unity
tests in CI, metrics and alerts, Play Integrity / App Attest, data export, store compliance beyond RuStore, iOS, Redis only if
measurements demand it.
Monetization v1 within the rules in `22` (cosmetics, slots, capped speed-ups); server-side IAP validation.
**Exit:** soft launch in 1–2 test countries with Eras I–II.

## M4 — Era III: Renaissance & Discovery (≈ 6 weeks)
Population classes and needs, expeditions, Era III content. **Exit:** retention data from soft launch is not worse after the update.

## M5+ — Eras IV–VI as major updates
Fuel and production lines (IV), power grid and automation (V), megaprojects (VI). Each era is its own milestone with an exit test.

## Rules
- Do not start work from a later milestone while the current one's exit criteria fail.
- Moving a feature between milestones is a decision; record it in `12-decisions.md`.
- A major mechanic is always tied to an era (`25`). A milestone may build it early in code, but players only see it on entering that era.
