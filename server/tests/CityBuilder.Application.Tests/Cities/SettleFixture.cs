using CityBuilder.Application.Cities;
using CityBuilder.Application.Content;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using Microsoft.Extensions.Time.Testing;

namespace CityBuilder.Application.Tests.Cities;

/// <summary>
/// A small content in code and a city on it for the settle tests. The numbers are made up for the tests, not the game's
/// (<c>server/content/*.json</c>).
/// </summary>
internal static class SettleFixture
{
    /// <summary>The woodcutter's store limit of logs (<see cref="Content"/>): a settlement long enough fills it, and then it stops.</summary>
    public const long WoodcutterStoreLimit = 1000;

    /// <summary>The woodcutter's cycle: 60 labor with its one worker.</summary>
    public const long CycleSeconds = 60;

    /// <summary>The city clock when the player last settled: the cottage and the woodcutter completed then, and the first cycle started.</summary>
    public const long StartClock = 60;

    /// <summary>How long the research <c>forestry</c> takes: 2 days.</summary>
    public const long ResearchSeconds = 2 * 86_400;

    /// <summary>
    /// The research <c>forestry</c> (2 days, started at <see cref="StartClock"/>, once the study completed: research needs a science building,
    /// CR-0008 D11) ends at this city-clock second, long after 8 hours.
    /// </summary>
    public const long ResearchEndsAt = StartClock + ResearchSeconds;

    /// <summary>The manor (3 days, placed at <see cref="StartClock"/>) completes at this city-clock second, after the research.</summary>
    public const long ManorEndsAt = StartClock + (3 * 86_400);

    /// <summary>The manor's building id: it is the fourth building placed, after the cottage, the woodcutter and the study.</summary>
    public const int ManorId = 4;

    /// <summary>The logs in the sawmill's store of <see cref="SawmillRepository"/> after its running cycle took its two.</summary>
    public const long SawmillLogs = 482;

    /// <summary>The id of the player's city.</summary>
    public static readonly Guid CityId = new("6f1d2c3b-0000-4000-8000-0000000000c1");

    /// <summary>The player whose city the repository holds.</summary>
    public static readonly Guid PlayerId = new("6f1d2c3b-0000-4000-8000-000000000001");

    /// <summary>The server second the city was last settled at: a whole second, so the fake clock starts at t = 0 of the tests.</summary>
    public static readonly DateTimeOffset LastSettled = DateTimeOffset.FromUnixTimeSeconds(1_790_000_000);

    /// <summary>The woodcutter's output and the sawmill's input.</summary>
    public static readonly ItemId Logs = new("logs");

    /// <summary>The sawmill's output.</summary>
    public static readonly ItemId Planks = new("planks");

    /// <summary>The research the player starts at clock 0.</summary>
    public static readonly ResearchId Forestry = new("forestry");

    /// <summary>The fixture's content, made once.</summary>
    public static GameContent Content { get; } = CreateContent();

    /// <summary>A fake server clock at <see cref="LastSettled"/>; each test moves it.</summary>
    public static FakeTimeProvider ClockAtLastSettled() => new(LastSettled);

    /// <summary>
    /// A repository with the player's city, built with the engine's rules from the starter city: at clock 0 the player places a cottage
    /// (2 workers), a woodcutter (1 log per 60 labor into its own store, 1 worker) and a study (the science building); at clock 60 all three
    /// have completed, the woodcutter got its worker and started a cycle, the player starts the 2-day research <c>forestry</c> at the study
    /// (coins only, so its timer runs at once) and places a manor (3 days). The city was settled then, at <see cref="LastSettled"/>; its purse
    /// holds 50 coins.
    /// </summary>
    public static InMemoryCityRepository Repository(DateTimeOffset? lastSettled = null)
    {
        CityState state = CityFactory.CreateStarterCity(Content);
        Apply(state, CityRules.PlaceBuilding(state, Content, new BuildingTypeId("cottage"), 0, 0));
        Apply(state, CityRules.PlaceBuilding(state, Content, new BuildingTypeId("woodcutter"), 1, 0));
        Apply(state, CityRules.PlaceBuilding(state, Content, new BuildingTypeId("study"), 3, 3));
        Simulator.Advance(state, Content, StartClock);
        Apply(state, CityRules.StartResearch(state, Content, Forestry));
        Apply(state, CityRules.PlaceBuilding(state, Content, new BuildingTypeId("manor"), 2, 0));

        return RepositoryWith(state, lastSettled);
    }

