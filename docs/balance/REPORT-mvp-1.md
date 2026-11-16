# Balance report 1 — Era I slice (content `mvp-2`)

M0-68 (task M0-A). All three strategies × three check-in patterns, 72 h each, on `server/content/mvp-content.json`, compared with the goals
of `core-instructions/24-economy-design.md`. **No content was changed.** The proposed changes are at the end. The owner picks from them,
and M0-69 applies the picks, reruns, and appends the before/after numbers here.

- **Command:** `dotnet run --project tools/BalanceSim` (defaults: first check-in 30 min, later ones 5 min, 300 s presence grace).
- **Simulator:** as of the M0-A fix below (commit `87bcfe9`). The variants are copies of the content with the named numbers changed, run with
  `--content <copy>`. They are not in the repo.

## Summary
- **Goal 1, first research in the first minute: met.** Trade Charter starts at 0:00:30 in every run.
- **Goal 1, first coins in about 20 minutes: just missed (0:23:30 in every run).** The gate is the Trading Post's **10 planks**, which take
  about ten minutes of the first Sawmill. The Trade Charter research (done at 0:10:30) is not the gate: halving it changes nothing.
- **Goal 2, something worth doing every check-in: missed on day 1.** The starting 800 coins are spent in the first session (basic city +
  Trade Charter = 830 coins). With no courier station, nothing moves while the player is away, so check-ins 2 and 3 find 20–130 coins and
  full stores. The first courier station (120 coins) comes at the 3rd–4th check-in (12 h on "every 4h", 24 h on three a day).
- **Goal 3, the next research within one or two absences: missed.** The second research starts after 4 absences ("every 4h", at 16:00)
  or 5 ("08:00,13:00,20:00", at 29:00). "Once a day" never starts a second one.
- **Once a day is stuck:** 2 coins/h, one research in 72 h, 42 % of worker time idle. Without a courier station, the chain stops 20 minutes
  after each check-in. `24` expects "a check-in a few times a day", but this pattern still loses progress, not just output.
- **The late slice has nothing to buy.** On three a day and every 4 h, all six nodes are done by 33–62 h, and the purse ends at
  12 000–43 000 coins. M0's research is short by design (`23`: the slice is played in one sitting), so this is expected for M0 and is not a
  proposal here. Era I proper (3–5 days, `25`) needs more sinks.
- **The best levers, measured:** a starting purse of **1000** (P1) puts the courier station into the first session's budget. A Trading Post
  that costs **5 planks** (P2) brings first coins to 0:18:30. With both, goal 3 is met on three a day and every 4 h. An **8 h / 12 h**
  courier stamina (P3) is the only lever that helps the night absence and once a day.

## Baseline runs (`mvp-2`)

| Strategy | Check-ins | First coins | 2nd research starts | First courier station | Coins on day 1 | All research | Coins/h | Purse at 72 h | Idle worker time | Top stall |
|---|---|---|---|---|---|---|---|---|---|---|
| GreedyCoins | 08:00,13:00,20:00 | 0:23:30 | 29:00:00 ¹ | 24:01:00 | 160 | — ¹ | 250 | 15 030 | 13.5 % | `StorageFull:logs` 23.9 % |
| GreedyCoins | every 4h | 0:23:30 | 16:00:00 ¹ | 12:01:00 | 4 800 | — ¹ | 436 | 25 180 | 8.5 % | `StorageFull:logs` 28.2 % |
| GreedyCoins | once a day | 0:23:30 | — | — | 50 | — | 2 | 130 | 41.6 % | `MissingInput:planks` 33.1 % |
| ResearchRush | 08:00,13:00,20:00 | 0:23:30 | 29:00:00 | 24:01:00 | 160 | 61:07:38 | 359 | 22 020 | 12.6 % | `MissingInput:planks` 18.3 % |
| ResearchRush | every 4h | 0:23:30 | 16:00:00 | 12:01:00 | 4 780 | 33:07:20 | 689 | 43 550 | 9.9 % | `StorageFull:logs` 23.1 % |
| ResearchRush | once a day | 0:23:30 | — | — | 50 | — | 2 | 130 | 41.6 % | `MissingInput:planks` 33.1 % |
| Balanced | 08:00,13:00,20:00 | 0:23:30 | 29:00:00 | 24:01:00 | 160 | 62:11:12 | 216 | 12 200 | 22.3 % | `MissingInput:planks` 22.9 % |
| Balanced | every 4h | 0:23:30 | 16:00:00 | 12:01:00 | 3 500 | 37:09:04 | 417 | 23 740 | 14.6 % | `StorageFull:logs` 20.6 % |
| Balanced | once a day | 0:23:30 | — | — | 50 | — | 2 | 130 | 41.6 % | `MissingInput:planks` 33.1 % |

