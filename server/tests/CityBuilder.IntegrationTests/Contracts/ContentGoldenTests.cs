using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.IntegrationTests.Content;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CityBuilder.IntegrationTests.Contracts;

/// <summary>
/// <c>GET /api/v1/content</c> with frozen content (<see cref="ContractContent.Json"/>) against the exact text the contract gives
/// (<see cref="_expectedResponse"/>): every name, number, null and position. The content file lists its items, buildings, research and
/// cost maps out of order on purpose, so the test also proves the response sorts them. The numbers are the test's own: balance changes to
/// <c>server/content/mvp-content.json</c> don't touch them. In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server; the test derives a copy that loads the frozen content.</param>
public sealed class ContentGoldenTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    /// <summary>The temporary directory for the frozen content file.</summary>
    private readonly ContentFileDirectory _files = new();

    /// <summary>Deletes the frozen content file.</summary>
    public void Dispose() => _files.Dispose();

    /// <summary>The frozen content, which lists everything out of order, also survives the client's way back through the factory.</summary>
    [Fact]
    public async Task Get_FrozenContent_RebuildsTheSameContentThroughTheFactory()
    {
        string path = _files.Write(ContractContent.Json);
        await using WebApplicationFactory<Program> server = factory.WithWebHostBuilder(host => host.UseSetting("Content:FilePath", path));

        ApiResponse response = await ApiClient.GetAsync(server, "/api/v1/content", Guid.NewGuid());

        Assert.Equal(ContentFromResponse.Normalize(response.Body), ContentFromResponse.ResponseOf(ContentFromResponse.Rebuild(response.Body)));
    }

    /// <summary>The frozen content produces exactly the contract's text, byte for byte.</summary>
    [Fact]
    public async Task Get_FrozenContent_ReturnsExactlyTheContractText()
    {
        string path = _files.Write(ContractContent.Json);
        await using WebApplicationFactory<Program> server = factory.WithWebHostBuilder(host => host.UseSetting("Content:FilePath", path));

        ApiResponse response = await ApiClient.GetAsync(server, "/api/v1/content", Guid.NewGuid());

        Assert.Equal(HttpStatusCode.OK, response.Status);
        // The expected text is written readable; parsing and printing it again gives the compact text the server sends.
        Assert.Equal(JsonNode.Parse(_expectedResponse)!.ToJsonString(), response.Body);
    }

    /// <summary>The contract's text for <see cref="ContractContent.Json"/>: sorted, with every null written and the time scale 1.</summary>
    private const string _expectedResponse = """
        {
          "contentVersion": "contract-1",
          "rules": {
            "gridWidth": 6, "gridHeight": 5, "builderSlots": 2, "researchSlots": 1,
            "baseStorageCaps": { "logs": 400, "planks": 300 },
            "maxOfflineProductionSeconds": 14400, "minCycleSeconds": 5, "maxLaborReductionPermille": 400, "timeScale": 1
          },
          "startingCity": { "era": 1, "inventory": { "coins": 300, "logs": 60, "planks": 20 } },
          "eras": [ { "number": 1, "id": "dawn", "name": "Dawn" } ],
          "items": [
            { "id": "coins", "name": "Coins", "era": 1, "capped": false, "tradable": false },
            { "id": "logs", "name": "Logs", "era": 1, "capped": true, "tradable": true },
            { "id": "planks", "name": "Planks", "era": 1, "capped": true, "tradable": true }
          ],
          "buildings": [
            {
              "id": "house", "name": "Cottage", "kind": "House", "era": 1, "availableAtStart": true, "unlockedBy": null,
              "cost": { "coins": 20 }, "buildSeconds": 10,
              "levels": [
                { "level": 1, "maxWorkers": 0, "workersProvided": 3, "recipe": null, "upgradeCost": null, "upgradeSeconds": null },
                { "level": 2, "maxWorkers": 0, "workersProvided": 5, "recipe": null,
                  "upgradeCost": { "coins": 30, "logs": 15 }, "upgradeSeconds": 40 }
              ]
            },
            {
              "id": "trading_post", "name": "Trading Post", "kind": "Producer", "era": 1, "availableAtStart": false,
              "unlockedBy": "trade_charter", "cost": { "coins": 90 }, "buildSeconds": 60,
              "levels": [
                { "level": 1, "maxWorkers": 1, "workersProvided": 0,
                  "recipe": { "inputs": { "logs": 1, "planks": 2 }, "outputs": { "coins": 6 }, "labor": 90 },
                  "upgradeCost": null, "upgradeSeconds": null }
              ]
            },
            {
              "id": "woodcutter", "name": "Woodcutter", "kind": "Producer", "era": 1, "availableAtStart": true, "unlockedBy": null,
              "cost": { "coins": 40, "logs": 10 }, "buildSeconds": 20,
              "levels": [
                { "level": 1, "maxWorkers": 2, "workersProvided": 0,
                  "recipe": { "inputs": {}, "outputs": { "logs": 1 }, "labor": 30 }, "upgradeCost": null, "upgradeSeconds": null },
                { "level": 2, "maxWorkers": 3, "workersProvided": 0,
                  "recipe": { "inputs": {}, "outputs": { "logs": 1 }, "labor": 30 },
                  "upgradeCost": { "coins": 60, "planks": 5 }, "upgradeSeconds": 45 }
              ]
            }
          ],
          "research": [
            {
              "id": "sharp_axes", "name": "Sharp Axes", "era": 1, "cost": { "coins": 80 }, "durationSeconds": 120, "prerequisites": [],
              "effects": [ { "type": "laborReduction", "building": "woodcutter", "item": null, "permille": 150, "amount": null } ]
            },
            {
              "id": "storehouses", "name": "Storehouses", "era": 1, "cost": { "coins": 100, "logs": 20 }, "durationSeconds": 300,
              "prerequisites": ["trade_charter"],
              "effects": [
                { "type": "storageCapBonus", "building": null, "item": "planks", "permille": null, "amount": 50 },
                { "type": "storageCapBonus", "building": null, "item": "logs", "permille": null, "amount": 100 }
              ]
            },
            {
              "id": "trade_charter", "name": "Trade Charter", "era": 1, "cost": { "coins": 60 }, "durationSeconds": 90, "prerequisites": [],
              "effects": [ { "type": "unlockBuilding", "building": "trading_post", "item": null, "permille": null, "amount": null } ]
            }
          ]
        }
        """;
}
