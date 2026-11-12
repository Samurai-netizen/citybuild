using CityBuilder.Simulation;

namespace CityBuilder.IntegrationTests.Contracts;

/// <summary>
/// The frozen content the contract tests run the server on: a small content file of the tests' own, so a balance change to
/// <c>server/content/mvp-content.json</c> never changes what they expect. Its items, buildings, research and the keys of its cost maps are
/// deliberately not in id order, so a response that is sorted proves the server sorts. <see cref="ContentGoldenTests"/> checks the text it
/// gives at <c>/content</c>; <see cref="CitySnapshotContractTests"/> run cities on it.
/// </summary>
internal static class ContractContent
{
    /// <summary>The item <c>coins</c> of the content.</summary>
    public static readonly ItemId Coins = new("coins");

    /// <summary>The item <c>logs</c> of the content: the woodcutter's store holds 40 (60 at level 2).</summary>
    public static readonly ItemId Logs = new("logs");

    /// <summary>The item <c>planks</c> of the content: the trading post's store holds 20.</summary>
    public static readonly ItemId Planks = new("planks");

    /// <summary>The building type <c>study</c>: the science building (one per city), where research happens.</summary>
    public static readonly BuildingTypeId Study = new("study");

    /// <summary>The building type <c>post</c>: a courier station for 2 couriers carrying 6 goods with 900 s of stamina.</summary>
    public static readonly BuildingTypeId Post = new("post");

    /// <summary>The building type <c>depot</c>: a warehouse holding 100 logs and 100 planks.</summary>
    public static readonly BuildingTypeId Depot = new("depot");

    /// <summary>The building type <c>house</c>: level 1 provides 3 workers, level 2 provides 5.</summary>
    public static readonly BuildingTypeId House = new("house");

    /// <summary>The building type <c>woodcutter</c>: a raw producer of logs (labor 30), 2 workers at level 1 and 3 at level 2.</summary>
    public static readonly BuildingTypeId Woodcutter = new("woodcutter");

    /// <summary>The building type <c>trading_post</c>: turns 2 planks and 1 log into 6 coins (labor 90), 1 worker; unlocked by <see cref="TradeCharter"/>.</summary>
    public static readonly BuildingTypeId TradingPost = new("trading_post");

    /// <summary>The research <c>trade_charter</c> (90 seconds): unlocks the trading post.</summary>
    public static readonly ResearchId TradeCharter = new("trade_charter");

    /// <summary>The research <c>storehouses</c> (300 seconds, needs the trade charter): +100 logs and +50 planks in every store that holds them.</summary>
    public static readonly ResearchId Storehouses = new("storehouses");

    /// <summary>The research <c>sharp_axes</c> (120 seconds): the woodcutter's labor −150 ‰.</summary>
    public static readonly ResearchId SharpAxes = new("sharp_axes");

