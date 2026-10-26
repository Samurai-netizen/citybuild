using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// Makes a random city from a seed: from the starting city, a random number of random operations, each run through the rules
/// (<see cref="CityOperation"/>), so every city it makes is one a player could reach. The commands are random but aimed: tiles are free,
/// buildings and research nodes exist, workers are within the level's limit; the rules still decide, and a rejected command is dropped.
/// Gaps between commands range from 0 s to several production caps, so cities are seen mid-cycle, stalled, dormant and after long absences.
/// </summary>
internal static class CityGenerator
{
    /// <summary>The most commands one history tries; each history tries a random number from 0 up to this.</summary>
    public const int MaxAttempts = 80;

    /// <summary>The history of seed <paramref name="seed"/> over <paramref name="content"/>. The same seed always gives the same history.</summary>
    public static CityHistory Generate(GameContent content, int seed)
    {
        var random = new SeededRandom(seed);
        CityState city = CityFactory.CreateStarterCity(content);
        var operations = new List<CityOperation>();
        var reports = new List<SettlementReport>();
        int rejected = 0;
        long attempts = random.Between(0, MaxAttempts);
        for (long attempt = 0; attempt < attempts; attempt++)
        {
            CityOperation? operation = Next(random, city, content);
            SettlementReport? report = operation?.TryApply(city, content);
            if (operation is null || report is null)
            {
                rejected += operation is null ? 0 : 1;
                continue;
            }

            operations.Add(operation);
            reports.Add(report);
        }

        return new CityHistory(seed, city, operations, reports, rejected);
    }

    /// <summary>
    /// A random gap: a quarter each up to a minute, up to ten minutes, up to the production cap, and beyond it (up to four caps), so most
    /// gaps keep production running and some make the city dormant.
    /// </summary>
    public static long Gap(SeededRandom random, GameContent content)
    {
        long cap = content.Rules.MaxOfflineProductionSeconds;
        return random.Between(1, 4) switch
        {
            1 => random.Between(0, 60),
            2 => random.Between(61, 600),
            3 => random.Between(601, Math.Max(601, cap)),
            _ => random.Between(cap + 1, 4 * cap),
        };
    }

    /// <summary>A random command (no time passing), chosen in the same proportions as the generator's commands.</summary>
    public static CityOperation.Command? RandomCommand(SeededRandom random, CityState city, GameContent content) => random.Between(1, 65) switch
    {
        <= 20 => PlaceBuilding(random, city, content),
        <= 40 => AssignWorkers(random, city, content),
        <= 50 => UpgradeBuilding(random, city),
        _ => StartResearch(random, city, content),
    };

    /// <summary>
    /// The next operation to try: time passes in about a third of the attempts (35 in 100); the rest are commands, placing and assigning
    /// workers more often than upgrading and research. Null when the chosen command has nothing to act on.
    /// </summary>
    private static CityOperation? Next(SeededRandom random, CityState city, GameContent content) => random.Between(1, 100) switch
    {
        <= 35 => new CityOperation.Advance(Gap(random, content)),
        <= 55 => PlaceBuilding(random, city, content),
        <= 75 => AssignWorkers(random, city, content),
        <= 85 => UpgradeBuilding(random, city),
        _ => StartResearch(random, city, content),
    };

    /// <summary>
    /// A placement on a random free tile, of a type the city doesn't have yet three times in four (when there is one), else of any type; null
    /// when no tile is free. The rules still decide whether the type is unlocked and affordable.
    /// </summary>
    private static CityOperation.PlaceBuilding? PlaceBuilding(SeededRandom random, CityState city, GameContent content)
    {
        var freeTiles = new List<(int X, int Y)>();
        for (int y = 0; y < content.Rules.GridHeight; y++)
        {
            for (int x = 0; x < content.Rules.GridWidth; x++)
            {
                if (!city.Buildings.Any(building => building.X == x && building.Y == y))
                {
                    freeTiles.Add((x, y));
                }
            }
        }

        if (freeTiles.Count == 0)
        {
            return null;
        }

        // Three times in four a type the city doesn't have yet (when there is one), so the coins aren't all spent on the first types and later
        // chains get built.
        var newTypes = content.Buildings.Where(type => !city.Buildings.Any(building => building.TypeId == type.Id)).ToList();
        BuildingDefinition chosen = newTypes.Count > 0 && random.Chance(75) ? random.Pick(newTypes) : random.Pick(content.Buildings);
        (int tileX, int tileY) = random.Pick(freeTiles);
        return new CityOperation.PlaceBuilding(chosen.Id.ToString(), tileX, tileY);
    }

    /// <summary>A worker change on a random building, to a random count from 0 to its level's limit; null when the city has no building.</summary>
    private static CityOperation.AssignWorkers? AssignWorkers(SeededRandom random, CityState city, GameContent content)
    {
        if (city.Buildings.Count == 0)
        {
            return null;
        }

        Building building = random.Pick(city.Buildings);
        if (!content.TryGetBuilding(building.TypeId, out BuildingDefinition? type))
        {
            throw new InvalidOperationException($"Building {building.Id} has the unknown type {building.TypeId}.");
        }

        return new CityOperation.AssignWorkers(building.Id, (int)random.Between(0, type.GetLevel(building.Level).MaxWorkers));
    }

    /// <summary>
    /// A research start of a random node, one that isn't completed yet when there is one; the rules still check the slot, the prerequisites,
    /// the era and the cost.
    /// </summary>
    private static CityOperation.StartResearch StartResearch(SeededRandom random, CityState city, GameContent content)
    {
        var open = content.Research.Where(node => !city.HasCompletedResearch(node.Id)).ToList();
        return new CityOperation.StartResearch(random.Pick(open.Count > 0 ? open : content.Research).Id.ToString());
    }

    /// <summary>An upgrade of a random building; null when the city has no building. The rules still decide whether it can be upgraded.</summary>
    private static CityOperation.UpgradeBuilding? UpgradeBuilding(SeededRandom random, CityState city) =>
        city.Buildings.Count == 0 ? null : new CityOperation.UpgradeBuilding(random.Pick(city.Buildings).Id);
}
