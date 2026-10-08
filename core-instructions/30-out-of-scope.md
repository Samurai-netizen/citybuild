# Out of Scope (M0)

If a prompt seems to need any of these, stop and ask instead of building it.
"Designed" means the docs describe it so the foundation doesn't block it, but no code is written.

## Gameplay
- More than the 3 items, 4 buildings, 2 levels and 4 research nodes in `23-mvp-content.md`
- Era transitions and any Era II+ content (designed in `25`, built in M2)
- Research effect types beyond `UnlockBuilding`, `LaborReduction` and `StorageCapBonus`
- A second research slot, cancelling research, or speeding research up
- Land expansion, terrain, multi-tile buildings, roads, adjacency bonuses
- Demolition or moving buildings; more than one city per player
- Multi-recipe buildings, fuel, power, automation, needs, expeditions (later eras)
- Random events or disasters
- A separate pause feature (assigning 0 workers *is* pausing)

## Social and online
- **Open market** (designed in `26`, built in M2), guilds, friends, visits, gifts, chat, leaderboards
- Mailbox and cross-player transfers (designed in `04` §13 and `26`)
- Realtime connections (WebSockets, SignalR) or multiplayer of any kind

## Business
- Premium currency, speed-ups, IAP, ads, offers, subscriptions
- Analytics SDKs, push notifications, remote config services

## Tech
- Redis, message queues, background workers, microservices
- Cloud deployment, Kubernetes, CD pipelines, server container images (moved to M3). CI on GitHub *is* in scope (`18`)
- Unity tests in CI (they need a runner license); run them locally
- Real account systems, OAuth, platform sign-in, device attestation
- Addressables, asset bundles, localization frameworks
- Custom DI containers, ECS/DOTS, networking frameworks (Netcode, Photon, Mirror)

## Art and polish
- Final art, animations beyond simple tweens, VFX beyond simple smoke, audio, haptics
- Era looks beyond Era I placeholders, day/night, weather, walking workers, camera cinematics

## Process
- Building anything without a plan task or an owner-approved CR (`15`)
- Bypassing git hooks, CI, the Stop hook or protected-file approvals

## Never in scope, in any milestone
- XP, player levels or city levels
- Endpoints that set state directly ("give coins", "set level", "finish research"), even in development
- Client-side calculation of anything the server stores
- Locking or writing another player's city inside a transaction
- Copying names, art, text or UI from Tap Tap Builder or other games