¹ GreedyCoins researches only the nodes that unlock or speed up the chain (3 of 6), by design.

Times are from the first check-in. "Coins on day 1" is the coins the Trading Post made in the first 24 h.

## Workers and stalls
- **Idle workers:** at least one worker sits idle almost all the time (95–100 % of the run) in every run. Until the first courier station,
  the strategies keep one worker free for hand deliveries. Houses also add workers four at a time, ahead of the slots they fill. Idle
  worker *time* is 8–15 % on every 4 h, 13–22 % on three a day, and 42 % on once a day (the chain is stalled, so workers wait).
- **Woodcutter's Hut:** `StorageFull:logs` 63–68 % of its time once couriers run. This is courier capacity, not store size: doubling the
  hut's store (40 → 80 logs) moves the share by under 1 point (variant `wc80` below).
- **Trading Post:** `MissingInput:planks` 62–78 %. One Trading Post sells 2 planks a minute, and one Sawmill makes 1. The chain is
  1 Trading Post : 2 Sawmills : 2 Huts. That is a small integer ratio (`24` → Pacing), and growing towards it is the strategies' job.
- **Couriers:** they run out of stamina 6–9 times in a run on three a day (the 11–12 h night absence against 4 h of stamina at level 1,
  8 h at level 2), and 0 times on every 4 h.

## Which research order wins
**Cheapest first, no upgrades until the research is done (ResearchRush), in every pattern a player checks in more than once a day.** It
makes the most coins per hour (359 and 689, against 216–250 and 417–436) and finishes the research first. Two reasons:
- Upgrades cost planks, the Trading Post's input, so early upgrades take planks away from coins. ResearchRush defers them.
- GreedyCoins skips the logistics nodes (Sturdy Boots, Storehouses, Tally Sticks) because they don't touch the chain. With couriers as the
  limit, those nodes are worth more than GreedyCoins assumes: it ends 30–40 % below ResearchRush.

On once a day, the order makes no difference: nothing runs.

## Variants measured
Each variant is the `mvp-2` content with only the named numbers changed. Columns: first coins (best–worst strategy), the second research
on three a day / every 4 h (Balanced), coins/h (Balanced / ResearchRush / GreedyCoins) on three a day, and once a day.

| Variant | Change | First coins | 2nd research, 3×day / 4h | Coins/h, 3×day (B / RR / G) | Coins/h, once a day (B / RR / G) |
|---|---|---|---|---|---|
| baseline | — | 0:23:30 | 29:00 / 16:00 | 216 / 359 / 250 | 2 / 2 / 2 |
| `tc300` | Trade Charter 600 → 300 s | 0:23:30 | 29:00 / 16:00 | 216 / 359 / 250 | 2 / 2 / 2 |
| `cs60` ³ | Courier Station 120 → 60 coins | 0:23:30 | 24:00 / 12:00 | 281 / 440 / 333 | 19 / 19 / 19 |
| `tp5` | Trading Post 10 → 5 planks | 0:18:30 | 29:00 / 16:00 | 217 / 361 / 252 | 2 / 2 / 2 |
| `p950` | starting purse 800 → 950 | 0:23:30–0:24:40 | 29:00 / 16:00 | 184 / 385 / 447 | 2 / 2 / 117 |
| `p1000` (**P1**) | starting purse 800 → 1000 | 0:23:30–0:24:40 | **0:10:30** / **0:10:30** | 301 / 514 / 447 | 19 / 19 / 117 |
| `p1000_tp5` (**P1+P2**) | P1, Trading Post 5 planks | **0:18:30–0:19:40** | 0:10:30 / 0:10:30 | 324 / 582 / 451 | 35 / 35 / 118 |
| `p1000_tp5_wc80` | P1+P2, Hut store 80 / 120 logs | 0:18:30–0:19:40 | 0:10:30 / 0:10:30 | 445 / 584 / 451 | 35 / 35 / 118 |
| `p1000_tp5_stam8h12h` (**P1+P2+P3**) | P1+P2, station stamina 8 h / 12 h | 0:18:30–0:19:40 | 0:10:30 / 0:10:30 | 390 / 701 / 511 | 68 / 68 / 201 |

