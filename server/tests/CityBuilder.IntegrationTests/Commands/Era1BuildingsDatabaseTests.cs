using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.IntegrationTests.Contracts;
using CityBuilder.IntegrationTests.EndToEnd;
using CityBuilder.IntegrationTests.Golden;
using CityBuilder.IntegrationTests.Postgres;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using static CityBuilder.IntegrationTests.Commands.HandBuiltCities;

namespace CityBuilder.IntegrationTests.Commands;

/// <summary>
/// The Era I content (<c>server/content/era1-content.json</c>, the server's default since M1-02) over the real API on PostgreSQL: a new player
/// gets the starting city of <c>core-instructions/28-era1-content.md</c>, and each building type places and completes, each producer
/// produces, and each type with a second level upgrades to it. The goods of a project come from a warehouse stocked with exactly the cost,
/// sent with one <c>all</c> trip per good; every number is read from the content, never written here.
/// </summary>
/// <param name="server">The run's PostgreSQL server; each test gets a fresh database on it.</param>
[Trait("Category", "Integration")]
public sealed class Era1BuildingsDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>Long enough for every walk, construction and upgrade of the tests (the Town Hall's 600 s the longest).</summary>
    private static readonly TimeSpan _projectTime = TimeSpan.FromHours(1);

    /// <summary>The id of the building under test in the hand-built cities: after two houses and the warehouse.</summary>
    private const int _subject = 4;

    /// <summary>The Era I content, as the server loads it.</summary>
    private static GameContent Content => CharterPlayer.Content;

    /// <summary>Every building type of the content.</summary>
    public static TheoryData<string> Types => [.. Content.Buildings.Select(building => building.Id.Value)];

    /// <summary>Every producer type of the content.</summary>
    public static TheoryData<string> Producers => [.. Content.Buildings.Where(building => building.Kind == BuildingKind.Producer).Select(building => building.Id.Value)];

    /// <summary>Every type with a second level.</summary>
    public static TheoryData<string> Upgradable => [.. Content.Buildings.Where(building => building.Levels.Count > 1).Select(building => building.Id.Value)];

    /// <summary>A new player's city is the starting city of <c>28</c>: era 1, a purse of 1000 coins, no goods, buildings or research, clock 0.</summary>
    /// <returns>A task that completes when the city is checked.</returns>
    [Fact]
    public async Task Login_NewPlayer_GetsTheEra1StartingCity()
    {
        using GoldenHttpPlayer player = await GoldenHttpPlayer.LoginAsync(Factory, "newcomer");

        JsonNode snapshot = await player.GetCityAsync();

        JsonNode city = snapshot["city"]!;
        Assert.Equal("era1-2", (string)snapshot["contentVersion"]!);
        Assert.Equal((1, 1000L, 0L), ((int)city["era"]!, (long)city["purse"]!, (long)city["clock"]!));
        Assert.All(city["totals"]!.AsObject().Where(total => total.Key != "coins"), total => Assert.Equal(0, (long)total.Value!));
        Assert.Empty(city["buildings"]!.AsArray());
        Assert.Empty(city["research"]!["completed"]!.AsArray());
    }

    /// <summary>
    /// Each type, with every research done: placing it answers 200 and pays its coins; its goods arrive by trips; within the hour it stands
    /// complete at level 1.
    /// </summary>
    /// <param name="type">The building type.</param>
    /// <returns>A task that completes when the building is checked.</returns>
    [Theory]
    [MemberData(nameof(Types))]
    public async Task Post_PlaceEachEra1Type_CompletesWithItsGoods(string type)
    {
        BuildingDefinition definition = Definition(type);
        string[] everyResearch = [.. Content.Research.Select(node => node.Id.Value)];
        (Guid player, Guid city) = await StoreResearchedCityAsync(Factory, "builder", 10_000, everyResearch, House(1), House(2, x: 1), StockedWarehouse(definition.Cost));

        await PostOkAsync(player, "place-building", $$"""{"requestId":"{{Guid.NewGuid()}}","buildingTypeId":"{{type}}","x":4,"y":4}""");
        await SendGoodsAsync(player, definition.Cost);
        Factory.Clock.Advance(_projectTime);

        Building placed = await SubjectAsync(player);
        Assert.Equal(definition.Id, placed.TypeId);
        Assert.True(placed.IsBuilt, $"{type}: {placed.Phase}");
        Assert.Equal(1, placed.Level);
        // A raw producer is staffed when it completes and has made goods since; the purse paid the coins and nothing else.
        Assert.Equal((10_000 - Coins(definition.Cost)).ToString(CultureInfo.InvariantCulture), StockOf(await StockAsync(Database, city), "coins"));
    }

    /// <summary>
    /// Each producer, idle at level 1 with the inputs of two cycles in its store and no research: given its level's workers, it makes the
    /// outputs of two cycles in two cycles' time (goods into its store, coins into the purse).
    /// </summary>
    /// <param name="type">The producer type.</param>
    /// <returns>A task that completes when the outputs are checked.</returns>
    [Theory]
    [MemberData(nameof(Producers))]
    public async Task Post_AssignWorkersToEachEra1Producer_ProducesTwoCycles(string type)
    {
        BuildingDefinition definition = Definition(type);
        BuildingLevelDefinition level = definition.GetLevel(1);
        RecipeDefinition recipe = level.Recipe!;
        var producer = new Building(_subject, definition.Id, 4, 4, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0,
            recipe.Inputs.Select(input => new KeyValuePair<ItemId, long>(input.Item, 2 * input.Quantity)));
        (Guid player, Guid city) = await StoreCityAsync(Factory, "producer", 0, House(1), House(2, x: 1), StockedWarehouse([]), producer);

        await PostOkAsync(player, "assign-workers", $$"""{"requestId":"{{Guid.NewGuid()}}","buildingId":{{_subject}},"workers":{{level.MaxWorkers}}}""");
        Factory.Clock.Advance(TimeSpan.FromSeconds(2 * IntegerMath.CeilDiv(recipe.Labor, level.MaxWorkers)));
        await ApiClient.GetAsync(Factory, "/api/v1/city", player);

        Building made = await SubjectAsync(player);
        Assert.All(recipe.Inputs, input => Assert.Equal(0, made.Store.GetValueOrDefault(input.Item)));
        foreach (ItemQuantity output in recipe.Outputs)
        {
            long quantity = output.Item == Content.Currency ? long.Parse(StockOf(await StockAsync(Database, city), "coins"), CultureInfo.InvariantCulture) : made.Store.GetValueOrDefault(output.Item);
            Assert.Equal(2 * output.Quantity, quantity);
        }
    }

    /// <summary>
    /// Each type with a second level, idle at level 1 without research: the upgrade answers 200 and pays its coins; its goods arrive by trips
    /// (a courier station's own couriers carry them); within the hour it is at level 2.
    /// </summary>
    /// <param name="type">The building type.</param>
    /// <returns>A task that completes when the level is checked.</returns>
    [Theory]
    [MemberData(nameof(Upgradable))]
    public async Task Post_UpgradeEachEra1Type_ReachesLevel2WithItsGoods(string type)
    {
        BuildingDefinition definition = Definition(type);
        IReadOnlyList<ItemQuantity> cost = definition.GetLevel(2).UpgradeCost;
        var idle = new Building(_subject, definition.Id, 4, 4, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
        (Guid player, Guid city) = await StoreCityAsync(Factory, "upgrader", 10_000, House(1), House(2, x: 1), StockedWarehouse(cost), idle);

        if (definition.Kind == BuildingKind.CourierStation)
        {
            // A built station carries every trip (04 §18): it needs its couriers before its goods can come.
            await PostOkAsync(player, "assign-workers", $$"""{"requestId":"{{Guid.NewGuid()}}","buildingId":{{_subject}},"workers":{{definition.GetLevel(1).MaxWorkers}}}""");
        }

        await PostOkAsync(player, "upgrade-building", $$"""{"requestId":"{{Guid.NewGuid()}}","buildingId":{{_subject}}}""");
        await SendGoodsAsync(player, cost);
        Factory.Clock.Advance(_projectTime);

        Assert.Equal(2, (await SubjectAsync(player)).Level);
        Assert.Equal($"coins {10_000 - Coins(cost)}", await StockAsync(Database, city));
    }

    /// <summary>The type's definition in the content.</summary>
    /// <param name="type">The type id.</param>
    /// <returns>The definition.</returns>
    private static BuildingDefinition Definition(string type)
    {
        Assert.True(Content.TryGetBuilding(new BuildingTypeId(type), out BuildingDefinition? definition), type);
        return definition;
    }

    /// <summary>A level-1 warehouse (id 3) on (2, 0) holding exactly the goods of <paramref name="cost"/> (not its coins).</summary>
    /// <param name="cost">A cost.</param>
    /// <returns>The building.</returns>
    private static Building StockedWarehouse(IReadOnlyList<ItemQuantity> cost) =>
        new(3, new BuildingTypeId("warehouse"), 2, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0,
            cost.Where(amount => amount.Item != Content.Currency).Select(amount => new KeyValuePair<ItemId, long>(amount.Item, amount.Quantity)));

    /// <summary>The coins of a cost.</summary>
    /// <param name="cost">The cost.</param>
    /// <returns>The coins.</returns>
    private static long Coins(IReadOnlyList<ItemQuantity> cost) => cost.Where(amount => amount.Item == Content.Currency).Sum(amount => amount.Quantity);

    /// <summary>One item's quantity in a <see cref="HandBuiltCities.StockAsync"/> text such as <c>coins 550, logs 10</c>.</summary>
    /// <param name="stock">The text.</param>
    /// <param name="item">The item.</param>
    /// <returns>The quantity as text.</returns>
    private static string StockOf(string stock, string item) =>
        stock.Split(", ").Single(line => line.StartsWith(item + " ", StringComparison.Ordinal))[(item.Length + 1)..];

    /// <summary>Sends one trip per good of <paramref name="cost"/> from the warehouse to the building under test, the whole stock each.</summary>
    /// <param name="player">The player.</param>
    /// <param name="cost">The cost whose goods go.</param>
    /// <returns>A task that completes when every trip is accepted.</returns>
    private async Task SendGoodsAsync(Guid player, IReadOnlyList<ItemQuantity> cost)
    {
        foreach (ItemQuantity good in cost.Where(amount => amount.Item != Content.Currency))
        {
            await PostOkAsync(player, "start-trip",
                $$"""{"requestId":"{{Guid.NewGuid()}}","sourceId":3,"targetId":{{_subject}},"itemId":"{{good.Item}}","amount":"all"}""");
        }
    }

    /// <summary>Posts a command and asserts it answers 200.</summary>
    /// <param name="player">The player.</param>
    /// <param name="command">The command's path under <c>/api/v1/commands/</c>.</param>
    /// <param name="json">The body.</param>
    /// <returns>A task that completes when the answer is checked.</returns>
    private async Task PostOkAsync(Guid player, string command, string json)
    {
        ApiResponse response = await ApiClient.PostAsync(Factory, "/api/v1/commands/" + command, player, json);
        Assert.True(response.Status == HttpStatusCode.OK, $"{command} {json}: {(int)response.Status} {response.Body}");
    }

    /// <summary>The building under test as stored after a look (<c>GET /api/v1/city</c>) settles the city to now.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The building.</returns>
    private async Task<Building> SubjectAsync(Guid player)
    {
        await ApiClient.GetAsync(Factory, "/api/v1/city", player);
        return (await Persistence.DatabaseCities.LoadAsync(Factory, player)).State.Buildings.Single(building => building.Id == _subject);
    }
}
