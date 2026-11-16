# M1 Plan — Era I complete: from a tech demo to a game

**Plan status: Proposed (CR-0009).** No M1 task may start before the owner approves this plan, and before M0 is closed (§3).
Written 2026-11-16. It replaces the M1 paragraph of `core-instructions/10-milestones.md` once approved (task M1-01 edits that file).

This plan has **18 tasks** (`M1-01` … `M1-18`). Each is several times bigger than an M0 task: one task is one PR and one to three
working days, split into **parts** (A, B, C…) that each end green and checkpointed. Tasks run under workflow v2
(`core-instructions/16-session-protocol.md`): tests before code, focused test runs, asset requests instead of self-made assets.
- Start a task: `/start-task M1-NN`. Print one spec: `bash scripts/dev/show-task.sh M1-NN`. Status of every task: the board in §6.

---

## 1. Why the game feels like a tech demo

M0 proved the machine: a deterministic engine, an authoritative server, chains, workers, couriers, research. It did not build a
reason to play. What a new player meets today, and what is missing:

| # | What the player meets today | What is missing | Answer in M1 |
|---|---|---|---|
| 1 | An empty lawn and 800 coins. Nothing says why the town exists or what to reach | **A purpose** | A premise, a long goal (earn the **Town Charter**) and the **Chronicle**: chapters of goals with rewards (M1-01, M1-04) |
| 2 | No guidance. The first coins come after about 20 minutes | **A first session that hooks** | A guided first chapter with an advisor; something finishes every 30–90 s in the first 10 minutes (M1-10) |
| 3 | Six research nodes, one chain (logs → planks → coins). After that nothing changes | **Content and an ending** | The full Era I: 3 chains, about 13 buildings, 3 levels, 14–16 research nodes, land, a landmark (M1-02, M1-03) |
| 4 | Coins come from one fixed recipe, forever the same | **Variety in what to make** | **Merchant orders** at the Trading Post and a daily caravan: named merchants ask for different goods (M1-05) |
| 5 | A flat 8 × 8 square. Where a building stands matters only for walking time | **A map with character** | Forest, rock, fertile soil and water; raw producers need their terrain; surveyed land reveals more (M1-03) |
| 6 | Total silence. Nothing celebrates a finished building or research | **Feel** | Music, ambience, sound on every action, animation, effects, townsfolk, day and night (M1-08) |
| 7 | The town has no name and nothing in it is "mine". A misplaced building stays forever | **Ownership** | Name the town, move and demolish, decorations (M1-06) |
| 8 | Nothing calls the player back; no reason to open the game tomorrow | **Return hooks** | Notifications from the real timers, orders that change, the next chapter, a better welcome-back (M1-05, M1-11) |
| 9 | English placeholder UI, a dev login, a server on localhost | **A product** | Russian first, a UI skin, settings, guest accounts, a hosted server, an Android release build, a RuStore alpha (M1-07, M1-12 … M1-17) |

None of this needs XP, city levels or tapping for income. Every reward is coins, goods, a decoration or story; progress stays
research, building levels and land (`core-instructions/22-game-mechanics.md`, pillars 1–6).

---

## 2. Goal and Definition of Done

**Goal:** a stranger installs the game from a RuStore alpha link, plays in Russian without help from the first tap to the Town
Charter over about a week, and wants to come back the next day.

- [ ] **Purpose:** the first screen tells who the player is and what the town is for; the Chronicle always shows the next goal and its reward.
- [ ] **First session:** a new player finishes chapter 1 (the tutorial) in 10–15 minutes without reading a manual; tutorial completion ≥ 80 % in the playtest.
- [ ] **Content:** the Era I of the design lock (M1-01) is in `server/content/`: three chains, about 13 buildings with 3 levels, 14–16 research
      nodes in 4 branches, land expansions, builder slots, a landmark; the last research grants the Town Charter.
- [ ] **Variety:** three merchant orders are always open or arriving; one daily caravan; nothing is lost by skipping a day.
- [ ] **Map:** terrain is visible and matters for raw producers; land grows by research.
- [ ] **Feel:** every player action has a sound and a visible answer; music and ambience play; finished buildings, research, orders and
      chapters are celebrated; the town shows life when nothing is tapped.
- [ ] **Ownership:** town name, move, demolish, decorations.
- [ ] **Return:** notifications for finished research and construction, resting couriers and the caravan, each with a setting.
- [ ] **Product:** every text is Russian (English as the second language); guest account, recovery and deletion; settings; a release
      Android build that talks to a hosted server over HTTPS; the privacy policy and the RuStore listing are ready.
- [ ] **Trust:** every new command has rule, application, integration and `Security_` tests written before its code; the ledger audit,
      determinism, split- and timer-invariance suites cover goals, orders, terrain and accounts; CI is green on `main`.
- [ ] **Exit (replaces the exit in `10`):** 10 or more external testers play the RuStore alpha for 7 days; no P0 or P1 open; the balance
      simulator matches the observed pacing within ±25 %; crash-free sessions ≥ 99 %; cold start to the town ≤ 6 s and a stable 30 fps on
      the reference phone (owner decision 9).
- [ ] **Process:** every task has a build log entry and closed Docs impact rows; every task tagged `[ASSETS]` has an asset request with
      every row `accepted` or `dropped`.

---

## 3. Before M1: close M0

M0 has 18 small tasks left (M0-67 … M0-84). Proposed regrouping under workflow v2 (a plan change for the owner, decision 12 in §4):

| Group | M0 tasks | One task about |
|---|---|---|
| M0-A | M0-67, M0-68, M0-70, M0-69 | Balance simulator, strategy runs, content validation, approved number changes |
| M0-B | M0-73, M0-74, M0-75, M0-77, M0-78 | The HTTP, attack and concurrency suites, the clean-clone test, the security report |
| M0-C | M0-72, M0-76, M0-79, M0-81 | Smoke checklist and playtest, bug triage, the invariant coverage matrix |
| M0-D | M0-80, M0-82, M0-83, M0-84 | Code-freeze review, development build, final audit, close-out |
| Skip | M0-71 | First-session polish: M1-10 replaces all of it |

M1-01 (design, no code) may run in parallel with M0-A … M0-D: it touches only design files. Every other M1 task waits for M0-84.

---

## 4. Owner decisions (answer before or in M1-01)

Each has a recommendation. M1-01 writes the answers into the design files and `core-instructions/12-decisions.md`.

