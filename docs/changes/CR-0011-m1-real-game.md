# CR-0011 — M1: from a tech demo to a game (direction and plan)

- **Status:** Approved
- **Type:** Design change
- **Requested by:** owner · **Date:** 2026-11-16
- **Milestone / era:** M1 / Era I
- **Conflicts with pillars (`22`) or the era rule (`25`):** none with the pillars (no XP, no city level, no tapping for income). The era
  rule needs the owner: the Chronicle, merchant orders and terrain are new systems, so they can only enter as part of **Era I's base
  set**, by the owner's decision, as logistics did in CR-0008.

<!-- Status values: Proposed | Approved | In progress | Done | Rejected | Superseded.
     Only the owner moves a CR to Approved. No code before that. -->

## Problem / goal
The owner, 2026-11-16: "This game should be interesting to play, I want to publish it in local russian app store (rustore). Now I see
that game has no purpose, no incentive for player to start and keep playing. Game has no soul. It is boring, like a tech demo. What
should be added? Game should look and feel like a real game. Make a development plan for age M1, similar to M0, but make each
individual task bigger."

M0 built a correct machine and no reason to use it. A new player gets an empty lawn and 800 coins; nobody says who they are or what the
town is for; one chain and six research nodes are the whole game; the first coins take about 20 minutes; nothing makes a sound or
celebrates; the town has no name; nothing calls the player back; the text is English and the server is on localhost.

## Proposal
The full plan is `docs/plan/M1-PLAN.md` (18 tasks, each one to three days). In short, M1 adds what the game lacks, in this order of
weight:

1. **A purpose.** A premise (the Duke's steward founds a town at a river crossing), a long goal (earn the **Town Charter**, the end of
   Era I) and the **Chronicle**: five chapters of goals with rewards. The player always knows the next goal.
2. **A first session that hooks.** Chapter 1 is a guided tutorial with an advisor; something finishes every 30–90 seconds in the first
   ten minutes.
3. **Variety.** **Merchant orders** at the Trading Post and a daily caravan: named merchants want different goods, so the question
   "what do I make today?" has a new answer every day. No penalty for a skipped day.
4. **Content.** The whole Era I: three chains (wood, stone, bread), about 13 buildings with three levels, 14–16 research nodes in four
   branches, land expansion, builder slots, a landmark.
5. **A map with character.** Forest, rock, fertile soil and water; raw producers need their terrain; surveyed land reveals more.
6. **Feel.** Music, ambience and a sound for every action; animation and effects; a moment for every finished thing; townsfolk,
   animals, day and night.
7. **Ownership.** A town name, move and demolish, decorations.
8. **Return hooks.** Local notifications from the real timers; a "what happens next" strip.
9. **A product.** Russian first, a UI skin, settings, guest accounts with recovery and deletion, a hosted server, a release Android
   build, and a **closed alpha in RuStore**.

What does not change: the six pillars, the server's authority, the deterministic integer engine, numbers only in content.

## Acceptance criteria
- [ ] The owner answered the 16 decisions in `docs/plan/M1-PLAN.md` §4 (recorded in `core-instructions/12-decisions.md` by M1-01).
- [ ] The owner approved the plan as a whole, or with named changes; its status line says `Approved`.
- [ ] `core-instructions/10-milestones.md` and `30-out-of-scope.md` carry the new M1 (done by M1-01, not by this CR).
- [ ] The Definition of Done of the plan (§2) at the end of M1-18.

## Out of scope
- Era II (market, guilds, the era transition), monetization, ads, iOS, a public release: see the plan's §9.
- Any code or design-file edit before approval. This CR adds only itself and the plan.

## Impact analysis
| Area | Impact | Files / docs |
|---|---|---|
| Engine (`04`) | Three research effects, terrain and bounds, goals and counters, a seeded order generator, move, demolish, cancel research | `shared/Simulation/`, `04` |
| Content (`23`, JSON) | A new content file for the full Era I, a map, goals, order tables, decorations; a new spec `28-era1-content.md` | `server/content/`, `28` |
| API (`05`) | About eight new commands (claim goal, fulfil and dismiss order, move, demolish, rename, cancel research, account); snapshot fields | `05`, generated reference |
| Data / migration (`06`) | Terrain bounds, goals, orders, decorations, town name, guest accounts | `06`, migrations |
| Security (`07`) | Guest auth replaces dev login; a threat row and `Security_` tests per new command; account deletion | `07` |
| Client (`09`) | Chronicle, order board, settings, tutorial, localization, audio, moments, notifications, release build | `09`, `client/` |
| Docs (recipes in `19`) | `10`, `21`, `22`, `24`, `25`, `30`, `31`; module docs; new how-tos; operations runbook | per task |
| Tests (`08`) | Written before the code in every task (workflow v2); new golden #3; cross-system suites in M1-16 | `08`, test projects |
| Process | Moves hosting and the release build from M3 into M1; M1 grows from 4–5 to 9–12 weeks | `10` |
| Assets | About 55 models, 130 pictures, 52 audio files, 2 fonts, made by another AI model from asset requests | `27`, `docs/assets/` |

## Risks and alternatives
- **Scope:** twice the old M1. The plan gives a cut order (§6) and each phase is playable on its own.
- **Alternative A, content only** (the old M1 of `10`): cheaper, but more buildings do not give a reason to play; the game would stay a
  bigger tech demo.
- **Alternative B, Era II first** (market and guilds as the "soul"): the riskiest milestone on top of a first hour nobody finishes.
- **Alternative C, publish M0 plus polish:** a week of work, and reviews that say what the owner already said.
- Other risks and their guards: the plan's §10.

## Tasks (filled in after approval)
The tasks are the plan's: `M1-01` … `M1-18` (`docs/plan/M1-PLAN.md` §6). Before them, the rest of M0 (§3 of the plan).

## Owner approval
- Approved on: 2026-11-23, the plan as a whole with the recommendations of §4. The owner's words: "I approve it. Reference phone is
  Google Pixel 5. Pick name later in M1-01. Hosting provider name is irrelevant to the building process, am i wrong?"
  - Decision 9: the reference phone is the **Google Pixel 5** (Android 8.0+ stays the range).
  - Decision 6: the game's name is picked in M1-01 from its candidates.
  - Decision 10: the provider is chosen in M1-13 (its plan, part A); code before it stays provider-neutral. The Russian-cloud
    recommendation (152-FZ) stands.
  - M1-01 writes all 16 answers into `core-instructions/12-decisions.md` and the design files.
- In part, 2026-11-17: the regrouping of the last M0 tasks (plan §3, decision 12), the owner's words: "now we will use M1 plan file instead of M0. The rest of the tasks are regrouped in M1 with references to M0 file".
- Renumbered from CR-0009 on 2026-11-17: `main` already had CR-0009 (player metrics, PR #96).
