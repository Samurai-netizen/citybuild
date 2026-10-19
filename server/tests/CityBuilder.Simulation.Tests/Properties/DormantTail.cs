using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// A test model of the dormant tail (<c>04</c> section 2): what an advance beyond the production cap must do after its window closes. From
/// the city at the last second of the window, every timer that ends up to the target completes in time order (research before buildings at
/// the same second, buildings by id, <c>04</c> section 6): a cycle delivers its outputs, a construction completes and auto-staffs with
/// <c>min(free, MaxWorkers)</c>, an upgrade raises the level, research joins the completed list. Nothing starts. The model is independent of
/// the engine's code: it only reads public state and content, and builds the expected city through the restore constructors.
/// </summary>
internal sealed class DormantTail
{
    private DormantTail(CityState city, SortedDictionary<ItemId, long> produced, List<int> completedBuildings, List<ResearchId> completedResearch)
    {
        City = city;
        Produced = produced;
        CompletedBuildings = completedBuildings;
        CompletedResearch = completedResearch;
    }

    /// <summary>The city the tail must leave at the target.</summary>
    public CityState City { get; }

    /// <summary>The outputs of the cycles that complete in the tail (nothing is consumed there).</summary>
    public IReadOnlyDictionary<ItemId, long> Produced { get; }

    /// <summary>The constructions and upgrades that complete in the tail, by id, and the research.</summary>
    public IReadOnlyList<int> CompletedBuildings { get; }

    public IReadOnlyList<ResearchId> CompletedResearch { get; }

    /// <summary>The tail from <paramref name="atCap"/> (the city at the window's last second, after its starts) to <paramref name="target"/>.</summary>
    public static DormantTail Model(GameContent content, CityState atCap, long target)
    {
        var inventory = new SortedDictionary<ItemId, long>(atCap.Inventory.ToDictionary(stock => stock.Key, stock => stock.Value));
        List<Building> buildings = [.. atCap.Buildings.Select(CityCopy.Of)];
        var completedResearch = new List<ResearchId>(atCap.CompletedResearch);
        ActiveResearch? active = atCap.ActiveResearch;
        var produced = new SortedDictionary<ItemId, long>();
        var completedBuildings = new List<int>();
        var tailResearch = new List<ResearchId>();

        if (active is not null && active.EndsAt <= target)
        {
            // Research completes before any building of its second; its effects don't matter here, because nothing starts in the tail.
            completedResearch.Add(active.Id);
            tailResearch.Add(active.Id);
            active = null;
        }

        IEnumerable<Building> due = buildings.Where(building => building.PhaseEndsAt <= target);
        foreach (Building building in due.OrderBy(building => building.PhaseEndsAt).ThenBy(building => building.Id).ToList())
        {
            BuildingDefinition type = Definition(content, building.TypeId);
            int index = buildings.IndexOf(building);
            switch (building.Phase)
            {
                case BuildingPhase.Producing:
                    foreach (ItemQuantity output in type.GetLevel(building.Level).Recipe!.Outputs)
                    {
                        inventory[output.Item] = inventory.GetValueOrDefault(output.Item) + output.Quantity;
                        produced[output.Item] = produced.GetValueOrDefault(output.Item) + output.Quantity;
                    }

                    buildings[index] = Idle(building, building.Level, building.AssignedWorkers);
                    break;
                case BuildingPhase.Constructing:
                    long free = Provided(content, buildings) - buildings.Sum(other => (long)other.AssignedWorkers);
                    buildings[index] = Idle(building, building.Level, (int)Math.Min(free, type.GetLevel(building.Level).MaxWorkers));
                    completedBuildings.Add(building.Id);
                    break;
                case BuildingPhase.Upgrading:
                    buildings[index] = Idle(building, building.TargetLevel, building.AssignedWorkers);
                    completedBuildings.Add(building.Id);
                    break;
                default:
                    throw new InvalidOperationException($"Building {building.Id} is {building.Phase} with an end time.");
            }
        }

        completedBuildings.Sort();
        var city = new CityState(target, atCap.Era, atCap.NextBuildingId, inventory, buildings, completedResearch, active);
        return new DormantTail(city, produced, completedBuildings, tailResearch);
    }

    /// <summary>The workers the completed houses provide (a house still constructing provides none).</summary>
    public static long Provided(GameContent content, IEnumerable<Building> buildings) => buildings
        .Where(building => building.Phase != BuildingPhase.Constructing)
        .Sum(building => (long)Definition(content, building.TypeId).GetLevel(building.Level).WorkersProvided);

    public static BuildingDefinition Definition(GameContent content, BuildingTypeId type) =>
        content.TryGetBuilding(type, out BuildingDefinition? definition) ? definition : throw new InvalidOperationException($"Unknown type {type}.");

    private static Building Idle(Building building, int level, int workers) =>
        new(building.Id, building.TypeId, building.X, building.Y, level, workers, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
}