| # | Decision | Recommendation | Why |
|---|---|---|---|
| 1 | What M1 ends with | A **RuStore closed alpha** (invited testers), then an open beta decision | Feedback from real phones before Era II is built; pulls hosting and the release build from M3 |
| 2 | The Chronicle (goals with rewards) joins Era I's base set | Yes | It is a new screen and a new kind of decision, so by the era rule (`25`) only the owner can add it to the base set, as with logistics in CR-0008 |
| 3 | Merchant orders and the daily caravan join Era I's base set | Yes | Same rule. Orders are the reason to produce different goods every day |
| 4 | Terrain that matters (forest, rock, soil, water) | Yes; first cut candidate | Gives the map character and land expansion a point. If cut, terrain stays as decoration only |
| 5 | Decorations, move, demolish, town name | Yes | Minor mechanics; ownership is cheap and missed at once when absent |
| 6 | Premise, characters and the game's name | Steward of a new settlement at a river crossing, sent by the Duke; four advisors (builder, merchant, scribe, courier master). The owner picks the name (M1-01 brings 8–10 candidates checked against RuStore) | A voice costs little and is the "soul" |
| 7 | Languages | Russian first, English second | RuStore's audience |
| 8 | Notifications | Local only, scheduled by the client from the timers it already predicts; no push server. Needs the Unity Mobile Notifications package (G-Dependency) | Timers are deterministic, so no server push is needed in M1 |
| 9 | Reference phone and Android range | The owner names one mid-range Android phone of 2021–2022 (4 GB RAM) as the performance target; Android 8.0+ | RuStore is Android only. OD-18 (iPhone shapes) gets an Android counterpart |
| 10 | Hosting | A Russian cloud with managed PostgreSQL; the owner picks the provider and the monthly budget | Russian law (152-FZ) asks that personal data of Russian users be stored in Russia. Not legal advice: the owner confirms |
| 11 | Monetization and ads in M1 | None | RuStore payments need the status of an individual entrepreneur or a company (since 2026-02-01); sell nothing before retention is known |
| 12 | Analytics | Server-side only: funnel and retention from the commands and the ledger the server already stores; a small client error report endpoint. No SDK | The server sees every action; no third-party data flow to declare |
| 13 | M0 regrouping and skipping M0-71 | As in §3 | Fewer, bigger tasks; no polish that M1-10 throws away |
| 14 | Who makes the assets | Another AI model, from asset requests (`core-instructions/27-asset-pipeline.md`); the owner runs it and delivers the files | Owner's instruction of 2026-11-16 |
| 15 | After the Town Charter (Era II does not exist yet) | The town keeps running: orders, decorations and upgrades continue, and a closing chapter says Era II is coming | No dead end for the fastest testers |
| 16 | Cancel research | Yes, with a 50 % coin refund and no goods back (the proposal in `25`) | A wrong tap on a 4-hour research must not hurt |

---

## 5. Design sketch (what M1-01 turns into specs)

**Premise.** The Duke grants you a river crossing and a year to turn it into a town. Earn the **Town Charter** and the settlement becomes a
free town (the door to Era II). Four advisors speak in one or two lines each: the master builder (building, land), the merchant (trade,
orders), the scribe (research, the Chronicle) and the courier master (logistics).

**The Chronicle.** Five chapters for Era I, six to eight goals each. Chapter 1 is the tutorial. A goal is a condition the engine can
check (a building at a level, goods produced or delivered, a research done, road tiles, an order fulfilled) and a reward (coins, a
decoration, an advisor's line in the town's chronicle). The chapter's last goal is a visible event: the first caravan, the bridge, the
windmill, the Town Hall, the Charter. Goals are content data; progress is computed by the engine from the city; a reward is claimed by a
command and written to the ledger.

**Merchant orders.** The Trading Post gets an order board with three slots. An order names a merchant, one to three goods the city can
already make, and a price above the plain selling recipe. Goods reach the Trading Post's store by courier or trip, then the player
fulfils the order; a slot refills after a wait. Once a day a **caravan** brings a bigger order with a better reward; a skipped caravan
costs nothing. Orders come from content tables through a seeded integer function of the city and the order's number, so the server and
every replay agree (rule 6 of `02` stays: no `System.Random`, no clock).

**Content.** Chains: logs → planks; stone → cut stone; grain → flour → bread. Buildings (about 13): Cottage, Woodcutter's Hut, Sawmill,
Quarry, Stonemason, Farm, Windmill, Bakery, Trading Post, Courier Station, Warehouse, Scriptorium, Town Hall (the landmark, built in
stages by delivery). Levels 1–3, the third by research (`MaxLevelBonus`). Research: 14–16 nodes in four branches (Production,
Construction, Trade, Logistics), with `LandExpansion` and `BuilderSlots`. Era I lasts 5–8 days for a player with 3–5 check-ins a day.

**Map.** Each tile has a terrain: grass, forest, rock, fertile soil or water. A Woodcutter needs forest beside it, a Quarry rock, a Farm
fertile soil; water blocks building and walking except over a bridge (a road kind). Unsurveyed land lies under mist beyond the border;
Land Survey research opens it. One hand-made map in M1 (the same for every player), with room for more later.

**Feel.** See §1 row 6 and M1-08. The rule: no player action without a sound and a visible answer; no finished thing without a moment.

---

## 6. Schedule and task board

Order is by dependency and by when assets can be ready. An asset request is written **one task ahead** (`27`), so the owner can have the
files generated while the coder works on the task before.

| Phase | Tasks | Outcome |
|---|---|---|
| A. Direction | M1-01 | Design locked, names chosen, style guide and the master asset list written |
| B. Systems that give purpose | M1-02 … M1-06 | Full Era I rules on the server and in the client, with placeholder art and English keys |
| C. Feel and first session | M1-07 … M1-11 | It looks, sounds and reads like a game; a guided first session; notifications |
| D. Product | M1-12 … M1-14 | Guest accounts, a hosted server, a release Android build |
| E. Tuning and proof | M1-15, M1-16 | A week of Era I that paces well; cross-system suites |
| F. Release to testers | M1-17, M1-18 | RuStore alpha, playtest, fixes, close-out |

**Realism check.** 18 tasks at one to three days each is **9–12 weeks** at 4–6 focused hours a day, not the 4–5 weeks `10` gave the old,
smaller M1. The asset generation and the `[MANUAL]` steps run beside it and are the owner's time. If behind, cut in this order:
decorations (keep move and demolish) → day and night and townsfolk → the terrain rule (keep the look) → the daily caravan (keep orders)
→ English → the RuStore update and review SDKs. Never cut: the Chronicle and the tutorial, audio, Russian, guest accounts, hosting,
tests first.

