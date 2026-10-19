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
/// (<c>server/content/*.json</c>), except the production cap, which is the M0 value of 8 hours, so the tests read like the spec.
/// </summary>
internal static class SettleFixture
{
    public const long ProductionCapSeconds = 8 * 3600;

    /// <summary>The woodcutter's cycle: 60 labor with its one worker.</summary>
    public const long CycleSeconds = 60;

    /// <summary>The city clock when the player last settled: the cottage and the woodcutter completed then, and the first cycle started.</summary>
    public const long StartClock = 60;

    /// <summary>The research <c>forestry</c> (2 days, started at clock 0) ends at this city-clock second, after the production cap.</summary>
    public const long ResearchEndsAt = 2 * 86_400;

    /// <summary>The manor (3 days, placed at <see cref="StartClock"/>) completes at this city-clock second, after the production cap.</summary>
    public const long ManorEndsAt = StartClock + (3 * 86_400);

    public const int ManorId = 3;

    public static readonly Guid PlayerId = new("6f1d2c3b-0000-4000-8000-000000000001");

    /// <summary>The server second the city was last settled at: a whole second, so the fake clock starts at t = 0 of the tests.</summary>
    public static readonly DateTimeOffset LastSettled = DateTimeOffset.FromUnixTimeSeconds(1_790_000_000);

    public static readonly ItemId Logs = new("logs");

    public static readonly ResearchId Forestry = new("forestry");

    public static GameContent Content { get; } = CreateContent();

    public static FakeTimeProvider ClockAtLastSettled() => new(LastSettled);

    /// <summary>
    /// A repository with the player's city, built with the engine's rules from the starter city: at clock 0 the player places a cottage
    /// (2 workers) and a woodcutter (1 log per 60 labor, 1 worker) and starts the 2-day research <c>forestry</c>; at clock 60 both buildings
    /// have completed, the woodcutter got its worker and started a cycle, and the player places a manor (3 days). The city was settled then,
    /// at <see cref="LastSettled"/>.
    /// </summary>
    public static InMemoryCityRepository Repository(DateTimeOffset? lastSettled = null)
    {
        CityState state = CityFactory.CreateStarterCity(Content);
        Apply(state, CityRules.PlaceBuilding(state, Content, new BuildingTypeId("cottage"), 0, 0));
        Apply(state, CityRules.PlaceBuilding(state, Content, new BuildingTypeId("woodcutter"), 1, 0));
        Apply(state, CityRules.StartResearch(state, Content, Forestry));
        Simulator.Advance(state, Content, StartClock);
        Apply(state, CityRules.PlaceBuilding(state, Content, new BuildingTypeId("manor"), 2, 0));

        var repository = new InMemoryCityRepository();
        repository.Add(new CityAggregate(PlayerId, state, (lastSettled ?? LastSettled).ToUnixTimeSeconds()));
        return repository;
    }

    public static SettleCity UseCase(ICityRepository repository, TimeProvider clock) => new(repository, new FixedContent(Content), clock);

    /// <summary>The city's state as text, field by field, so two cities compare in one assertion that shows the first difference.</summary>
    public static string Describe(CityState city)
    {
        IEnumerable<string> lines =
        [
            $"clock {city.Clock}, era {city.Era}, next id {city.NextBuildingId}",
            "stock " + string.Join(", ", city.Inventory.Select(stock => $"{stock.Key}={stock.Value}")),
            "research done " + string.Join(", ", city.CompletedResearch),
            city.ActiveResearch is null ? "no active research" : $"research {city.ActiveResearch.Id} {city.ActiveResearch.StartedAt}..{city.ActiveResearch.EndsAt}",
            .. city.Buildings.Select(building =>
                $"#{building.Id} {building.TypeId} ({building.X},{building.Y}) L{building.Level} w{building.AssignedWorkers} {building.Phase} " +
                $"{building.PhaseStartedAt}..{building.PhaseEndsAt} target {building.TargetLevel} labor {building.CycleLabor}/{building.LaborRemaining} " +
                $"segment {building.SegmentStartedAt}"),
        ];
        return string.Join("\n", lines);
    }

    // A mutation followed by the zero-length advance every command gets (04 section 7).
    private static void Apply(CityState state, MutationResult result)
    {
        Assert.True(result.IsOk, result.ToString());
        Simulator.Advance(state, Content, 0);
    }

    private static GameContent CreateContent()
    {
        var data = new GameContentData
        {
            ContentVersion = "settle-test-1",
            Rules = new GameRulesData
            {
                GridWidth = 4,
                GridHeight = 4,
                BuilderSlots = 2,
                ResearchSlots = 1,
                BaseStorageCaps = new() { ["logs"] = 1000 },
                MaxOfflineProductionSeconds = ProductionCapSeconds,
                MinCycleSeconds = 10,
                MaxLaborReductionPermille = 500,
            },
            StartingCity = new StartingCityData { Era = 1, Inventory = new() { ["coins"] = 100 } },
            Eras = [new EraData { Number = 1, Id = "first_era", Name = "First Era" }],
            Items =
            [
                new ItemData { Id = "coins", Name = "Coins", Era = 1, Capped = false, Tradable = false },
                new ItemData { Id = "logs", Name = "Logs", Era = 1, Capped = true, Tradable = true },
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
                        },
                    ],
                },
            ],
            Research =
            [
                new ResearchData
                {
                    Id = "forestry", Name = "Forestry", Era = 1, Cost = new() { ["coins"] = 10 }, DurationSeconds = ResearchEndsAt,
                    Effects = [new ResearchEffectData { Type = "laborReduction", Building = "woodcutter", Permille = 100 }],
                },
            ],
        };

        GameContentResult result = GameContentFactory.Create(data);
        Assert.True(result.IsValid, string.Join("\n", result.Errors.Select(error => error.ToString())));
        return result.Content!;
    }

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

    /// <summary>An <see cref="IContentProvider"/> that serves the given content.</summary>
    public sealed class FixedContent(GameContent content) : IContentProvider
    {
        public GameContent Content { get; } = content;
    }
}
