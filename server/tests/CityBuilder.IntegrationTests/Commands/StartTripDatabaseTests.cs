using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Contracts;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation;
using CityBuilder.Simulation.State;
using Microsoft.EntityFrameworkCore;
using static CityBuilder.IntegrationTests.Commands.DatabaseCommands;
using static CityBuilder.IntegrationTests.Commands.HandBuiltCities;
using static CityBuilder.IntegrationTests.Persistence.DatabaseCities;

namespace CityBuilder.IntegrationTests.Commands;

/// <summary>
/// <c>POST /api/v1/commands/start-trip</c> end to end on PostgreSQL over HTTP (CR-0008): the server's real content (trips carry 10, a tile of
/// open ground takes 5 s), endpoint, pipeline and repositories, the server clock a <c>FakeTimeProvider</c> at
/// <see cref="PostgresApiFactory.Start"/>. Each test stores a city built by hand at clock 0 (<see cref="StoreCityAsync"/>) and checks the
/// stored rows: <c>city_trips</c>, the stores and needs, the ledger and the receipts.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class StartTripDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>The endpoint under test.</summary>
    private const string _path = "/api/v1/commands/start-trip";

    /// <summary>The request id most tests send.</summary>
    private static readonly Guid _requestId = new("1de9e700-0000-4000-8000-0000000004a1");

    /// <summary>
    /// A trip from the warehouse (1,3) to a sawmill's construction site (1,2) next door: the trip row is stored with its load and its 5 s walk,
    /// the warehouse row holds 10 logs less, the next trip id is 2, and nothing goes to the ledger; the receipt holds the response. At second 5
    /// the request's settle delivers the 10 logs to the site, whose need row goes and whose build timer starts (120 s), deletes the trip row,
    /// and writes one <c>SETTLEMENT_DELIVERED</c> row of −10 logs with the city's total after it.
    /// </summary>
    [Fact]
    public async Task Post_TripToASite_StoresTheTripThenDeliversAndStartsTheBuild()
    {
        Building site = new(2, new BuildingTypeId("sawmill"), 1, 2, 1, 0, BuildingPhase.Constructing, 0, null, 0, 0, 0, 0, null, Goods(("logs", 10)));
        (Guid player, Guid city) = await StoreCityAsync(Factory, "carrier", 1000, House(1), site, Warehouse(3, logs: 30, planks: 0));

        ApiResponse response = await TripAsync(player, _requestId, 3, 2, "logs");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        await using (CityBuilderDbContext db = Database.CreateContext())
        {
            CityTripRow trip = await db.CityTrips.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal((1, 3, 2, "logs", 10L, 0L, 5L), (trip.TripId, trip.SourceId, trip.TargetId, trip.ItemId, trip.Quantity, trip.DepartedAt, trip.ArrivesAt));
            Assert.Equal(2, (await db.Cities.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).NextTripId);
        }

        Assert.Equal("coins 1000, logs 30", await StockAsync(Database, city));
        Assert.Empty(await LedgerAsync(Database, city));
        Assert.Equal(_requestId, Assert.Single(await ReceiptsAsync(Database, player)).RequestId);

        Factory.Clock.Advance(TimeSpan.FromSeconds(5));
        JsonNode settled = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();

        Assert.Equal("""{"logs":10}""", settled["settlement"]!["delivered"]!.ToJsonString());
        Assert.Equal("2 sawmill L1 Constructing 5-125 target 0 workers 0", await BuildingAsync(Database, city, 2));
        await using (CityBuilderDbContext db = Database.CreateContext())
        {
            Assert.False(await db.CityTrips.AnyAsync(TestContext.Current.CancellationToken));
            Assert.False(await db.CityProjectNeeds.AnyAsync(TestContext.Current.CancellationToken));
        }

        Assert.Equal(["logs -10 20 SETTLEMENT_DELIVERED settlement"], (await LedgerAsync(Database, city)).Select(row => $"{row.ItemId} {row.Delta} {row.BalanceAfter} {row.Reason} {row.RefType}"));
        Assert.Equal("coins 1000, logs 20", await StockAsync(Database, city));
    }

    /// <summary>
    /// A trip to a store without room brings its whole load back: the warehouse sends 10 logs to a woodcutter whose store is full (40); at
    /// the arrival they go back into the warehouse, the trip row is deleted, nothing is ledgered, and the city is saved at a new version.
    /// </summary>
    [Fact]
    public async Task Get_TripToAFullStore_BringsTheLoadBack()
    {
        (Guid player, Guid city) = await StoreCityAsync(
            Factory, "carrier", 1000, House(1), Woodcutter(2, BuildingPhase.Idle, workers: 0, logs: 40), Warehouse(3, logs: 10, planks: 0));
        Assert.Equal(HttpStatusCode.OK, (await TripAsync(player, _requestId, 3, 2, "logs")).Status);
        long version = (await LoadAsync(Factory, player)).StateVersion;
        Factory.Clock.Advance(TimeSpan.FromSeconds(30));

        JsonNode settled = (await ApiClient.GetAsync(Factory, "/api/v1/city", player)).Json();

        Assert.Equal(("{}", "{}", "[]"), (settled["settlement"]!["moved"]!.ToJsonString(), settled["settlement"]!["delivered"]!.ToJsonString(), settled["city"]!["trips"]!.ToJsonString()));
        Assert.Equal(version + 1, (long)settled["stateVersion"]!);
        Assert.Equal("""{"logs":10}""", settled["city"]!["buildings"]![2]!["store"]!.ToJsonString());
        Assert.Empty(await LedgerAsync(Database, city));
    }

    /// <summary>
    /// Another player's building id is <c>BUILDING_NOT_FOUND</c> (404), never a sign that it exists: the attacker names the owner's warehouse
    /// (building 3) as the source; nothing is stored in either city.
    /// </summary>
    [Fact]
    public async Task Security_AnotherPlayersBuildingId_Is404AndTouchesNeitherCity()
    {
        (Guid owner, Guid ownerCity) = await StoreCityAsync(Factory, "owner", 1000, House(1), House(2, x: 1), Warehouse(3, logs: 30, planks: 0));
        (Guid attacker, Guid attackerCity) = await StoreCityAsync(Factory, "attacker", 1000, House(1), Warehouse(2, logs: 0, planks: 0));
        string ownerStored = await StoredAsync(Factory, Database, owner, ownerCity);
        string attackerStored = await StoredAsync(Factory, Database, attacker, attackerCity);

        ApiResponse response = await TripAsync(attacker, _requestId, 3, 2, "logs");

        Assert.Equal((HttpStatusCode.NotFound, "BUILDING_NOT_FOUND"), (response.Status, (string)response.Json()["code"]!));
        Assert.Equal(ownerStored, await StoredAsync(Factory, Database, owner, ownerCity));
        Assert.Equal(attackerStored, await StoredAsync(Factory, Database, attacker, attackerCity));
    }

    /// <summary>Sends a trip of all of a good over HTTP with the player's token.</summary>
    /// <param name="player">The acting player.</param>
    /// <param name="requestId">The request id.</param>
    /// <param name="source">The source building.</param>
    /// <param name="target">The target building.</param>
    /// <param name="item">The good.</param>
    /// <returns>The answer.</returns>
    private Task<ApiResponse> TripAsync(Guid player, Guid requestId, int source, int target, string item) =>
        ApiClient.PostAsync(
            Factory, _path, player, $$"""{"requestId":"{{requestId}}","sourceId":{{source}},"targetId":{{target}},"itemId":"{{item}}","amount":"all"}""");
}
