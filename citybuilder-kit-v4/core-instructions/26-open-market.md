# Open Market — player-to-player trading (Era II design)

> **Not built in M0.** This file exists so the M0 foundation doesn't block the market later.
> The section "What M0 must already respect" is binding now. Everything else is the design for M2.

## Concept
An MMO-style exchange. Players list goods at their own prices and buy from each other, with coins as the
only currency. The market unlocks when a city enters **Era II (High Middle Ages)**. That keeps the first
days simple, and it also makes throwaway alt accounts expensive to farm.

## What M0 must already respect (binding)
1. Every inventory change goes through the ledger with a reason. The market will add `MARKET_*` reasons.
2. A transaction locks and writes **only the acting player's city**. Effects on other players go to a
   **mailbox** that their own next settle applies (`04` §13). Never lock two cities in one transaction.
3. Items carry `era` and `tradable` in content. Coins are the currency, not a tradable item.
4. Storage caps limit production only. Refunds and deliveries may exceed caps.
5. Prices, fees and quantities are integers (`long` coins per unit). There is no fractional money anywhere.
6. Player identity is separate from the city (`players` ≠ `cities`). Market orders will belong to players.

## Order book
- Per item: **sell orders** (asks) and **buy orders** (bids), matched by price, then time.
- "Buy now" fills against the cheapest asks. "Sell now" fills against the highest bids.
- Partial fills are allowed. An order lasts 7 days; on expiry, the remaining escrow returns through the mailbox.
- Delivery is instant and global (one market per server). Shipping and logistics could come in a later era.

## Escrow and settlement
- Placing a sell order moves the goods from the city's inventory into escrow. Placing a buy order moves `price × qty` coins into escrow.
- On a match, the acting player's side is settled in their own transaction. The counterparty's goods or coins
  go to their mailbox, timestamped with the trade time.
- At the counterparty's next settle, mailbox rows enter `Advance` as timed deliveries, so items arrive "when they were traded".
- Cancelling an order returns escrow immediately to the acting player. The listing fee is not refunded.

## Concurrency
- Matching for an item is serialized with a transaction-scoped advisory lock per item
  (`pg_advisory_xact_lock(item_key)`).
- Lock order is always **own city row → item lock → order rows in ascending id**. Never lock another city.
- Every order command is idempotent (`requestId`). Every fill writes ledger rows on both sides plus a `market_trades` row.

## Economy controls
| Control | Proposal | Why |
|---|---|---|
| Listing fee | 1 % of order value (min 1 coin), not refunded | Coin sink; discourages order spam |
| Sales tax | 4 % of proceeds, paid by the seller | Main coin sink of the market |
| Price band | Orders must be within 0.25×–4× of the item's reference price | Blocks wealth transfer between alts at absurd prices |
| Reference price | 7-day volume-weighted average, seeded from the NPC Trading Post value | Moves with the real market |
| Open-order limit | 10 per player, more via research | Limits spam and manipulation |
| Access | Era II and an account at least 72 h old | Raises the cost of alt farms |

**The trade-off is real.** Price bands limit the owner's "players set their own prices" freedom at the extremes.
The proposal keeps a wide band (16×) and should be tuned with live data.

## Liquidity at launch (the biggest practical risk)
A player market needs many active players. With few players the order book is empty and the feature feels dead.
- **NPC floor:** Trading Posts always buy basic goods for coins, which caps how low prices can go.
- **NPC ceiling:** a "Royal Warehouse" sells basic Era I–II goods at about 3× the reference price, which caps prices and prevents cornering.
- Show price history and the last trades even when the book is thin.

## Abuse and integrity
- Duplicate goods via races → item locks, escrow, the ledger auditor, and a kill switch per item.
- Alt-account funneling → price bands, account-age gate, trade-graph analysis, no direct gifting.
- Real-money trading → terms of service, monitoring for large one-sided trades, and bans.
- Bots sniping cheap listings → per-player rate limits. Matching is time-priority, not reaction speed.
- An economy incident needs rollback tools based on the ledger and an operator console (later milestone).

## Screens (Era II)
Item list with last price and 24 h change · order book per item (top 5 asks and bids) · place order
(price stepper, band shown) · my orders · trade history with a price chart · mailbox/notifications.

## Data (M2, for orientation only)
`market_orders`, `market_trades`, `market_escrow` (or escrow columns on orders), `mailbox_deliveries`,
`item_price_history` (hourly OHLC + volume).