Status values: `Todo` · `In progress` · `In review` · `Done` · `Blocked` · `Skipped` (reason in the build log).

| ID | Phase | Task | Tags | Status |
|---|---|---|---|---|
| M1-01 | A | Design lock for Era I, style guide, master asset list | O PLAN MANUAL | Todo |
| M1-02 | B | Full Era I content and the three new research effects | O PLAN | Todo |
| M1-03 | B | The map: terrain, placement rules, land expansion | O PLAN ASSETS | Todo |
| M1-04 | B | The Chronicle: goals, chapters, rewards | O PLAN ASSETS | Todo |
| M1-05 | B | Merchant orders and the daily caravan | O PLAN ASSETS | Todo |
| M1-06 | B | Town management: name, move, demolish, decorations, cancel research | O ASSETS | Todo |
| M1-07 | C | Russian localization, UI skin, settings | S ASSETS MANUAL | Todo |
| M1-08 | C | Game feel: audio, animation, effects, life | S ASSETS MANUAL | Todo |
| M1-09 | C | Era I art into the client and the asset checker | S ASSETS MANUAL | Todo |
| M1-10 | C | Tutorial and the first session | O PLAN ASSETS MANUAL | Todo |
| M1-11 | C | Notifications and return hooks | S MANUAL | Todo |
| M1-12 | D | Guest accounts, recovery, deletion | O PLAN | Todo |
| M1-13 | D | Hosting and operations | O PLAN MANUAL | Todo |
| M1-14 | D | Android release build and performance | S MANUAL | Todo |
| M1-15 | E | Balance for a week of Era I | O MANUAL decision | Todo |
| M1-16 | E | Cross-system suites and security report | O | Todo |
| M1-17 | F | RuStore listing and closed alpha | S ASSETS MANUAL | Todo |
| M1-18 | F | Playtest fixes and M1 close-out | O MANUAL | Todo |

---

## 7. Task specs

Tags: **[O]** strongest model · **[S]** standard model · **[PLAN]** the owner approves the plan before code · **[MANUAL]** has human steps
· **[ASSETS]** has an asset request (`docs/assets/requests/AR-M1-NN.md`; code works with placeholders until the files arrive).

**How to read a spec.** *Read first* lists the only files to open before coding (sections, not whole files). *Parts* are the checkpoints.
*Tests first* names the behaviours whose tests are written, and seen failing, before the part's code (`08` → Test-first loop); add the
obvious neighbours, don't pad. *Full run* says when the whole suite is worth its cost before the finish. Every task also follows
`AGENTS.md`, the session protocol and its change recipe in `19`. Where a spec and the design files (`21`–`26`) disagree, the files win.
Numbers are never in a spec: they are in the design lock and the content JSON.

#### M1-01 — Design lock for Era I, style guide, master asset list · [O] [PLAN] [MANUAL]
```text
No code. Turn §4 and §5 of this plan into specs the next 17 tasks can build from. Present each part to the owner; an answer is recorded
with the owner's words (build log → Owner input, 12-decisions.md).
Read first: this plan §1–§5; 22 (pillars, core loop); 25 (rules, major vs minor); 24 (goals, pacing); 21 (look, key screens);
art/README.md → Style guide.
Parts:
 A. Decisions: walk the 16 owner decisions of §4, one message, recommendations first. Write the answers down. Nothing below starts
    before decisions 1–7 and 14 are answered.
 B. Game design files: 22 (the Chronicle, orders, terrain, decorations, move/demolish, cancel research; the base set of Era I), 25
    (effect types now built, the Town Charter as the end of Era I, what happens after it), 21 (premise, advisors, new key screens), 24
    (orders and goal rewards as faucets, the caravan, targets for a week of Era I), 26 unchanged unless an answer touches it, 31 (new
    words), 10 (the M1 paragraph and exit from this plan; what moves from M3), 30 (rewritten for M1).
 C. Content design: a new core-instructions/28-era1-content.md with every item, building and level, recipe, research node (four
    branches, prerequisites, effects), the map (a text grid), the five chapters and their goals with rewards, the order tables, the
    decorations. Ratios are small integers (24). Numbers are a first pass for the simulator (M1-15), marked as such. 23 stays the M0 record.
 D. Names and words: the game's name (8–10 candidates, each checked for an existing game of that name in RuStore and Google Play; the
    owner picks), the town's default name, advisors' names, every item, building and research in Russian and English. Tone: 21 → Tone.
 E. Style guide: docs/assets/STYLE.md, the one page every asset request points to: 3D (extends art/README.md: proportions, palette
    slots, triangle budgets, pivots, naming, art/MOTIONS.md), UI (shapes, colours, 9-slice rules, icon style, fonts with Cyrillic),
    portraits and illustrations, audio (mood, instruments, loudness, formats, lengths). Reference pictures come from art/previews/.
 F. Master asset list: docs/assets/requests/AR-M1-03.md … AR-M1-17.md, one per [ASSETS] task, every row filled from the template; the
    indicative counts are in §8. AR-M1-03 and AR-M1-04 are complete at the end of this task; later ones may leave descriptions short
    and are finished one task ahead (27).
Acceptance: every decision of §4 has an answer in 12; 22, 25, 21, 24, 10, 30 and 31 agree with each other and with 28 (a docs-sync pass
by hand); the handbook builds; the owner approved parts B–E in words; M1-02 … M1-06 can be started without a design question.
Out of scope: code, content JSON, any asset file.
```

