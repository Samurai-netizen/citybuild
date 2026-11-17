using System.Net;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Contracts;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation;
using CityBuilder.Simulation.State;
using static CityBuilder.IntegrationTests.Commands.DatabaseCommands;
using static CityBuilder.IntegrationTests.Commands.HandBuiltCities;
using static CityBuilder.IntegrationTests.Concurrency.SoundCity;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Concurrency;

/// <summary>
/// The concurrency suite (M0-75): requests of one player sent at the same time over HTTP to the real API on PostgreSQL, so the city's row lock
/// puts them one after the other. Each test checks who won, what was stored, and that the city is sound (<see cref="SoundCity"/>). The races
/// one endpoint's tests already run (the same request ten times: <c>PlaceBuildingDatabaseTests</c>, <c>UpgradeBuildingDatabaseTests</c>,
/// <c>ApiAttackDatabaseTests</c>, <c>CommandConcurrencyDatabaseTests</c>; two assignments over the free workers: <c>AssignWorkersDatabaseTests</c>;
/// two research starts: <c>StartResearchDatabaseTests</c>) end with the same soundness check there. Content as in those tests: a house costs 50
/// coins, its level 2 120 coins and 30 logs; a sawmill's level 2 250 coins and 30 planks; 2 builders.
/// </summary>
/// <param name="server">The run's PostgreSQL server; each test gets a fresh database on it.</param>
[Trait("Category", "Integration")]
public sealed class RaceTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The place endpoint.</summary>
    private const string _place = "/api/v1/commands/place-building";

    /// <summary>
    /// Two houses at once on two free tiles with coins for one (90, a house costs 50): exactly one is placed and the other is
    /// <c>INSUFFICIENT_RESOURCE</c>; the purse paid once (40), one building, one cost row, one receipt.
    /// </summary>
    [Fact]
    public async Task Security_TwoPlacementsOverTheCoins_PlaceExactlyOne()
    {
        (Guid player, Guid city) = await StoreAuditedCityAsync(90);

        ApiResponse[] responses = await Task.WhenAll(PlaceHouseAsync(player, 3, 4), PlaceHouseAsync(player, 4, 4));

        Assert.Equal(["INSUFFICIENT_RESOURCE", "OK"], Outcomes(responses));
        Assert.Single((await LoadAsync(Factory, player)).State.Buildings);
        Assert.Equal("coins 40", await StockAsync(Database, city));
        Assert.Single(await LedgerAsync(Database, city), row => row.Reason == "COST_PLACE");
        Assert.Single(await ReceiptsAsync(Database, player));
        await AssertSoundAsync(Factory, Database, player, city);
    }

    /// <summary>
    /// Two houses at once on the same tile, with coins for both: exactly one is placed and the other is <c>TILE_OCCUPIED</c>; one building on
    /// the tile, one cost paid, one receipt.
    /// </summary>
    [Fact]
    public async Task Security_TwoPlacementsOnOneTile_PlaceExactlyOne()
    {
        (Guid player, Guid city) = await StoreAuditedCityAsync(1000);

        ApiResponse[] responses = await Task.WhenAll(PlaceHouseAsync(player, 3, 4), PlaceHouseAsync(player, 3, 4));

        Assert.Equal(["OK", "TILE_OCCUPIED"], Outcomes(responses));
        Building house = Assert.Single((await LoadAsync(Factory, player)).State.Buildings);
        Assert.Equal((3, 4), (house.X, house.Y));
        Assert.Equal("coins 950", await StockAsync(Database, city));
        Assert.Single(await LedgerAsync(Database, city), row => row.Reason == "COST_PLACE");
        Assert.Single(await ReceiptsAsync(Database, player));
        await AssertSoundAsync(Factory, Database, player, city);
    }

    /// <summary>
    /// An upgrade and four <c>GET /city</c> at once, at the second a producing sawmill's upgrade gets its last planks (the setting of
    /// <c>UpgradeBuildingDatabaseTests.Post_ProducingSawmill_LastPlanksDeliveredStartTheUpgradeAndRefundAboveTheLimit</c>): whichever request
    /// settles first ends the sawmill's cycle and refunds its 2 logs; the others find the city settled. Exactly one answer reports the refund,
    /// the ledger holds one <c>SETTLEMENT_REFUNDED</c> row, the sawmill upgrades from second 15, and the house's upgrade (the other request)
    /// pays its 120 coins once.
    /// </summary>
    [Fact]
    public async Task Security_UpgradeRacingGetCity_RefundsOnce()
    {
        (Guid player, Guid city) = await StoreAuditedCityAsync(1000, House(1), Sawmill(2, logs: 40), Warehouse(3, logs: 0, planks: 30));
        Assert.Equal(HttpStatusCode.OK, (await UpgradeAsync(player, 2)).Status);
        Assert.Equal(HttpStatusCode.OK, (await TripAsync(player, 3, 2)).Status);
        Assert.Equal(HttpStatusCode.OK, (await TripAsync(player, 3, 2)).Status);
        Factory.Clock.Advance(TimeSpan.FromSeconds(15));

        ApiResponse[] responses = await Task.WhenAll(
            [UpgradeAsync(player, 1), .. Enumerable.Range(0, 4).Select(_ => ApiClient.GetAsync(Factory, "/api/v1/city", player))]);

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.Status));
        Assert.Single(responses, response => response.Json()["settlement"]!["refunded"]!["logs"] is not null);
        Assert.Equal("2 sawmill L1 Upgrading 15-195 target 2 workers 2", await BuildingAsync(Database, city, 2));
        Assert.Equal("coins 630, logs 42", await StockAsync(Database, city));
        Assert.Single(await LedgerAsync(Database, city), row => row.Reason == "SETTLEMENT_REFUNDED");
        Assert.Equal(4, (await ReceiptsAsync(Database, player)).Count);
        await AssertSoundAsync(Factory, Database, player, city);
    }

    /// <summary>
    /// Stores a city built by hand (<see cref="StoreCityAsync"/>) with a <c>STARTING_GRANT</c> ledger row for each of its stored totals, as
    /// the first rows of its ledger, so the auditor can audit it like a city a login created (M0-B decision 7).
    /// </summary>
    /// <param name="coins">The coins in the purse.</param>
    /// <param name="buildings">The buildings, with ids from 1 up.</param>
    /// <returns>The player and their city.</returns>
    private async Task<(Guid Player, Guid City)> StoreAuditedCityAsync(long coins, params Building[] buildings)
    {
        (Guid player, Guid city) = await StoreCityAsync(Factory, "racer", coins, buildings);
        Dictionary<string, long> totals = await StoredTotalsAsync(Database, city, new ItemId("coins"));
        await using CityBuilderDbContext db = Database.CreateContext();
        db.LedgerEntries.AddRange(totals.Where(total => total.Value > 0).OrderBy(total => total.Key, StringComparer.Ordinal).Select(total => new LedgerEntryRow
        {
            CityId = city,
            ItemId = total.Key,
            Delta = total.Value,
            BalanceAfter = total.Value,
            Reason = "STARTING_GRANT",
            CreatedAt = PostgresApiFactory.Start,
        }));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (player, city);
    }

    /// <summary>The answers as their codes in ordinal order, <c>OK</c> for a 200.</summary>
    /// <param name="responses">The answers.</param>
    /// <returns>The sorted outcomes.</returns>
    private static string[] Outcomes(ApiResponse[] responses) =>
        [.. responses.Select(response => response.Status == HttpStatusCode.OK ? "OK" : (string)response.Json()["code"]!).Order(StringComparer.Ordinal)];

    /// <summary>Places a house with a new request id.</summary>
    /// <param name="player">The player.</param>
    /// <param name="x">The tile's column.</param>
    /// <param name="y">The tile's row.</param>
    /// <returns>The answer.</returns>
    private Task<ApiResponse> PlaceHouseAsync(Guid player, int x, int y) =>
        ApiClient.PostAsync(Factory, _place, player, $$"""{"requestId":"{{Guid.CreateVersion7()}}","buildingTypeId":"house","x":{{x}},"y":{{y}}}""");

    /// <summary>Orders a building's next level with a new request id.</summary>
    /// <param name="player">The player.</param>
    /// <param name="buildingId">The building.</param>
    /// <returns>The answer.</returns>
    private Task<ApiResponse> UpgradeAsync(Guid player, int buildingId) =>
        ApiClient.PostAsync(Factory, "/api/v1/commands/upgrade-building", player, $$"""{"requestId":"{{Guid.CreateVersion7()}}","buildingId":{{buildingId}}}""");

    /// <summary>Sends all planks of a building to another with a new request id (a trip of one worker, 10 a load).</summary>
    /// <param name="player">The player.</param>
    /// <param name="source">The building the planks come from.</param>
    /// <param name="target">The building they go to.</param>
    /// <returns>The answer.</returns>
    private Task<ApiResponse> TripAsync(Guid player, int source, int target) =>
        ApiClient.PostAsync(
            Factory,
            "/api/v1/commands/start-trip",
            player,
            $$"""{"requestId":"{{Guid.CreateVersion7()}}","sourceId":{{source}},"targetId":{{target}},"itemId":"planks","amount":"all"}""");
}