    /// <summary>
    /// The content file's text (CR-0008's model): one era, three items (coins the currency), a building of each kind, four research nodes,
    /// two road levels and two amount presets, out of order on purpose.
    /// </summary>
    public const string Json = """
        {
          "contentVersion": "contract-2",
          "rules": {
            "gridWidth": 6, "gridHeight": 5, "builderSlots": 2, "researchSlots": 1,
            "minCycleSeconds": 5, "maxLaborReductionPermille": 400,
            "groundTileMillis": 6000, "crossingSpeedPermille": 300, "minWalkSeconds": 3, "maxWalkBonusPermille": 250,
            "tripCarryLimit": 8, "maxRouteStops": 5, "maxStopActions": 2, "maxDeliveryLines": 12
          },
          "startingCity": { "era": 1, "purse": 300 },
          "eras": [ { "number": 1, "id": "dawn", "name": "Dawn" } ],
          "items": [
            { "id": "planks", "name": "Planks", "era": 1, "currency": false, "tradable": true },
            { "id": "coins", "name": "Coins", "era": 1, "currency": true, "tradable": false },
            { "id": "logs", "name": "Logs", "era": 1, "currency": false, "tradable": true }
          ],
          "amountPresets": [
            { "id": "half", "name": "Half", "percent": 50 },
            { "id": "all", "name": "All", "percent": 100 }
          ],
          "buildings": [
            {
              "id": "woodcutter", "name": "Woodcutter", "kind": "producer", "era": 1, "availableAtStart": true,
              "cost": { "logs": 10, "coins": 40 }, "buildSeconds": 20,
              "levels": [
                { "level": 1, "maxWorkers": 2, "recipe": { "outputs": { "logs": 1 }, "labor": 30 }, "storage": { "logs": 40 } },
                { "level": 2, "maxWorkers": 3, "recipe": { "outputs": { "logs": 1 }, "labor": 30 }, "storage": { "logs": 60 },
                  "upgradeCost": { "planks": 5, "coins": 60 }, "upgradeSeconds": 45 }
              ]
            },
            {
              "id": "trading_post", "name": "Trading Post", "kind": "producer", "era": 1, "availableAtStart": false,
              "cost": { "coins": 90 }, "buildSeconds": 60,
              "levels": [
                { "level": 1, "maxWorkers": 1, "recipe": { "inputs": { "planks": 2, "logs": 1 }, "outputs": { "coins": 6 }, "labor": 90 },
                  "storage": { "planks": 20, "logs": 10 } }
              ]
            },
            {
              "id": "study", "name": "Study", "kind": "science", "era": 1, "availableAtStart": true, "maxPerCity": 1,
              "cost": { "coins": 30 }, "buildSeconds": 15,
              "levels": [ { "level": 1 } ]
            },
            {
              "id": "post", "name": "Post", "kind": "courierStation", "era": 1, "availableAtStart": true,
              "cost": { "coins": 35 }, "buildSeconds": 25,
              "levels": [ { "level": 1, "maxWorkers": 2, "carryLimit": 6, "staminaSeconds": 900 } ]
            },
            {
              "id": "depot", "name": "Depot", "kind": "warehouse", "era": 1, "availableAtStart": true,
              "cost": { "logs": 5, "coins": 25 }, "buildSeconds": 20,
              "levels": [ { "level": 1, "storage": { "planks": 100, "logs": 100 } } ]
            },
            {
              "id": "house", "name": "Cottage", "kind": "house", "era": 1, "availableAtStart": true,
              "cost": { "coins": 20 }, "buildSeconds": 10,
              "levels": [
                { "level": 1, "workersProvided": 3 },
                { "level": 2, "workersProvided": 5, "upgradeCost": { "logs": 15, "coins": 30 }, "upgradeSeconds": 40 }
              ]
            }
          ],
          "research": [
            {
              "id": "storehouses", "name": "Storehouses", "era": 1, "cost": { "logs": 20, "coins": 100 }, "durationSeconds": 300,
              "prerequisites": ["trade_charter"],
              "effects": [
                { "type": "storageCapBonus", "item": "planks", "amount": 50 },
                { "type": "storageCapBonus", "item": "logs", "amount": 100 }
              ]
            },
            {
              "id": "sharp_axes", "name": "Sharp Axes", "era": 1, "cost": { "coins": 80 }, "durationSeconds": 120,
              "effects": [ { "type": "laborReduction", "building": "woodcutter", "permille": 150 } ]
            },
            {
              "id": "trade_charter", "name": "Trade Charter", "era": 1, "cost": { "coins": 60 }, "durationSeconds": 90,
              "effects": [ { "type": "unlockBuilding", "building": "trading_post" } ]
            },
            {
              "id": "tallies", "name": "Tallies", "era": 1, "cost": { "coins": 40 }, "durationSeconds": 60,
              "effects": [ { "type": "walkSpeedBonus", "permille": 100 }, { "type": "unlockAmountMode", "mode": "percent" } ]
            }
          ],
          "roads": [
            { "level": 2, "name": "Stone path", "tileMillis": 2000, "cost": { "coins": 12 } },
            { "level": 1, "name": "Track", "tileMillis": 4000, "cost": { "coins": 3 } }
          ]
        }
        """;
}