#### M1-02 — Full Era I content and the three new research effects · [O] [PLAN]
```text
The whole Era I of 28-era1-content.md runs in the engine, on the server and in today's generic client UI, with placeholder models.
Read first: 28 (all); 04 §3–§9 and §14 (labor, recipes, research, effects, invariants); 25 → Effect types; 19 → "New engine rule or
effect type" and "Content-only change"; docs/how-to/add-research-effect.md; docs/how-to/add-building-or-item.md.
Parts:
 A. Effects. Tests first: MaxLevelBonus raises one type's level cap and an upgrade past the old cap is accepted only after it;
    BuilderSlots adds slots at the exact completion second and a waiting construction starts then; each effect is refused in content
    validation with a bad target; determinism, split- and timer-invariance hold with the new effects (property harness extended);
    conservation holds. Then build: the effect types, validation, the snapshot fields, 04 and 25.
    (LandExpansion is M1-03: it needs the map.)
 B. Content. Tests first: the new content file loads and validates; every recipe input has a producer; every item has a use; each chain
    step's ratio is the one 28 states (read from content, never a literal); the last research completes the era's tree; golden
    scenario #3 (the bread chain with couriers, hand-checked table in 28) on a frozen copy. Then build: server/content/era1-content.json
    (content version era1-1), staged delivery for the landmark through the existing project rules, DocTools sentences, the generated
    reference. The M0 golden files and their tests stay untouched (frozen content).
 C. Server and client. Tests first: integration tests that a new player gets the new starting city, that each new building places,
    produces and upgrades over HTTP, that a level-3 upgrade is refused before its research (error code, no lock side effects), and the
    ledger audit over a scripted hour. Then build: only what fails; the client shows new content through the existing menus, with
    placeholder boxes and missing-icon tiles (27 → Placeholders).
Full run: after part A (the engine's shared rules changed) and at the finish.
Acceptance: golden #1–#3 pass; a scripted player reaches the last research over HTTP with FakeTimeProvider; the performance budget of
04 §12 holds with 13 building types on the largest map; the docs of the recipes are closed.
Out of scope: terrain, goals, orders, final numbers (M1-15), art.
```

#### M1-03 — The map: terrain, placement rules, land expansion · [O] [PLAN] [ASSETS]
```text
The town stands on land with forest, rock, fertile soil and water; raw producers need their terrain; research opens more land.
Read first: 28 → Map; 04 §16–§17 (paths, roads) and §12 (performance); 06 (city tables); 05 → snapshot and PlaceBuilding;
docs/how-to/add-migration.md; 09 → City view.
Parts:
 A. Engine. Tests first: a Woodcutter is refused without forest beside it, a Quarry without rock, a Farm off fertile soil (one error
    code each, named in 05); nothing is built on water or forest; walkers never cross water except on a bridge road; LandExpansion grows
    the bounds at the completion second and reveals the map's terrain there; the path search keeps the budget of 04 §12 on the largest
    map (measured, in Release); old cities without terrain load as all grass (migration test). Then build: terrain in content (the map)
    and state (bounds), the placement rule, the bridge as a road kind, 04, 05, 06, the migration.
 B. Server. Tests first: integration and Security_ tests: placing outside the surveyed bounds or on forbidden terrain is refused
    before the city is locked when the coordinates can never be valid (OD-13), by the engine otherwise; the snapshot carries bounds and
    terrain once per content version, not per poll.
 C. Client. Tests first (EditMode): the ground model picks the right tile kind and variant per terrain; the build menu's placement
    hints mark allowed tiles for the chosen building; the camera limits follow the bounds. Then build: terrain tiles, mist over
    unsurveyed land, the reveal when land opens, hints. Visuals are checked by the [MANUAL] list in client/README.md.
Assets: AR-M1-03 (terrain tiles and variants, mist, bridge, survey stakes). Placeholders: tinted flat tiles.
Full run: after part A and at the finish.
Acceptance: the tutorial town of 28 can be laid out on the map; a 7-day scripted run with every expansion stays inside the performance
budget; golden #1–#3 still pass (their content has no terrain).
Out of scope: more than one map, terrain bonuses, 2 × 2 buildings.
```

#### M1-04 — The Chronicle: goals, chapters, rewards · [O] [PLAN] [ASSETS]
```text
The player always has a next goal, sees its progress, and claims a reward for it. Chapter 1's goals are the script of the tutorial
(M1-10 adds the guiding).
Read first: 28 → Chronicle; 04 §8 (commands), §10 (invariants), §13 (mailbox seam: untouched); 05 → Commands and errors;
docs/how-to/add-command.md; 07 → threat table; 19 → "New command".
Parts:
 A. Engine. Tests first: each condition kind of 28 becomes true at the exact second it is met, also in the middle of a long absence
    (split invariance: one Advance or many give the same completion time and order); counters (produced, delivered, fulfilled) never go
    down and survive upgrades, moves and demolition; a goal completes once; chapter N+1 opens only when chapter N's goals are done;
    content validation refuses a goal that nothing in the content can satisfy. Then build: goal content, counters in state, completion
    in Advance, 04.
 B. Claim. Tests first: ClaimGoalReward pays the coins once (replay with the same requestId returns the stored result), refuses an
    unfinished or already claimed goal with its error code, writes ledger rows the auditor reconciles, and grants a decoration as an
    owned item (M1-06 places it); Security_: another player's goal, a goal id below 1 (refused before the lock, OD-13), a claim racing a
    second claim (concurrency test, one winner). Then build: the command, endpoint, DTOs, tables, migration, 05, 06, 07.
 C. Client. Tests first (EditMode): the Chronicle view-model orders chapters and goals, shows progress text from the snapshot, enables
    Claim only for finished goals, and the HUD tracker shows the first unfinished goal; the screen joins the menu group (OD-16) and
    scrolls one way (OD-17). Then build: the Chronicle screen, the HUD goal tracker, the claim moment (placeholder effect and sound
    hooks for M1-08), the welcome-back line for goals finished while away.
Assets: AR-M1-04 (chapter illustrations, seal and reward icons, the Chronicle button). Placeholders: flat cards.
Full run: after part A and at the finish.
Acceptance: a scripted player finishes all five chapters over HTTP; the ledger audit reports zero mismatches; no goal can be claimed
twice under 50 concurrent claims.
Out of scope: daily or repeating goals, goods as rewards unless 28 says where they land, achievements beyond the chapters.
```

#### M1-05 — Merchant orders and the daily caravan · [O] [PLAN] [ASSETS]
```text
The Trading Post shows three orders from named merchants and one caravan a day; fulfilling them is the varied way to earn coins.
Read first: 28 → Orders; 22 → Trading Post; 24 → Sources and sinks; 04 §2 (time, presence) and §18 (deliveries); 02 rule 6;
docs/how-to/add-command.md.
Parts:
 A. Generator. Tests first: the same city and order number always give the same order (fixed vectors in the test); an order asks only
    for goods the city has unlocked; its price is inside the band of 28 relative to the plain selling recipe; a refilled slot appears at
    the exact second after its wait, also during an absence, the same for one Advance or many; the caravan appears once per server day
    (the day boundary is content data) and leaves without any penalty; no float, no clock, no System.Random (architecture test).
    Then build: the seeded integer function (spec'd in 04 with its constants), order tables in content, orders in state.
 B. Commands. Tests first: FulfilOrder takes the goods from the Trading Post's store and pays the purse once, refuses with its error
    code when goods are short, the order is gone or the Trading Post is paused or missing; DismissOrder frees the slot after the dismiss
    wait; ledger rows reconcile; Security_: replay, another player's order, ids below 1, two fulfils racing. Then build: commands,
    endpoints, DTOs, tables, migration, 05, 06, 07; the goal condition "orders fulfilled" of M1-04 gets its counter.
 C. Client. Tests first (EditMode): the board view-model shows per order what is in the Trading Post's store against what is asked,
    enables Fulfil only when complete, shows the refill countdown from the snapshot's times, and offers "send goods" to the trip flow
    for a missing good. Then build: the order board in the Trading Post's inspector and as its own menu entry, merchant cards, the
    caravan's arrival on the map (placeholder), the HUD mark for a ready order.
Assets: AR-M1-05 (merchant portraits, caravan model, order icons). Placeholders: initials on coloured circles, a box on wheels.
Full run: after part A and at the finish.
Acceptance: over a scripted week the orders never repeat within a day, always stay fulfillable with the unlocked buildings, and pay
within the band; BalanceSim strategies can use orders (a small adapter; the tuning is M1-15).
Out of scope: player-to-player trade (Era II, 26), order chains, bargaining, reputation.
```

