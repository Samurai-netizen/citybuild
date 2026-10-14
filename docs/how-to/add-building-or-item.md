# Add a building or an item (content-only change)

A content-only change edits game data and no code: numbers, or a new item, building type or research node that uses the existing effect types
(`unlockBuilding`, `laborReduction`, `storageCapBonus`). The checklist is recipe "Content-only change" in `core-instructions/19-change-recipes.md`.
A new effect type or a new rule needs engine code instead: recipe "New engine rule or effect type" and `add-research-effect.md` *(planned, M0-09)*.

## What you change
| What | Where |
|---|---|
| The data | `server/content/mvp-content.json`, the only content file. Nothing under `src/` changes |
| The content version | `contentVersion` in the same file: every change gets a new one (`mvp-1` → `mvp-2`), so clients refetch `GET /content` (`05`) |
| The design numbers (during M0) | `core-instructions/23-mvp-content.md`, in the same commit: the JSON is the source of truth, `23` describes it |
| History | one line in `CHANGELOG.md`; a balance note in the build log entry |
| Generated reference | `docs/reference/generated/content.md`, regenerated *(planned, M0-61)* |
| Balance check | the balance simulator, `run-balance-simulator.md` *(planned, M0-67)* |

Golden scenario tests use a frozen copy of the content, so they don't change with balancing (`.claude/rules/tests.md`).

## The format in short
- The file is the engine's `GameContentData` in camelCase. Each field, whether it is required and its unit is in the doc comments of the
  `*Data` classes (`shared/Simulation/Runtime/Content/Data/`); the format decision is row 028 in `core-instructions/12-decisions.md`.
- Names are case-sensitive. An unknown field, a repeated key or a number written as text is an error, so a typo can't be silently dropped.
- Item amounts are objects keyed by item id: `"cost": { "coins": 150, "logs": 30 }`.
- Levels carry their number. Level 1 has no `upgradeCost` or `upgradeSeconds`; every level from 2 on has both: the cost and time to reach it.
- A building type says `"availableAtStart"`. A locked one is unlocked by exactly one research node's `unlockBuilding` effect.
- Optional, and left out when empty or 0: `inputs`, `prerequisites`, `effects`, `maxWorkers`, `workersProvided`. Everything else is required.
- The rules every file must pass (positive amounts, unique ids, existing references, storage caps, unlocks, no research cycle, the
  shortest cycle) are `core-instructions/04-simulation-model.md` §14.

## Worked example: stone from a quarry
Goal: a new capped item `stone`, made by a `quarry` that is locked until the research `masonry`. The numbers below only show the shape; they
are not a design.

**1. The item** (in `items`). `capped` means production stops at the storage cap; `tradable` is for the market from Era II.
```json
{ "id": "stone", "name": "Stone", "era": 1, "capped": true, "tradable": true }
```

**2. Its storage cap.** A capped item needs exactly one base cap (`04` §14 rule 4), in `rules.baseStorageCaps`:
```json
"baseStorageCaps": { "logs": 500, "planks": 500, "stone": 300 },
```

**3. The building** (in `buildings`). A producer has worker slots and a recipe on every level; labor is in worker-seconds per cycle.
```json
{
  "id": "quarry",
  "name": "Quarry",
  "kind": "producer",
  "era": 1,
  "availableAtStart": false,
  "cost": { "coins": 120, "logs": 20 },
  "buildSeconds": 90,
  "levels": [
    { "level": 1, "maxWorkers": 2, "recipe": { "outputs": { "stone": 1 }, "labor": 90 } },
    {
      "level": 2,
      "maxWorkers": 4,
      "recipe": { "outputs": { "stone": 1 }, "labor": 90 },
      "upgradeCost": { "coins": 200, "planks": 20 },
      "upgradeSeconds": 150
    }
  ]
}
```

**4. The research that unlocks it** (in `research`):
```json
{
  "id": "masonry",
  "name": "Masonry",
  "era": 1,
  "cost": { "coins": 200 },
  "durationSeconds": 900,
  "prerequisites": ["trade_charter"],
  "effects": [{ "type": "unlockBuilding", "building": "quarry" }]
}
```

**5. The version:** `"contentVersion": "mvp-2"`.

**6. Check it.** Either command loads the real file through the same loader as the server:
```bash
dotnet test server/tests/CityBuilder.IntegrationTests --filter "FullyQualifiedName~ContentFileLoaderTests.Load_RealMvpContent"
dotnet run --project server/src/CityBuilder.Api    # logs "Content mvp-2 loaded from …", or stops listing every error
```
To try a draft without touching the real file, point the server at it: `dotnet run --project server/src/CityBuilder.Api -- --Content:FilePath=/path/to/draft.json`.

**7. Docs:** the new rows in `23` (Items, Buildings, Recipes, Upgrades, Research), the CHANGELOG line, the balance note.

## When the check fails
The loader lists every rule error at once, each with a code, a path in the JSON's own names and a message. This example without step 2
and with an empty `effects` list in step 4 gives:
```text
ContentLoadException: The content file '…/mvp-content.json' can't be loaded (2 errors):
  - StorageCapMismatch at rules.baseStorageCaps.stone: stone is capped, so it needs a base storage cap.
  - UnlockMismatch at buildings[quarry]: quarry is not available at the start, but no research node unlocks it (an unlockBuilding effect).
```
A JSON problem (a syntax error, a wrong type, an unknown field) is reported alone, because the reader stops there. A misspelt field:
```text
  - The file is not valid content JSON: The JSON property 'maxWorker' could not be mapped to any .NET member contained in type
    'CityBuilder.Simulation.Content.Data.BuildingLevelData'. Path: $.buildings[4].levels[0].maxWorker | LineNumber: 237 | BytePositionInLine: 22.
```
The codes are `ContentErrorCode` (`shared/Simulation/Runtime/Content/ContentErrorCode.cs`, each with its meaning); line numbers count from 0.

## Related
- How the server loads the file: `docs/modules/CityBuilder.Infrastructure.md` and `docs/modules/CityBuilder.Api.md` (`Content:FilePath`).
- How the engine checks it: `docs/modules/CityBuilder.Simulation.md`.