    /// <summary>
    /// A repository with a sawmill city at clock 120, restored as stored (goods reach a store only by walkers, so a sawmill with logs is set up
    /// directly): cottage 1 (0,0) gives 2 workers; woodcutter 2 (1,0) is producing (its cycle from 120 to 180) with 1 log in its store; sawmill 3
    /// (2,0) is producing (2 logs into 1 plank per 120 labor, its cycle from 120 to 240) with <see cref="SawmillLogs"/> logs left in its store.
    /// Settled then, at <see cref="LastSettled"/>; 70 coins in the purse.
    /// </summary>
    public static InMemoryCityRepository SawmillRepository() => RepositoryWith(SawmillState());

    /// <summary>The state of <see cref="SawmillRepository"/>'s city: 70 coins, 1 + 482 logs in two stores, no planks.</summary>
    /// <returns>A new state each call.</returns>
    public static CityState SawmillState() =>
        new CityState(
            2 * StartClock,
            1,
            4,
            70,
            [
                new Building(1, new BuildingTypeId("cottage"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
                new Building(2, new BuildingTypeId("woodcutter"), 1, 0, 1, 1, BuildingPhase.Producing, 120, 180, 0, 60, 60, 120, Store(("logs", 1))),
                new Building(3, new BuildingTypeId("sawmill"), 2, 0, 1, 1, BuildingPhase.Producing, 120, 240, 0, 120, 120, 120, Store(("logs", SawmillLogs))),
            ],
            [],
            activeResearch: null);

    /// <summary>A store's contents as the engine's pairs.</summary>
    /// <param name="goods">The goods and their quantities.</param>
    /// <returns>The pairs.</returns>
    public static KeyValuePair<ItemId, long>[] Store(params (string Item, long Quantity)[] goods) =>
        [.. goods.Select(good => new KeyValuePair<ItemId, long>(new ItemId(good.Item), good.Quantity))];

    /// <summary>The starter city with a completed study (the science building) at (3,3): research can start there.</summary>
    /// <returns>A city at clock 0 with the starting purse.</returns>
    public static CityState StarterCityWithStudy() => new(
        0, 1, 2, Content.StartingCity.Purse, [new Building(1, new BuildingTypeId("study"), 3, 3, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0)], [], null);

    /// <summary>The city's total of an item (<see cref="CityQueries.Total"/>): every store and load, or the purse for coins.</summary>
    /// <param name="city">The city.</param>
    /// <param name="item">The item.</param>
    /// <returns>The total.</returns>
    public static long Total(CityState city, ItemId item) => CityQueries.Total(city, Content, item);

    /// <summary>A repository with the player's city in <paramref name="state"/>, at state version 0, with no completed research.</summary>
    /// <param name="state">The city's state.</param>
    /// <param name="lastSettled">The server second it was last settled at; <see cref="LastSettled"/> by default.</param>
    /// <param name="contentVersion">The content version it was last settled with; the fixture's by default.</param>
    public static InMemoryCityRepository RepositoryWith(CityState state, DateTimeOffset? lastSettled = null, string? contentVersion = null)
    {
        var repository = new InMemoryCityRepository();
        repository.Add(new CityAggregate(
            CityId, PlayerId, state, researchCompletedAt: [], (lastSettled ?? LastSettled).ToUnixTimeSeconds(), stateVersion: 0,
            contentVersion ?? Content.ContentVersion));
        return repository;
    }

    /// <summary>
    /// The settle use case over the repository (also its unit of work), the fixture's content and the clock. Each call is a request of its
    /// own, with a new <see cref="CityTransactionRunner"/> (<see cref="RunnerPerRequest"/>), so a test can settle with it again and again.
    /// </summary>
    public static SettleCity UseCase(InMemoryCityRepository repository, TimeProvider clock) =>
        new(new RunnerPerRequest(repository), new FixedContent(Content), clock, Presence);

    /// <summary>The server's presence policy: the player counts as away 300 s after a request (<c>04</c> section 2).</summary>
    public static PresencePolicy Presence { get; } = new(300);

    /// <summary>The city's state as text, field by field, so two cities compare in one assertion that shows the first difference.</summary>
    public static string Describe(CityState city)
    {
        IEnumerable<string> lines =
        [
            $"clock {city.Clock}, era {city.Era}, next id {city.NextBuildingId}, purse {city.Purse}, next trip {city.NextTripId}",
            "stores " + string.Join(", ", city.Buildings.SelectMany(building => building.Store.Select(stock => $"#{building.Id} {stock.Key}={stock.Value}"))),
            "needs " + string.Join(", ", city.Buildings.SelectMany(building => building.Needs.Select(need => $"#{building.Id} {need.Key}={need.Value}"))),
            "trips " + string.Join(", ", city.Trips.Select(trip => $"{trip.Id} {trip.SourceId}->{trip.TargetId} {trip.Item}={trip.Quantity} {trip.DepartedAt}..{trip.ArrivesAt}")),
            "research done " + string.Join(", ", city.CompletedResearch),
            city.ActiveResearch is null ? "no active research" : $"research {city.ActiveResearch.Id} {city.ActiveResearch.StartedAt}..{city.ActiveResearch.EndsAt}",
            .. city.Buildings.Select(building =>
                $"#{building.Id} {building.TypeId} ({building.X},{building.Y}) L{building.Level} w{building.AssignedWorkers} {building.Phase} " +
                $"{building.PhaseStartedAt}..{building.PhaseEndsAt} target {building.TargetLevel} labor {building.CycleLabor}/{building.LaborRemaining} " +
                $"segment {building.SegmentStartedAt}"),
        ];
        return string.Join("\n", lines);
    }

    /// <summary>A mutation followed by the zero-length advance every command gets (<c>04</c> section 7); a rejected mutation fails the test.</summary>
    private static void Apply(CityState state, MutationResult result)
    {
        Assert.True(result.IsOk, result.ToString());
        Simulator.Advance(state, Content, 0);
    }

    /// <summary>
    /// The fixture's content: a 4 × 4 grid, three builder slots, two houses (cottage and manor), the woodcutter (a store of 1 000 logs), the
    /// sawmill (2 logs into 1 plank; stores of 1 000 each; a level 2 for 10 coins and 60 s, for the upgrade tests), the study (the science
    /// building, one per city), the courier station (2 couriers carrying 10 with 14 400 s of stamina; CR-0008-T1 part G), coins as the currency (100 to start), the preset <c>all</c>, trips of up to 1 000, and the research
    /// <c>forestry</c> (−100 ‰ on the woodcutter's labor). Invalid content fails the test with every error.
    /// </summary>
    private static GameContent CreateContent()
    {
        var data = new GameContentData
        {
            ContentVersion = "settle-test-1",
            Rules = new GameRulesData
            {
                GridWidth = 4,
                GridHeight = 4,
                BuilderSlots = 3,
                ResearchSlots = 1,
                MinCycleSeconds = 10,
                MaxLaborReductionPermille = 500,
                GroundTileMillis = 5000,
                CrossingSpeedPermille = 200,
                MinWalkSeconds = 2,
                MaxWalkBonusPermille = 500,
                TripCarryLimit = 1000,
                MaxRouteStops = 4,
                MaxStopActions = 2,
                MaxDeliveryLines = 20,
            },
            AmountPresets = [new AmountPresetData { Id = "all", Name = "All", Percent = 100 }],
            Roads = [new RoadLevelData { Level = 1, Name = "Path", TileMillis = 2500, Cost = new() { ["coins"] = 5 } }],
            StartingCity = new StartingCityData { Era = 1, Purse = 100 },
            Eras = [new EraData { Number = 1, Id = "first_era", Name = "First Era" }],
            Items =
            [
                new ItemData { Id = "coins", Name = "Coins", Era = 1, Currency = true, Tradable = false },
                new ItemData { Id = "logs", Name = "Logs", Era = 1, Currency = false, Tradable = true },
                new ItemData { Id = "planks", Name = "Planks", Era = 1, Currency = false, Tradable = true },
            ],
            Buildings =
            [
                House("cottage", buildSeconds: StartClock),
                House("manor", buildSeconds: ManorEndsAt - StartClock),
                new BuildingData
                {
                    Id = "woodcutter", Name = "Woodcutter", Kind = "producer", Era = 1, AvailableAtStart = true,
                    Cost = new() { ["coins"] = 10 }, BuildSeconds = StartClock,
                    Levels =
                    [
                        new BuildingLevelData
                        {
                            Level = 1, MaxWorkers = 1, Recipe = new RecipeData { Outputs = new() { ["logs"] = 1 }, Labor = CycleSeconds },
                            Storage = new() { ["logs"] = WoodcutterStoreLimit },
                        },
                    ],
                },
                new BuildingData
                {
                    Id = "sawmill", Name = "Sawmill", Kind = "producer", Era = 1, AvailableAtStart = true,
                    Cost = new() { ["coins"] = 10 }, BuildSeconds = StartClock,
                    Levels =
                    [
                        new BuildingLevelData
                        {
                            Level = 1, MaxWorkers = 1,
                            Recipe = new RecipeData { Inputs = new() { ["logs"] = 2 }, Outputs = new() { ["planks"] = 1 }, Labor = 2 * CycleSeconds },
                            Storage = new() { ["logs"] = 1000, ["planks"] = 1000 },
                        },
                        new BuildingLevelData
                        {
                            Level = 2, MaxWorkers = 1, UpgradeCost = new() { ["coins"] = 10 }, UpgradeSeconds = CycleSeconds,
                            Recipe = new RecipeData { Inputs = new() { ["logs"] = 2 }, Outputs = new() { ["planks"] = 1 }, Labor = 2 * CycleSeconds },
                            Storage = new() { ["logs"] = 1000, ["planks"] = 1000 },
                        },
                    ],
                },
                new BuildingData
                {
                    Id = "study", Name = "Study", Kind = "science", Era = 1, AvailableAtStart = true, MaxPerCity = 1,
                    Cost = new() { ["coins"] = 10 }, BuildSeconds = StartClock, Levels = [new BuildingLevelData { Level = 1 }],
                },
                new BuildingData
                {
                    Id = "courier_station", Name = "Courier Station", Kind = "courierStation", Era = 1, AvailableAtStart = true,
                    Cost = new() { ["coins"] = 10 }, BuildSeconds = StartClock,
                    Levels = [new BuildingLevelData { Level = 1, MaxWorkers = 2, CarryLimit = 10, StaminaSeconds = 14_400 }],
                },
            ],
            Research =
            [
                new ResearchData
                {
                    Id = "forestry", Name = "Forestry", Era = 1, Cost = new() { ["coins"] = 10 }, DurationSeconds = ResearchSeconds,
                    Effects = [new ResearchEffectData { Type = "laborReduction", Building = "woodcutter", Permille = 100 }],
                },
            ],
        };

        GameContentResult result = GameContentFactory.Create(data);
        Assert.True(result.IsValid, string.Join("\n", result.Errors.Select(error => error.ToString())));
        return result.Content!;
    }

    /// <summary>A house type with one level that provides 2 workers, costs 10 coins and builds in <paramref name="buildSeconds"/>.</summary>
    private static BuildingData House(string id, long buildSeconds) => new()
    {
        Id = id,
        Name = id,
        Kind = "house",
        Era = 1,
        AvailableAtStart = true,
        Cost = new() { ["coins"] = 10 },
        BuildSeconds = buildSeconds,
        Levels = [new BuildingLevelData { Level = 1, WorkersProvided = 2 }],
    };

    /// <summary>
    /// An <see cref="ICityTransactionRunner"/> that runs every call with a new <see cref="CityTransactionRunner"/> over the repository, as each
    /// request gets its own scoped runner in the server; one runner runs once.
    /// </summary>
    /// <param name="repository">The repository and unit of work of every run.</param>
    public sealed class RunnerPerRequest(InMemoryCityRepository repository) : ICityTransactionRunner
    {
        /// <summary>Runs the work with a new runner, as a new request would.</summary>
        public Task<TResult> RunAsync<TResult>(
            Guid playerId, Func<CityTransaction, CancellationToken, Task<TResult>> work, CancellationToken cancellationToken) =>
            new CityTransactionRunner(repository, repository).RunAsync(playerId, work, cancellationToken);
    }

    /// <summary>An <see cref="IContentProvider"/> that serves the given content.</summary>
    /// <param name="content">The content to serve.</param>
    public sealed class FixedContent(GameContent content) : IContentProvider
    {
        /// <summary>The content given to the constructor.</summary>
        public GameContent Content { get; } = content;
    }
}