#### M1-06 — Town management: name, move, demolish, decorations, cancel research · [O] [ASSETS]
```text
The town is the player's own: it has a name, mistakes can be undone, and it can be made pretty.
Read first: 28 → Decorations and the rules for move, demolish and cancel; 04 §8 and §18 (what happens to a store, couriers and routes
when a building goes); 05 → Commands; docs/how-to/add-command.md.
Parts:
 A. Engine. Tests first: demolishing returns the share of 28 in coins, refuses while goods are inside unless confirmed (then the goods
    are lost and logged in the ledger), frees its workers, ends trips and removes route stops that named it (couriers go home with their
    loads returned as 04 §18 says); moving keeps level, store and progress, costs what 28 says, refuses forbidden terrain, and re-plans
    paths; a decoration takes a tile, gives nothing, needs no workers, never appears in rates; cancelling a research refunds the share
    of 28 and frees the slot at once; conservation and the ledger audit hold through each.
 B. Server. Tests first: integration and Security_ tests for RenameCity (length, allowed characters, a small deny list in content,
    refused before the lock when it can never be valid), MoveBuilding, DemolishBuilding, CancelResearch, placing an owned decoration
    (not more than owned). Then build: commands, endpoints, DTOs, migration, 05, 06, 07.
 C. Client. Tests first (EditMode): move mode view-model (ghost, allowed tiles, cost line), the demolish confirmation text with the
    refund and the goods that would be lost, the decorations tab (owned against placed), the name field's validation message. Then
    build: move and demolish in the inspector, the decorations tab of the build menu, the town name in the HUD and settings.
Assets: AR-M1-06 (10–12 decorations and their icons). Placeholders: tinted boxes.
Full run: at the finish.
Acceptance: after any sequence of place, move and demolish in a property test the city passes every invariant of 04 §10 and no worker,
courier or good is orphaned.
Out of scope: rotating buildings, storing a building in an inventory, decoration bonuses, paid decorations.
```

#### M1-07 — Russian localization, UI skin, settings · [S] [ASSETS] [MANUAL]
```text
Every text the player sees comes from a string table, in Russian first; the UI wears one medieval skin; a settings screen exists.
Read first: 09 → UI rules and UX rules (OD-16 … OD-18); docs/how-to/add-ui-screen.md; docs/assets/STYLE.md → UI; 28 → Names;
14 → approved dependencies (no localization package without the owner: plain JSON tables are the default).
Parts:
 A. Strings. Tests first (EditMode): every key used in code and UXML exists in ru and en (a scan test); no player-facing literal is
    left in Presentation (a scan test with an allow-list); Russian plurals pick the right form for 1, 2–4, 5–20, 21, 22, 25, 111 (one,
    few, many); numbers, durations and rates format per language; an unknown key shows the key and logs once; error codes map to
    Russian messages (extends the existing "no code without a message" test); content names come from a content text table keyed by
    id and language. Then build: the tables, the lookup, the plural rule, the language setting; move every existing text.
 B. Skin. Tests first: the mobile layout tests (MobileLayoutTests, check-ui-rules.py) pass with the longest Russian strings (a
    pseudo-long fixture: Russian text runs about 20–30 % longer than English); fonts contain every Cyrillic character used in ru
    (a scan test over the table against the font's character set). Then build: the USS theme from AR-M1-07 (panels, buttons, tabs,
    chips, bars), applied to every existing screen.
 C. Settings. Tests first: the settings view-model stores language, music and sound volume, notification toggles (used by M1-11) and
    survives a restart; shows the player id and the app and content versions. Then build: the screen (it joins the menu group), links to
    the privacy policy and support (addresses from config, filled in M1-17).
 [MANUAL] The owner reads every Russian string in the game once (a generated list, docs/testing/strings-ru.md) and corrects the tone.
Assets: AR-M1-07 (9-slice panels, buttons, icons, two font families with Cyrillic and their licences).
Full run: at the finish (Unity EditMode and PlayMode, the owner's machine).
Acceptance: switching the language changes every visible text without a restart; no clipped or overflowing text at 866–889 units on
the long side in Russian; no English left in a Russian session (the owner's read-through).
Out of scope: more languages, right-to-left, voice.
```

#### M1-08 — Game feel: audio, animation, effects, life · [S] [ASSETS] [MANUAL]
```text
No action without a sound and a visible answer; no finished thing without a moment; the town lives when nobody taps.
Read first: 21 → The look, Tone; art/MOTIONS.md; docs/assets/STYLE.md → Audio and Effects; 09 → Presentation rules, performance notes.
Parts:
 A. Audio system. Tests first (EditMode): the sound map has an entry for every UI action and every engine event kind the client shows
    (a scan test: a new event without a sound fails); volumes and mutes follow the settings; music changes with day and night and
    never restarts on a menu; a missing clip plays silence and logs once (27 → Placeholders); at most N voices at once (config). Then
    build: the audio service (music, ambience, effects), hooks on buttons, placement, construction, production cycles near the camera,
    couriers, coins, research, goals, orders, errors.
 B. Moments. Tests first: each "finished" event (building, upgrade, research, goal, chapter, order, caravan, land opened, Town Charter)
    queues exactly one moment, moments never overlap and never block input for longer than the limit in config, and a welcome-back with
    ten finished things shows one summary, not ten moments. Then build: the moment queue, banners, fly-to-HUD rewards, counters that
    tick, construction rise and dust, button and panel motion, haptics behind a setting.
 C. Life. Tests first: the number of ambient figures is capped by config and by the frame budget; day and night follows device time and
    changes nothing in the engine's state (architecture test: Presentation only). Then build: townsfolk near cottages, animals, birds,
    water and smoke motion, the light cycle.
 [MANUAL] The owner plays 15 minutes with sound on and lists every action that felt dead and every sound that annoys on repeat.
Assets: AR-M1-08 (music loops, ambience loops, about 45 sound effects, effect textures, villager and animal models).
Full run: at the finish.
Acceptance: the [MANUAL] list has no open "dead action"; 30 fps holds on the reference phone with everything on; audio memory stays
inside the budget in STYLE.md.
Out of scope: voice acting, weather, cutscenes.
```

