# Milestones

Durations assume one developer with Claude Code, about 4–6 focused hours a day. They are estimates,
not commitments; each milestone ends only when its exit criteria pass.

## M0 — Technical vertical slice of Era I (14 days, realistically 16–20) ← current
Prove the spine: server-authoritative, deterministic, real-time production with workers and research.
- Content: 3 items, 4 buildings (levels 1–2), 4 research nodes (`23-mvp-content.md`).
- Engine: labor model (workers speed up production, progress-preserving reassignment), auto-staffing, research
  timer and effects, storage caps, builder slots, offline production cap with a dormant tail, `era` in state.
- Server: dev auth, settle-on-access, 4 commands (place, upgrade, assign workers, start research), idempotency,
  row locks, ledger, rate limits.
- Client: grid, placement, building inspector with worker stepper, research screen, HUD with rates,
  welcome-back report, display prediction.
- Tools: balance simulator CLI with research-aware strategies; DocTools (generated reference docs).
- Process: AI workflow kit in use from Day 0 (session protocol, build log, CRs, git hooks, CI with docs checks).

**Exit:**
- golden scenario #1 works over HTTP and on a dev build;
- state survives a server restart;
- the attack, concurrency, determinism, split- and timer-invariance suites pass;
- clean-clone setup is documented;
- every project has a current module doc, every task has a build log entry, and CI is green on `main`.

## M1 — Era I complete (≈ 4–5 weeks)
Make the first 3–5 days worth playing.
- Full Era I content: ~8 items (wood, stone, grain/flour/bread chains), ~10 buildings, ~12 research nodes in
  3–4 branches, building levels up to 3–4, Land Survey expansions, more builder slots via research.
- Remaining research effect types (`25`): max level, land, slots, offline production bonus.
- Guest accounts (device-bound, upgradable to platform sign-in later) replacing dev login.
- Tutorial for the first 15 minutes, notifications (construction/research done, storage full), first art pass.
**Exit:** 5–10 external playtesters play Era I for 7 days; the balance simulator matches observed pacing within ±25 %.

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
Server container image (moved from M0), cloud hosting with managed PostgreSQL, CD pipeline, Unity tests in CI, crash reporting, metrics and alerts, Play Integrity / App Attest,
GDPR export/delete, store compliance, analytics (funnel, retention, stall reasons), Redis only if measurements demand it.
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
