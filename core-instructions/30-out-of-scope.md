# Out of Scope (M1)

If a prompt seems to need any of these, stop and ask instead of building it.
"Designed" means the docs describe it so the foundation doesn't block it, but no code is written.
M1 is `docs/plan/M1-PLAN.md` (its §9 is the source of this list); what M0 left out and M1 builds is in `10` → M1.

## Gameplay
- More than the Era I content of `28-era1-content.md` and `29-era1-chronicle.md`
  (items, buildings, levels, research, map, chapters, orders, decorations)
- Era transitions and any Era II+ content (designed in `25`, built in M2); after the Town Charter the town only keeps running (G38)
- Research effect types beyond those `25` marks M0 or M1; a second research slot; speeding research up
- More than one map; maps per player; terrain that runs out or changes; multi-tile buildings; adjacency bonuses
- Logistics beyond Era I: Era II amounts and roads, carts, wagons, trucks, couriers blocking each other, loading time, diagonal steps,
  waiting at a stop for goods, cancelling a construction project, science building levels above 1
- More than one city per player; any loss of progress for being away; orders or caravans with a penalty or a countdown
- Multi-recipe buildings, fuel, power, automation, population needs, expeditions (later eras)
- Random events or disasters

## Social and online
- **Open market** (designed in `26`, built in M2), guilds, friends, visits, gifts, chat, leaderboards
- Mailbox and cross-player transfers (designed in `04` §13 and `26`)
- Realtime connections (WebSockets, SignalR) or multiplayer of any kind

## Business
- Premium currency, speed-ups, purchases, ads, offers, subscriptions (G34)
- Third-party analytics or attribution SDKs, remote push servers, remote config services (G31, G35); server-side player events (CR-0009)
  and a client error report endpoint are in scope
- A public release: M1 ends in a RuStore closed alpha (G24)

## Tech
- Redis, message queues, microservices; background workers beyond what M1-13's operations plan approves
- Kubernetes, CD pipelines (M3). Hosting one API with managed PostgreSQL in a Russian cloud *is* in scope (M1-13, G33)
- Unity tests in CI (they need a runner license); run them locally
- Platform sign-in, OAuth, device attestation (guest accounts with recovery and deletion *are* in scope, M1-12)
- Addressables, asset bundles; localization beyond Russian and English
- iOS builds
- Custom DI containers, ECS/DOTS, networking frameworks (Netcode, Photon, Mirror)

## Art and polish
- Era looks beyond Era I, weather, camera cinematics
- Assets made by the coder: every asset comes from an asset request (`27`, W18)

## Process
- Building anything without a plan task or an owner-approved CR (`15`)
- Bypassing git hooks, CI, the Stop hook or protected-file approvals

## Never in scope, in any milestone
- XP, player levels or city levels
- Endpoints that set state directly ("give coins", "set level", "finish research"), even in development
- Client-side calculation of anything the server stores
- Locking or writing another player's city inside a transaction
- Copying names, art, text or UI from Tap Tap Builder or other games