#### M1-09 — Era I art into the client and the asset checker · [S] [ASSETS] [MANUAL]
```text
Every placeholder of M1-02 … M1-06 is replaced by its delivered asset, and a script proves each delivery matches its request.
Read first: 27 (all); art/README.md → Rebuild, Style guide; art/MOTIONS.md; the open AR files.
Parts:
 A. Checker. Tests first (its own small test script with sample files): scripts/dev/check-assets.sh <AR file> fails on a requested
    file that is missing, misnamed, in the wrong folder, the wrong format, the wrong pixel size, over its file-size budget, or not
    listed in docs/assets/LICENSES.md; reports rows still `requested`; passes a complete delivery. Then build it and document it in 27.
 B. Integration. Tests first (EditMode, PlayMode): every building type and level, item, research node, terrain kind, decoration,
    merchant and chapter of the content resolves to a real asset, not a placeholder (a scan test with a "pending" list that must be
    empty at the finish); models keep their tile and budgets (the existing art checks). Then build: import settings, name maps, store
    fills and carried loads for the new goods, motions for the new producers.
 [MANUAL] The owner looks at every model in the town at both zoom limits and marks rejects; a reject goes back as a row in the AR with
 the reason, never fixed by the coder.
Assets: the remaining rows of AR-M1-03 … AR-M1-06 and AR-M1-09 (new buildings and levels, fills, loads, icons).
Full run: at the finish.
Acceptance: the pending list is empty; check-assets passes for AR-M1-03 … AR-M1-09; the build size stays inside the budget of M1-14.
Out of scope: making or editing any asset; Era II looks.
```

#### M1-10 — Tutorial and the first session · [O] [PLAN] [ASSETS] [MANUAL]
```text
A new player is led through chapter 1 by an advisor and leaves the first session with a running chain, a research under way and a
reason to return.
Read first: 28 → Chapter 1; 24 → Goals 1–4; the M1-04 Chronicle view-models; docs/how-to/add-ui-screen.md.
Parts:
 A. First-session numbers. Tests first: a BalanceSim scenario "first session" (a scripted new player who follows the goals) asserts the
    targets of 24 as rewritten in M1-01: first reward inside the first minute, something finishes every 30–90 s for 10 minutes, first
    coins from production and the first courier inside the targets, the session ends with a research longer than the break. Then tune
    chapter 1's goals, rewards and the starting city in content until it passes. A number change is a content change (19).
 B. Guide. Tests first (EditMode): the guide is driven by the snapshot only (the current goal and the city's state), so killing the app
    at any step and returning shows the right step (a test per step); it never blocks a tap the step does not need for longer than a
    skippable line; it ends for good when chapter 1 is claimed; "skip tutorial" exists in settings. Then build: advisor lines with
    portraits, the spotlight and pointer, the stall explainer (tap a stalled building → why, and a button that opens the fix), empty
    states for every screen.
 C. First run. The name-your-town moment, the premise in three short cards, the notification permission asked when the first long
    research starts (M1-11), the welcome-back report's new look.
 [MANUAL] Three people who never saw the game play the first 15 minutes while the owner watches silently and notes every hesitation
 (docs/testing/FIRST-SESSION-PLAYTEST.md gives the script and the form). Fix every P0 and P1, each with a test where testable.
Assets: AR-M1-10 (advisor portraits with expressions, pointer, premise cards).
Full run: after part A (content changed) and at the finish.
Acceptance: all three first-time players finish chapter 1 without help in under 15 minutes; the guide survives an app kill at every step.
Out of scope: a tutorial for Era II mechanics, videos, forced tutorials on a returning account.
```

#### M1-11 — Notifications and return hooks · [S] [MANUAL]
```text
The game tells the player, politely, when something worth returning for has happened.
Read first: 21 → Tone (no dark patterns); 22 → Notifications; 09 → display prediction; owner decision 8 (G-Dependency: ask before
adding the package).
Parts:
 A. Planner. Tests first (EditMode): from a snapshot the planner lists the next events worth a notification (research done,
    construction done, couriers resting, the caravan arrived, a store full for the first time), each at the predicted second; events
    closer than the merge window become one message; nothing is scheduled inside the quiet hours of the settings; at most N a day
    (config); each kind obeys its toggle; a new snapshot replaces the old schedule, so nothing fires for a thing already seen.
 B. Platform. Then build: the Android side behind an interface (channels, the Android 13 permission, the icon), scheduling on app
    pause, clearing on resume, opening the right screen from a tapped notification.
 C. The "next" strip in the HUD: what happens next and when, from the same planner.
 [MANUAL] The owner checks on a phone: permission flow, a scheduled notification after 2 minutes with DevTimeScale, the quiet hours.
Full run: at the finish.
Acceptance: no notification ever arrives for something that did not happen (the planner's times match the server's within the
prediction's tolerance, tested against recorded snapshots); every text is in the string tables.
Out of scope: remote push, marketing messages, any message about a loss.
```

#### M1-12 — Guest accounts, recovery, deletion · [O] [PLAN]
```text
A real player gets an account without typing anything, keeps the town when reinstalling, and can delete everything.
Read first: 07 (all); 05 → Auth; 06 → players; the M0-25 dev auth code; owner decisions 10 and 12.
Parts:
 A. Guest. Tests first: registration creates one player per install secret and returns tokens; the secret is stored hashed; tokens
    expire and refresh; a stolen refresh token is revoked when reused; registration and refresh are rate limited per address and
    device; dev login answers only in Development (a test on the Production configuration); every existing Security_ test passes with
    the new auth. Then build it.
 B. Recovery and deletion. Tests first: a recovery code (shown once, stored hashed) moves the town to a new install and ends the old
    sessions; wrong codes are rate limited; DeleteAccount removes or anonymizes every personal row and the town after the grace period
    of the privacy policy, and the ledger stays auditable; an export of the player's data exists for the owner to send on request.
 C. Client: silent first registration, the recovery code screen in settings, restore on a fresh install, delete with a typed confirmation.
Full run: after part A (every endpoint's auth changed) and at the finish, with the integration category.
Acceptance: 07 has a row per new threat with its test; the attack suite runs against guest auth; no dev endpoint is reachable in the
release configuration.
Out of scope: VK ID, Yandex ID or phone sign-in (a later CR), linking two devices at once, friends.
```

