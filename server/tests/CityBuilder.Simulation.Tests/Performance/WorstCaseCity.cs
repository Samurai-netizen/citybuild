using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.Content;
using CityBuilder.Simulation.Tests.Rules;

namespace CityBuilder.Simulation.Tests.Performance;

/// <summary>
/// The worst case of the performance budget (<c>04</c> section 12): 64 producers with 10-second cycles at full staffing that never stall,
/// over <c>Fixtures/content-performance.json</c>. Half are mines (no inputs, 2 ore a cycle into a store of <see cref="MineOreLimit"/>, which
/// fills only in a long advance after 8 hours), half
/// are smelters (1 ore in, 1 ingot out, each with 6 000 ore in its own store: enough for every cycle of the tests, as no courier brings ore
/// before CR-0008-T1 part F; part I sets the courier worst case of <c>04</c> section 12). The city is built through
/// <see cref="CityRules"/> as a player would build it, one producer a second, so the 64 cycles end on all ten seconds of every ten
/// (28,800 event times in 8 hours) instead of on one (2,880): that is the most work a 10-second cycle can cause. A 2-day research runs,
/// so a long advance also has a timer to complete.
/// </summary>
internal static class WorstCaseCity
{
    /// <summary>The producers of the worst case (<c>04</c> section 12).</summary>
    public const int Producers = 64;

    /// <summary>The houses that staff them: 4 × 32 workers = 64 × 2.</summary>
    public const int Houses = 4;

    /// <summary>A cycle at full staffing: labor 20 with 2 workers.</summary>
    public const long CycleSeconds = 10;

    /// <summary>The house type of the performance content, which provides the workers.</summary>
    public static readonly BuildingTypeId House = new("house");

    /// <summary>The mine: 2 ore a cycle from labor alone.</summary>
    public static readonly BuildingTypeId Mine = new("mine");

    /// <summary>The smelter: 1 ore in, 1 ingot out.</summary>
    public static readonly BuildingTypeId Smelter = new("smelter");

    /// <summary>
    /// A mine's store limit of ore (<c>content-performance.json</c>): more than a mine makes in the build and 8 hours (about 5,800), so it
    /// fills only in a long advance after them, as a store does in the game while the player is away.
    /// </summary>
    public const long MineOreLimit = 12_000;

    /// <summary>The mines' output.</summary>
    public static readonly ItemId Ore = new("ore");

    /// <summary>The smelters' uncapped output.</summary>
    public static readonly ItemId Ingots = new("ingots");

    /// <summary>The science building, where the research happens (CR-0008 D11).</summary>
    public static readonly BuildingTypeId Lab = new("lab");

    /// <summary>
    /// The ore each smelter starts with in its own store: the cycles it starts in the build, the 2,880 of an 8-hour advance, and about 3,100
    /// for a 7-day advance after it, which uses them up and leaves the smelter idle.
    /// </summary>
    public const long SmelterOre = 6000;

    /// <summary>The 2-day research the city starts last, so a long advance has a timer to complete.</summary>
    public static readonly ResearchId DeepVaults = new("deep_vaults");

    /// <summary>The performance content of <c>Fixtures/content-performance.json</c>, loaded once and checked like the server's.</summary>
    public static GameContent WorstCaseContent { get; } = ContentFile.Load("content-performance.json");

    /// <summary>
    /// A new worst-case city: the houses and the lab first; then a mine or a smelter (alternating, a mine first) placed every second, a
    /// smelter with <see cref="SmelterOre"/> in its store, each staffed automatically when its construction completes; then the research. It
    /// returns when every producer runs a cycle, each ending within the next <see cref="CycleSeconds"/>.
    /// </summary>
    public static CityState Build()
    {
        CityState city = CityFactory.CreateStarterCity(WorstCaseContent);
        int width = WorstCaseContent.Rules.GridWidth;
        for (int index = 0; index < Houses; index++)
        {
            Run(city, CityRules.PlaceBuilding(city, WorstCaseContent, House, index % width, index / width));
        }

        Run(city, CityRules.PlaceBuilding(city, WorstCaseContent, Lab, Houses % width, Houses / width));
        Simulator.Advance(city, WorstCaseContent, BuildSeconds(House));
        for (int index = 0; index < Producers; index++)
        {
            int tile = Houses + 1 + index;
            BuildingTypeId type = index % 2 == 0 ? Mine : Smelter;
            Run(city, CityRules.PlaceBuilding(city, WorstCaseContent, type, tile % width, tile / width));
            if (type == Smelter)
            {
                city = WithOre(city, city.NextBuildingId - 1);
            }

            Simulator.Advance(city, WorstCaseContent, 1);
        }

        Simulator.Advance(city, WorstCaseContent, Math.Max(BuildSeconds(Mine), BuildSeconds(Smelter)));
        Run(city, CityRules.StartResearch(city, WorstCaseContent, DeepVaults));
        return city;
    }

    /// <summary>
    /// The city restored with <see cref="SmelterOre"/> ore in the store of one building (a smelter just placed), the way the database would hold
    /// it; everything else is copied as it is.
    /// </summary>
    private static CityState WithOre(CityState city, int buildingId) => new(
        city.Clock,
        city.Era,
        city.NextBuildingId,
        city.Purse,
        city.Buildings.Select(building => new Building(
            building.Id,
            building.TypeId,
            building.X,
            building.Y,
            building.Level,
            building.AssignedWorkers,
            building.Phase,
            building.PhaseStartedAt,
            building.PhaseEndsAt,
            building.TargetLevel,
            building.CycleLabor,
            building.LaborRemaining,
            building.SegmentStartedAt,
            building.Id == buildingId ? [new KeyValuePair<ItemId, long>(Ore, SmelterOre)] : building.Store.ToArray(),
            building.Needs.ToArray())),
        city.CompletedResearch,
        city.ActiveResearch,
        city.Roads);

    /// <summary>The construction time of a building type of the performance content.</summary>
    private static long BuildSeconds(BuildingTypeId type) =>
        WorstCaseContent.TryGetBuilding(type, out BuildingDefinition? definition) ? definition.BuildSeconds : throw new ArgumentOutOfRangeException(nameof(type));

    /// <summary>Asserts a mutation succeeded and runs the zero-length advance that follows every command.</summary>
    private static void Run(CityState city, MutationResult result)
    {
        RulesAssert.Ok(result);
        Simulator.Advance(city, WorstCaseContent, 0);
    }
}