³ Run on the simulator before the M0-A fix; the fix changes Balanced only when it has coins to spare, so `cs60`'s row may move by a
few percent.

What the third research does with P1+P2, which is the goal-3 measure from then on: it starts at 12:00 on three a day (two absences) and at
8:00 on every 4 h (two absences). On the baseline the third research starts at 36:00 and 20:00. With P1 the courier station lands at
0:12 for GreedyCoins (in the first session). Balanced and ResearchRush spend the extra coins on the second research first, so their
station comes at the next check-in (5:04 with P2, 12:03 without).

## Proposed number changes
For the owner to pick from (M0-69 applies the picks). Each one is a content-only change (`19` → Content-only change).

| # | Change (`mvp-content.json`) | Expected effect, from the variants | Risk |
|---|---|---|---|
| **P1** | `startingCity.purse` 800 → **1000** | The first session can afford the courier station, or the second research. Day-1 coins on three a day: 160 → 1 400–8 500. Second research: after 5 absences → in the first session. All research: 62 h → 51 h (Balanced, three a day). | The first session has more to choose from; the station still isn't forced, so a player who skips it stays slow (a tutorial question, not a number). |
| **P2** | `trading_post` cost planks 10 → **5** | First coins 0:23:30 → **0:18:30** (goal 1). On top of P1: station at 5:04 instead of 12:03 for Balanced and ResearchRush; all research 51 h → 40 h (Balanced, three a day). | Alone it moves only first coins (`tp5`). |
| **P3** | `courier_station` `staminaSeconds` L1 14400 → **28800**, L2 28800 → **43200** | The night absence (11–12 h) and once a day keep moving goods longer. On top of P1+P2: once a day 35 → 68 coins/h (Balanced), three a day 324 → 390 coins/h. Level 2 keeps a reason to upgrade (12 h). | Halves how often stamina matters; `22`/`21` want check-ins a few times a day to be enough, not required, so this fits, but it weakens the stamina decision. |
| — | Not proposed: Trade Charter 600 → 300 s | No effect (`tc300`): the planks gate first coins, not the research. | — |
| — | Not proposed: Courier Station 120 → 60 coins | Smaller gains than P1 (`cs60`): about 20 coins are left after the first session, so even 60 doesn't fit. | — |
| — | Not proposed: Woodcutter store 40 → 80 | Under 1 point less `StorageFull:logs` (`wc80`): couriers, not stores, are the limit. | — |

**Recommendation: P1 + P2.** Together they meet goals 1 and 3 for players who check in more than once a day. Add P3 if the owner wants
once a day and the night absence to keep earning (it is the only lever that moves them). Once a day still fails goal 3 with all three:
the second research waits for the next day's check-in. Whether that is acceptable is a design question for `24` (owner).

## Owner's picks
2026-11-17: "I approve P1 and P2, and P3 should be a research for later". M0-69 (M0-A part C) applies P1 and P2. P3 is CR-0012
(Proposed): a research that raises courier stamina, instead of new content numbers. The once-a-day question stays open (owner queue).

## Simulator findings
- **Fixed in M0-A (regression test `ConstructionTests`, commit `87bcfe9`):** with spare coins, Balanced ordered an upgrade with goods before
  the basic city stood. First it was the only Woodcutter's Hut, whose planks come from the Sawmill, whose logs come from that hut, and a
  waiting building gets no workers. Then it was a house upgrade that took the logs the Trading Post's planks needed. With a purse of 1000
  its first coins came at 12:06 instead of 0:23:30. Upgrades with goods now wait until every chain type stands, and until the goods can
  reach them without the building itself. On `mvp-2` only Balanced on every 4 h moved: all research 33:27 → 37:09, coins/h 387 → 417.
- **Limits that shape the numbers above** (`docs/modules/BalanceSim.md` → Known limitations): no strategy builds a Warehouse or roads;
  the courier station is the last building of the basic city, so the strategies learn the value of a first-session station only from the
  coins (GreedyCoins with P1 builds it at 0:12, the others spend on research first).

Last updated: M0-68 (M0-A)