#### M1-13 — Hosting and operations · [O] [PLAN] [MANUAL]
```text
The API runs on the internet, in the provider the owner chose, and the owner can see that it is healthy.
Read first: 03 → deployment notes; 06 → migrations; docs/getting-started.md → Run; 30 (what moves from M3); owner decision 10.
Parts:
 A. Plan (owner approves): provider, sizes, monthly cost, domain and TLS, backups and a restore drill, secrets handling, a staging and
    a production environment, who can deploy. New tools or services are G-Dependency.
 B. Image and deploy. Tests first: the container starts with the Production configuration and passes the health check with migrations
    applied (an integration test builds and runs the image where Docker exists); it refuses to start with a missing secret or with dev
    features on; a migration from the previous release's schema keeps the data (test with a dump fixture). Then build: the image, the
    deploy script or pipeline, [MANUAL] account and DNS steps written as a numbered list for the owner.
 C. Seeing it. Tests first: the funnel and retention queries return the hand-computed numbers on a fixture database (registered,
    finished each tutorial goal, claimed chapter 1, returned on day 1, 3 and 7; stalls by reason); the client error report endpoint
    stores a bounded, rate-limited report without personal data. Then build: those, structured logs kept for the period of the privacy
    policy, an uptime check with an alert to the owner, the restore drill run once and written down.
Full run: at the finish.
Acceptance: staging and production answer over HTTPS; a deploy is one command; a backup was restored once; the owner can open the
funnel report; docs/operations/ has the runbook (deploy, roll back, restore, rotate secrets).
Out of scope: autoscaling, Redis, queues, a second region, an admin web panel.
```

#### M1-14 — Android release build and performance · [S] [MANUAL]
```text
A signed release build for Android installs from a file, starts fast, runs smoothly and is small enough.
Read first: client/README.md; 09 → build and performance notes; M0-82's docs/BUILD.md; owner decision 9.
Parts:
 A. Build. Tests first: a build check script fails when the release configuration allows HTTP, has a development flag, contains the dev
    login, points at a non-production server, lacks the link.xml entries the DTO tests need, or is over the size budget. Then build: the
    scripted build (IL2CPP, arm64, the target API level RuStore asks for on the day, checked in M1-17's checklist), signing with a
    keystore that never enters the repository, version and build numbers from one place.
 B. Budgets. Tests first where measurable in the Editor (PlayMode): allocations per frame in the idle town stay at the budget; draw
    calls and triangles for the largest finished town stay under the numbers set in this part. Then measure on the reference phone and
    fix: frame time, memory, start time, battery (30 fps cap, nothing runs while paused), the poll rate on mobile data.
 C. Rough edges: no network at start, a lost connection mid-command, the app killed in the background, a server update in progress,
    an old client (the M0-58 gate, with a Russian message and a button to the store page).
 [MANUAL] The owner installs the build on the reference phone and one small and one tall phone, and walks docs/testing/RELEASE-SMOKE.md.
Full run: at the finish.
Acceptance: the budgets of this task are written in 09 and met on the reference phone; the smoke list has no open P0 or P1.
Out of scope: iOS, tablets' own layouts, app bundles with on-demand assets.
```

#### M1-15 — Balance for a week of Era I · [O] [MANUAL] decision
```text
The numbers make Era I a good week: never stuck, never done in an afternoon.
Read first: 24 (all); 28; docs/how-to/run-balance-simulator.md; the M0 balance report.
Parts:
 A. Simulator. Tests first: strategies handle the new buildings, terrain, goals (they claim) and orders (they fulfil the best paying
    one they can); a run is deterministic; the new metrics are computed correctly on a tiny fixture (time to each chapter, to the Town
    Charter, coins by source: selling, orders, goals; idle workers; stalls by reason; hours with nothing affordable).
 B. Report. Run every strategy against the session patterns for 10 days. Write docs/balance/REPORT-era1-1.md: where a player waits
    with nothing to do, where coins pile up unused, which research order dominates, how much orders pay against plain selling, the
    proposed number changes and the expected effect of each. No content change yet.
 C. [MANUAL] The owner picks the changes. Apply exactly those (content version era1-2), re-run, append before and after.
Full run: after part C (content changed: golden tests untouched, everything else runs).
Acceptance: for the "3–5 check-ins a day" pattern the Town Charter comes on day 5–8; no check-in in the first three days has nothing
worth doing; orders give 25–45 % of the coins (or the band the owner sets); the report names what the simulator cannot judge.
Out of scope: Era II numbers, market prices.
```

#### M1-16 — Cross-system suites and security report · [O]
```text
Each system arrived with its own tests. This task proves they hold together and names what is still open.
Read first: 08 → Layers and invariants; 07; docs/testing/COVERAGE-MATRIX.md; the M0 E2E, attack and concurrency suites.
Parts:
 A. The long story (integration, FakeTimeProvider, frozen content): a new guest plays to the Town Charter over ten simulated days with
    absences, a server restart, a move, a demolition, a cancelled research, every chapter claimed and orders fulfilled; assert
    snapshots, database rows, receipts and zero ledger mismatches.
 B. Attack and concurrency: every new command against replay, another player's ids, impossible ids (OD-13), stale versions and races
    (claim against claim, fulfil against dismiss, move against a courier's stop, delete account against a command in flight).
 C. The matrix and the report: every invariant, error code and threat mapped to its tests; focused tests for each gap; an honest
    docs/SECURITY-REPORT-M1.md: what is guarded, what is open, what a tester with a rooted phone could still do. Never "cheat-proof".
Full run: at the finish, with the integration category and the Unity suites.
Acceptance: no gap in the matrix without a test or a written reason; the report is reviewed by the owner.
Out of scope: load testing beyond 50 concurrent players, penetration testing by a third party.
```

