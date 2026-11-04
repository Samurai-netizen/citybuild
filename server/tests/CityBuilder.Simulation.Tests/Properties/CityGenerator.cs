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
    public const int MaxAttempts = 120;

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
    public static CityOperation.Command? RandomCommand(SeededRandom random, CityState city, GameContent content) => random.Between(1, 70) switch
    {
        <= 15 => PlaceBuilding(random, city, content),
        <= 30 => AssignWorkers(random, city, content),
        <= 40 => UpgradeBuilding(random, city),
        <= 50 => StartResearch(random, city, content),
        _ => StartTrip(random, city, content),
    };

    /// <summary>
    /// The next operation to try: time passes in 30 of 100 attempts; the rest are commands, trips (which bring every project and producer its
    /// goods) the most, then placing and assigning workers, then upgrading and research. A trip with nothing to carry gives way to a placement
    /// or time passing, so young cities grow and fill their stores. Null when the chosen command has nothing to act on.
    /// </summary>
    private static CityOperation? Next(SeededRandom random, CityState city, GameContent content) => random.Between(1, 100) switch
    {
        <= 30 => new CityOperation.Advance(Gap(random, content)),
        <= 42 => PlaceBuilding(random, city, content),
        <= 54 => AssignWorkers(random, city, content),
        <= 64 => UpgradeBuilding(random, city),
        <= 72 => StartResearch(random, city, content),
        _ => (CityOperation?)StartTrip(random, city, content)
            ?? (random.Chance(50) ? PlaceBuilding(random, city, content) : new CityOperation.Advance(Gap(random, content))),
    };

    /// <summary>
    /// A trip that can usually go: a target and a good it accepts (three times in four one its waiting project needs, when there is one, else
    /// half the time a producer's input) that another building has in its store, from such a building, with a random amount (mostly a preset; a percentage or exact number the rules
    /// may refuse while its mode is locked). When no worker is free, a worker change that frees one instead (a player who wants to carry
    /// goods does that). Without such a pair, one time in ten a random trip, which the rules usually refuse, else null; null with fewer than
    /// two buildings.
    /// </summary>
    private static CityOperation.Command? StartTrip(SeededRandom random, CityState city, GameContent content)
    {
        if (city.Buildings.Count < 2)
        {
            return null;
        }

        List<Building> staffed = [.. city.Buildings.Where(building => building.AssignedWorkers > 0)];
        if (CityQueries.FreeWorkers(city, content) < 1 && staffed.Count > 0)
        {
            Building busy = random.Pick(staffed);
            return new CityOperation.AssignWorkers(busy.Id, busy.AssignedWorkers - 1);
        }

        var pairs = new List<(Building Target, ItemId Item, bool ForProject, bool ForInput)>();
        foreach (Building target in city.Buildings)
        {
            RecipeDefinition? recipe = content.TryGetBuilding(target.TypeId, out BuildingDefinition? type) ? type.GetLevel(target.Level).Recipe : null;
            foreach (ItemDefinition item in content.Items.Where(item => !item.IsCurrency))
            {
                bool forProject = target.GetNeed(item.Id) > 0 || CityQueries.ResearchNeedAt(city, content, target, item.Id) > 0;
                bool forInput = recipe is not null && recipe.Inputs.Any(input => input.Item == item.Id);
                bool supplied = city.Buildings.Any(source => source.Id != target.Id && source.GetStock(item.Id) > 0);
                if (supplied && CityQueries.Accepts(city, content, target, item.Id))
                {
                    pairs.Add((target, item.Id, forProject, forInput));
                }
            }
        }

        Amount amount = RandomAmount(random, city, content);
        if (pairs.Count == 0)
        {
            if (!random.Chance(10))
            {
                return null;
            }

            Building from = random.Pick(city.Buildings);
            Building to = random.Pick(city.Buildings);
            return new CityOperation.StartTrip(from.Id, to.Id, random.Pick(content.Items).Id.ToString(), amount);
        }

        // Three times in four a project's need, else half the time a producer's input, so chains run and upgrades start.
        var forProjects = pairs.Where(pair => pair.ForProject).ToList();
        var forInputs = pairs.Where(pair => pair.ForInput).ToList();
        (Building chosenTarget, ItemId chosenItem, _, _) = forProjects.Count > 0 && random.Chance(75) ? random.Pick(forProjects)
            : forInputs.Count > 0 && random.Chance(50) ? random.Pick(forInputs) : random.Pick(pairs);
        Building chosenSource = random.Pick(city.Buildings.Where(source => source.Id != chosenTarget.Id && source.GetStock(chosenItem) > 0).ToList());
        return new CityOperation.StartTrip(chosenSource.Id, chosenTarget.Id, chosenItem.ToString(), amount);
    }

    /// <summary>
    /// A random amount: with a mode research unlocked, that mode's amount 40 % of the time (a percentage 1..100, an exact number 1..10);
    /// otherwise a preset of the content, except one time in 20 a percentage or exact number, which the rules refuse while it is locked.
    /// </summary>
    private static Amount RandomAmount(SeededRandom random, CityState city, GameContent content)
    {
        AmountMode[] unlocked = [.. Enum.GetValues<AmountMode>().Where(mode => CityQueries.IsAmountModeUnlocked(city, content, mode))];
        bool modeAmount = unlocked.Length > 0 ? random.Chance(40) : random.Chance(5);
        if (!modeAmount)
        {
            return Amount.Preset(random.Pick(content.AmountPresets).Id);
        }

        AmountMode chosen = unlocked.Length > 0 ? random.Pick(unlocked) : random.Pick(Enum.GetValues<AmountMode>());
        return chosen == AmountMode.Percent ? Amount.Percent((int)random.Between(1, 100)) : Amount.Exact(random.Between(1, 10));
    }

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

        // A city that lacks an essential type it may place gets the first one it lacks 40 % of the time: a type research just unlocked (what
        // the research was for), workers, a producer of a good from nothing, the science building (every chain, project and research needs
        // the last three since CR-0008). Otherwise, three times in four a type the city doesn't
        // have yet (when there is one), so the coins aren't all spent on the first types and later chains get built.
        var newTypes = content.Buildings.Where(type => !city.Buildings.Any(building => building.TypeId == type.Id)).ToList();
        var placeable = newTypes.Where(type => CityQueries.IsUnlocked(city, content, type.Id)).ToList();
        BuildingDefinition? essential = placeable.FirstOrDefault(type => !type.IsAvailableAtStart)
            ?? placeable.FirstOrDefault(type => type.GetLevel(1).WorkersProvided > 0)
            ?? placeable.FirstOrDefault(type => type.GetLevel(1).Recipe is { Inputs.Count: 0 } recipe && recipe.Outputs.Any(output => output.Item != content.Currency))
            ?? placeable.FirstOrDefault(type => type.Kind == BuildingKind.Science);
        BuildingDefinition chosen = essential is not null && random.Chance(40) ? essential
            : newTypes.Count > 0 && random.Chance(75) ? random.Pick(newTypes) : random.Pick(content.Buildings);
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
    /// A research start of a random node: three times in four one the city could start now (not completed, its era, its prerequisites done)
    /// when there is one, else one that isn't completed yet when there is one; the rules still check the slot, the science building, the
    /// prerequisites, the era and the cost.
    /// </summary>
    private static CityOperation.StartResearch StartResearch(SeededRandom random, CityState city, GameContent content)
    {
        var open = content.Research.Where(node => !city.HasCompletedResearch(node.Id)).ToList();
        var ready = open.Where(node => node.Era == city.Era && node.Prerequisites.All(city.HasCompletedResearch)).ToList();
        return new CityOperation.StartResearch(random.Pick(ready.Count > 0 && random.Chance(75) ? ready : open.Count > 0 ? open : content.Research).Id.ToString());
    }

    /// <summary>An upgrade of a random building; null when the city has no building. The rules still decide whether it can be upgraded.</summary>
    private static CityOperation.UpgradeBuilding? UpgradeBuilding(SeededRandom random, CityState city) =>
        city.Buildings.Count == 0 ? null : new CityOperation.UpgradeBuilding(random.Pick(city.Buildings).Id);
}
