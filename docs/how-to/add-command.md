# Add a player command

A command is something the player does to their own city: place, upgrade, assign workers, start research (`core-instructions/05-api-contract.md`
→ Endpoints). Every command runs through one pipeline, `CommandPipeline` ([ADR-0003](../adr/ADR-0003-command-endpoints.md),
[ADR-0004](../adr/ADR-0004-idempotency.md)): shape check, the city's row lock, the receipt check, settle to now, the mutation, the closing
advance, one save of the city, its ledger rows and the receipt. A new command adds its data and its rule, never its own write path.
Recipe "New command" in `core-instructions/19-change-recipes.md` is the checklist; this guide walks through it with PlaceBuilding (M0-29).

## Before you start
- The command needs a plan task or an approved CR (`core-instructions/15-change-control.md`). Its endpoint and body are an API contract:
  write them into `05` first; changing them once a client exists is G-Contract.
- The body holds only what the player chose. Costs, levels, times, worker totals, timestamps and player ids never come from the client
  (`core-instructions/02-principles.md`, rule 2): the server reads them from the content and the city.
- The rule is the engine's (`shared/Simulation/Runtime/Rules/CityRules.cs`, `04` §8), tested there with every error code. If the rule is new,
  that is recipe "New engine rule or effect type" first.

## Worked example: PlaceBuilding
`POST /api/v1/commands/place-building` with `{ "requestId": "…", "buildingTypeId": "woodcutter", "x": 2, "y": 3 }`.

**1. The engine rule.** `CityRules.PlaceBuilding(city, content, typeId, x, y)` exists (M0-08): it checks in the order of `04` §8, pays
the cost, starts construction and returns a `MutationResult` with what it spent, or a `MutationErrorCode`. Business-rule failures are
results, never exceptions.

**2. The command.** `server/src/CityBuilder.Application/Commands/PlaceBuilding/PlaceBuildingCommand.cs`: an immutable record that implements
`ICityCommand`. Its properties are its data, and they are its canonical payload, which is hashed into the receipt (`CommandPayload`).
```csharp
public sealed record PlaceBuildingCommand(Guid RequestId, string BuildingTypeId, int X, int Y) : ICityCommand
{
    public const string Type = "place-building";            // stored in the receipt; the last segment of the path
    public string CommandType => Type;
    public LedgerReason? CostReason => LedgerReason.CostPlace; // the ledger reason of Spent
    public LedgerReason? RefundReason => null;               // the ledger reason of Refunded (an upgrade has REFUND_UPGRADE)
    public string? ValidateShape() =>                        // text that can't be a content id never locks the city
        ContentIds.IsValid(BuildingTypeId) ? null : "buildingTypeId must be a content id: " + ContentIds.FormatDescription + ".";
    public MutationResult Apply(CityState city, GameContent content) =>
        CityRules.PlaceBuilding(city, content, new BuildingTypeId(BuildingTypeId), X, Y);
}
```
`ValidateShape` refuses only what no city state could make valid: an id that can't be a content id (`ContentIds`, the format the content
loader enforces), checked before the lock (M0-30). Everything else is the rule's job: coordinates outside the
grid are `OUT_OF_BOUNDS`, not a shape error. A renamed property or command type changes every stored hash, and an old request id then
conflicts, so pin the payload in a test.