#### M1-17 — RuStore listing and closed alpha · [S] [ASSETS] [MANUAL]
```text
The game is in RuStore's closed alpha and invited testers can install it.
Read first: RuStore's current developer help (the requirements change: check them on the day, write what you found and the date into
docs/release/RUSTORE-CHECKLIST.md); owner decisions 1, 10, 11.
Parts:
 A. Checklist (written first, then walked): developer account and its verification; package name; target API level; permissions and
    the reason for each; the data declaration (what leaves the phone: the install secret, gameplay commands, error reports); age
    rating; privacy policy and user agreement in Russian at a public address (drafts for the owner to check with a lawyer; not legal
    advice); the support contact; what personal data is stored and where.
 B. Listing: name, short and full description in Russian, what's-new text, screenshots taken from the release build by a scripted
    capture, the icon and feature graphic from AR-M1-17.
 C. Alpha: [MANUAL] the owner uploads the build, invites testers (they need RuStore and a VK ID), and shares the feedback channel.
    docs/testing/ALPHA-PLAYTEST.md: the 7-day protocol, the questions asked on day 1, 3 and 7, how a bug is reported.
 D. Optional, each a G-Dependency the owner approves or drops: RuStore's in-app update SDK and review SDK.
Assets: AR-M1-17 (app icon, feature graphic, logo, screenshot frames).
Full run: none beyond the build check of M1-14.
Acceptance: an invited tester installs from RuStore and reaches the town; the checklist has a date and a source for every item.
Out of scope: public release, payments, ads, other stores.
```

#### M1-18 — Playtest fixes and M1 close-out · [O] [MANUAL]
```text
Seven days of real play turn into fixes, a verdict and a clean handoff.
Read first: docs/testing/ALPHA-PLAYTEST.md; the funnel report of M1-13; docs/balance/REPORT-era1-1.md.
Parts:
 A. Triage: every report becomes a row (P0–P3) with its source; fix P0 and P1, each with a failing test first where testable; P2 and P3
    go to the M2 backlog or a CR.
 B. Pacing against the simulator: compare observed times (each chapter, the Town Charter, coins per day) with the report; where they
    differ by more than 25 %, explain it and fix the simulator's strategies or the content (the owner approves number changes).
 C. Close-out: this plan's Definition of Done (§2) checked with evidence; the board all Done or Skipped; a full docs audit by hand
    (/docs-sync); CHANGELOG section "0.2.0 — M1"; docs/HANDOFF-M1.md (what exists, how to run and deploy, known issues, the open-beta
    go or no-go with the numbers behind it, the M2 backlog as items that can become CRs); resume point IDLE.
Full run: at the finish, everything.
Acceptance: the exit of §2 is met or the owner has written why M1 closes without it.
Out of scope: Era II work.
```

---

## 8. Assets (indicative; the real lists are the asset requests)

AI coders make no assets (`core-instructions/27-asset-pipeline.md`). Counts are estimates for budgeting; M1-01 part F replaces them with the
requests.

| Request | For | Roughly |
|---|---|---|
| AR-M1-03 | Terrain | 15 models: forest, rock, soil and water tiles with variants, mist, bridge, survey stakes |
| AR-M1-04 | Chronicle | 6 chapter illustrations, 6 icons |
| AR-M1-05 | Orders | 8 merchant portraits, 2 caravan models, 4 icons |
| AR-M1-06 | Decorations | 12 models, 12 icons |
| AR-M1-07 | UI skin | About 40 sprites (panels, buttons, tabs, bars, ribbons, seals), 12 glyphs, 2 font families with Cyrillic |
| AR-M1-08 | Feel | 4 music loops, 3 ambience loops, about 45 sound effects, 6 effect textures, 5 villager and animal models |
| AR-M1-09 | Era I buildings | About 20 models (6 new buildings, level 3 of all, the landmark's stages), 12 store fills and loads, about 25 icons |
| AR-M1-10 | Tutorial | 4 advisors × 3 expressions, a pointer, 3 premise cards |
| AR-M1-17 | Store | App icon, feature graphic, logo, screenshot frames |

Total: about 55 models, 130 pictures, 52 audio files, 2 fonts.

---

## 9. What not to build in M1

Era II and everything in it (market, guilds, the era transition ceremony); population needs; expeditions; real-time multiplayer; chat;
friends; leaderboards; premium currency, purchases, ads, offers; remote push; third-party analytics or attribution SDKs; iOS; more
languages than Russian and English; more than one map; XP, player levels or city levels in any form; any timer that punishes absence.
`core-instructions/30-out-of-scope.md` is rewritten from this list in M1-01.

---

## 10. Risks and honest trade-offs

| Risk | Effect | Guard |
|---|---|---|
| The scope is twice the old M1 | 9–12 weeks, not 4–5 | The cut order in §6; phases B and C are playable on their own |
| Generated assets don't match each other | The game looks stitched together | One style guide (M1-01 part E); requests name the reference pictures; the owner rejects in M1-09; one generator per asset kind |
| Orders and goals pour coins in | Research becomes trivial | Both are in the simulator before tuning (M1-15); the band for order prices is content data |
| A seeded function in a "no randomness" engine | A hidden source of non-determinism | Integer-only, spec'd in `04` with test vectors, covered by the determinism suite; `[PLAN]` |
| Bigger tasks, bigger PRs | Review is harder; a broken part hides longer | Parts end green and checkpointed; tests first; early full runs where a part changes shared rules |
| The path search on a bigger map | Advance over budget (308 ms worst case today) | Measured in M1-03 part A before any client work |
| Legal and store rules change | A late surprise before the alpha | The checklist is written on the day, with dates and sources (M1-17); the owner confirms law questions with a lawyer |
| A week of content for a store launch is thin | Players finish and leave | M1 ends in a closed alpha, not a public release; orders and decorations keep the town alive after the Charter; Era II is M2 |

## 11. Sources (checked 2026-11-16)
- RuStore help, alpha testing: https://www.rustore.ru/help/developers/publishing-and-verifying-apps/app-publication/testing/alpha-testing
- RuStore help, beta testing: https://www.rustore.ru/help/developers/publishing-and-verifying-apps/app-publication/testing/beta-testing
- RuStore SDK for Unity, in-app updates: https://www.rustore.ru/help/sdk/updates/unity/10-3-0
- RuStore SDK for Unity, payments: https://www.rustore.ru/help/en/sdk/pay/unity/history/7-0-0
- RuStore developer blog, Google's installation rules: https://www.rustore.ru/developer/blog/google-menyaet-pravila-ustanovki-android-prilozhenij-chto-eto-znachit-dlya-razrabotchikov-rustore
- The payment status rule (individual entrepreneur or company since 2026-02-01) and the data declaration come from search summaries of
  RuStore's pages; M1-17 part A re-checks both at the source.