`UpgradeBuildingCommand` (M0-31) is the same with `BuildingId` as its data, `COST_UPGRADE` and a refund reason, `REFUND_UPGRADE`, for a
running cycle's inputs; its `ValidateShape` refuses an id below 1, which no city has, and the rule answers `BUILDING_NOT_FOUND` for an id
of 1 or more that the city doesn't have. `AssignWorkersCommand` (M0-32) pays and refunds nothing, so both
its ledger reasons are `null`; its `ValidateShape` checks two values (an id below 1, a negative count) and names the first one that is wrong. `StartResearchCommand`
(M0-33) pays `COST_RESEARCH` and checks its `researchId` with `ContentIds` as PlaceBuilding checks its type id; its request has no number,
so it carries `Disallow` and `JsonRequired` only (step 3).
The rule for every command is owner directive OD-13 (`core-instructions/15-change-control.md`): a
value that no city and no content could make valid (an id below 1, a negative coordinate or count, text that can't be a content id) is
refused in `ValidateShape` or the binding, before the lock, with a test that the lock was never taken; whatever depends on the city or the
content stays the rule's.

**3. The request.** `server/src/CityBuilder.Api/Contracts/PlaceBuildingRequest.cs`: the body as a record with three attributes, so the binding
refuses anything that isn't the contract (400) before the pipeline runs:
- `[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]`: an unknown property (`cost`, `level`, `playerId`) is refused, not ignored;
- `[property: JsonRequired]` on every field: a missing `x` would otherwise read as 0;
- `[JsonNumberHandling(JsonNumberHandling.Strict)]`: the web defaults would read `"2"` as 2.

**4. The endpoint.** In `server/src/CityBuilder.Api/Endpoints/CommandEndpoints.cs`, one line in `MapCommands()` and a handler that only turns
the body into the command:
```csharp
commands.MapPost("/place-building", PlaceBuildingAsync).Produces<CitySnapshot>(StatusCodes.Status200OK, "application/json");
…
var command = new PlaceBuildingCommand(request.RequestId, request.BuildingTypeId ?? string.Empty, request.X, request.Y);
return await ExecuteAsync(command, player, pipeline, content, json, cancellationToken);
```
`ExecuteAsync` is shared by every command: it runs the pipeline for the token's player with the snapshot as the response (`CitySnapshotMapper`,
serialized inside the transaction and stored in the receipt as text) and maps the outcome with `CommandResponses.ToHttpResult`: 200 with
the snapshot, also for a replay (the stored bytes); Problem Details with `code`, `requestId` and `traceId` otherwise. The handler writes
the snapshot as text, so `.Produces<CitySnapshot>` gives the OpenAPI document its 200 schema (a test checks the document).
The `/commands` group is in the commands rate-limit class (`RequireRateLimit`, M0-34), so the new route is limited with the others and a
concurrency conflict is already answered; `RateLimitTests.Security_EveryApiEndpoint_IsInItsLimitClass` lists the API's endpoints and fails
until the new route is in its table.

**5. The errors.** `ErrorCatalogue.Of` (`Api/Errors/`) maps every `MutationErrorCode` to its `05` code and status, so a command that only
uses existing codes adds nothing there. A new code needs its row in `05`, its engine value, its member in `ErrorCatalogue` and in `All`, and
its rows in `CommandErrorMappingTests` and `ErrorCatalogueTests`, which fail until all exist (recipe "New error code").

**6. Tests.** One file per level, each named after the command:
- `CityBuilder.Application.Tests/Commands/PlaceBuildingCommandTests.cs`: the shape check, the pinned payload and hash (computed outside
  the code: `printf 'place-building\n{…}' | shasum -a 256`), the ledger reasons, `Apply` equal to the rule, one pipeline run on the fakes.
- `CityBuilder.IntegrationTests/Contracts/PlaceBuildingContractTests.cs` (no Docker, `FakeCityStore`): the success snapshot as raw JSON and
  equal to `GET /city`'s; replay and conflict; every forbidden property and wrong shape → 400 with nothing run (`Security_` for the fields an
  attacker would send).
- `CityBuilder.IntegrationTests/Commands/PlaceBuildingDatabaseTests.cs` (PostgreSQL over HTTP): success with the stored rows, the ledger and
  the receipt; every error code with nothing stored; replay; conflict; concurrent identical requests; another player's request id; the
  settle before the mutation; what a later `GET /city` shows. Assert database state and ledger rows, not only status codes.
- Add the path to `UnauthenticatedContractTests` (401 without a token).
- Mutate the code once (a wrong ledger reason, swapped coordinates) and see a test fail; restore it.

**7. Docs.** `05` (endpoint, body, errors), `07` (threat rows that name the `Security_` tests), `04` §8 if the rule changed, the module docs of
Application and Api, `CHANGELOG.md`, and this guide if a step changed.

## Common mistakes
- **A write outside the pipeline.** An endpoint that loads, changes and saves a city itself skips the lock, the settle, the ledger and the
  receipt. If a command doesn't fit the pipeline, stop and ask (`.claude/rules/server.md`).
- **A value from the client.** A body field that the server should compute (a cost, an end time) is a cheat. Leave it out of the record;
  `Disallow` refuses it.
- **The clock in the command.** `Apply` runs at `city.Clock`, after the pipeline settled to the server's now; a command never reads a clock.
- **A field without `JsonRequired`.** Its default (0, `Guid.Empty`) becomes a real value.
- **A new code without its `05` row.** The table test fails; the client would not know it.
