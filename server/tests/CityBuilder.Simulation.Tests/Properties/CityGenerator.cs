using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// Makes a random city from a seed: from the starting city, a random number of random operations, each run through the rules
/// (<see cref="CityOperation"/>), so every city it makes is one a player could reach. The commands are random but aimed: tiles are free,
/// buildings and research nodes exist, workers are within the level's limit; the rules still decide, and a rejected command is dropped.
/// Gaps between commands range from 0 s to four long gaps (<see cref="LongGapOf"/>), so cities are seen mid-cycle, stalled, with tired
/// couriers and after long absences; some gaps are absences that start within the presence grace (<c>04</c> section 2).
/// </summary>
internal static class CityGenerator
{
    /// <summary>The most commands one history tries; each history tries a random number from 0 up to this.</summary>
    public const int MaxAttempts = 120;

    /// <summary>The longest presence grace a generated absence starts after: the server's 300 s (<c>04</c> section 2).</summary>
    public const long MaxGraceSeconds = 300;

    /// <summary>
    /// The length of a long gap on <paramref name="content"/>: the offline cap each property content had before CR-0008-T1 part H removed it
    /// (1 hour for the fixture, 8 hours for golden-v2), so the seeds keep the scale of their gaps and the stepped observation its cost. The
    /// third quarter of gaps reaches it, the fourth goes up to four of them.
    /// </summary>
    public static long LongGapOf(GameContent content) => ReferenceEquals(content, CityFixture.TestContent) ? 3600 : 8 * 3600;

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
    /// A random gap: a quarter each up to a minute, up to ten minutes, up to <paramref name="longGap"/>, and beyond it (up to four of them).
    /// </summary>
    public static long Gap(SeededRandom random, long longGap) => random.Between(1, 4) switch
    {
        1 => random.Between(0, 60),
        2 => random.Between(61, 600),
        3 => random.Between(601, longGap),
        _ => random.Between(longGap + 1, 4 * longGap),
    };

    /// <summary>
    /// Time passing for a random gap (<see cref="Gap"/>), with the player present or absent. Whether and from when the player is absent comes
    /// from the gap itself, not from more random draws: the seeds share one random stream (seed n starts n draws into seed 0's), so one more
    /// draw per gap reshuffles every history, and the seeds kept the cities they reached before part H. A gap over ten minutes is an
    /// absence two times in five (<c>gap % 5 &lt; 2</c>), from 0 to <see cref="MaxGraceSeconds"/> in (the player left; couriers spend stamina
    /// after the grace), else presence (the game stayed open, as the client's display always advances); a shorter gap is an absence one time
    /// in four (<c>gap % 4 == 0</c>), from anywhere in it.
    /// </summary>
    public static CityOperation.Advance TimePasses(SeededRandom random, long longGap)
    {
        long gap = Gap(random, longGap);
        long? absentAfter = gap > 600 ? (gap % 5 < 2 ? gap / 5 % (MaxGraceSeconds + 1) : null)
            : gap % 4 == 0 ? gap / 4 % (gap + 1) : null;
        return new CityOperation.Advance(gap, absentAfter);
    }

    /// <summary>A random command (no time passing), chosen in the same proportions as the generator's commands.</summary>
    public static CityOperation.Command? RandomCommand(SeededRandom random, CityState city, GameContent content) => random.Between(1, 70) switch
    {
        <= 15 => PlaceBuilding(random, city, content),
        <= 30 => AssignWorkers(random, city, content),
        <= 40 => UpgradeBuilding(random, city),
        <= 50 => StartResearch(random, city, content),
        <= 58 => (CityOperation.Command?)SetRoute(random, city, content) ?? StartTrip(random, city, content),
        _ => StartTrip(random, city, content),
    };

    /// <summary>
    /// The next operation to try: time passes in 26 of 100 attempts (the couriers are re-energized instead when one is tired) and a route is
    /// set in 4 (time passes instead in a city without a courier station); the rest are commands, trips (which bring every project and producer its goods) the most, then placing and assigning workers,
    /// then upgrading and research. A trip with nothing to carry gives way to a placement or time passing, so young cities grow and fill their stores. Null
    /// when the chosen command has nothing to act on.
    /// </summary>
    private static CityOperation? Next(SeededRandom random, CityState city, GameContent content) => random.Between(1, 100) switch
    {
        <= 26 => (CityOperation?)Reenergize(city) ?? TimePasses(random, LongGapOf(content)),
        <= 30 => (CityOperation?)SetRoute(random, city, content) ?? TimePasses(random, LongGapOf(content)),
        <= 42 => PlaceBuilding(random, city, content),
        <= 54 => AssignWorkers(random, city, content),
        <= 64 => UpgradeBuilding(random, city),
        <= 72 => StartResearch(random, city, content),
        _ => (CityOperation?)StartTrip(random, city, content)
            ?? (random.Chance(50) ? PlaceBuilding(random, city, content) : TimePasses(random, LongGapOf(content))),
    };

    /// <summary>
    /// The player, back in the city, re-energizes the couriers (<c>04</c> section 8) when one is tired (resting, or walking home tired); null
    /// otherwise, so time passes instead. It draws no random number, so the seeds' streams stay as they were.
    /// </summary>
    private static CityOperation.ReenergizeCouriers? Reenergize(CityState city) =>
        city.Couriers.Any(courier => courier.State == CourierState.Resting || (courier.State == CourierState.Returning && courier.Stamina == 0))
            ? new CityOperation.ReenergizeCouriers() : null;

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

        List<(Building Target, ItemId Item, bool ForProject, bool ForInput)> pairs = CarryPairs(city, content);
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
    /// Every (target, good) that goods could go to now: the target accepts the good (a need of its project or of the research there, or its
    /// store) and another building has some of it in its store; with whether it is for a project and whether it is a producer's input.
    /// </summary>
    private static List<(Building Target, ItemId Item, bool ForProject, bool ForInput)> CarryPairs(CityState city, GameContent content)
    {
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

        return pairs;
    }

    /// <summary>
    /// A route for a courier station's slot, aimed like a trip (<c>04</c> section 19): four times in five, when goods could go somewhere, a
    /// building that has a good (take it) and one that accepts it (put it), sometimes with a third stop that puts the rest elsewhere; else two
    /// random buildings with random takes and puts. The slot is mostly one that has a courier, one without delivery lines when the station has
    /// one; one time in 20 the route is cleared. Amounts as
    /// <see cref="RandomAmount"/>. When every courier of the station has delivery lines (it takes no route, CR-0008 D19) the chosen one's
    /// lines are cancelled instead (<see cref="LinesOrRoute"/>). Null without a courier station or with fewer than two
    /// buildings.
    /// </summary>
    private static CityOperation.Command? SetRoute(SeededRandom random, CityState city, GameContent content)
    {
        var stations = city.Buildings
            .Where(building => content.TryGetBuilding(building.TypeId, out BuildingDefinition? type) && type.Kind == BuildingKind.CourierStation)
            .ToList();
        if (stations.Count == 0 || city.Buildings.Count < 2)
        {
            return null;
        }

        Building station = random.Pick(stations);
        int levelSlots = content.TryGetBuilding(station.TypeId, out BuildingDefinition? stationType) ? stationType.GetLevel(station.Level).MaxWorkers : 1;
        int slot = station.AssignedWorkers > 0 && random.Chance(80)
            ? (int)random.Between(1, station.AssignedWorkers)
            : (int)random.Between(1, Math.Max(1, levelSlots));
        // A courier with delivery lines takes no route (CR-0008 D19): move to the first slot of the station without lines, if one has none,
        // with no extra draw, so a history only changes where the rule would refuse the route.
        for (int other = 1; city.CountLines(station.Id, slot) > 0 && other <= station.AssignedWorkers; other++)
        {
            if (city.CountLines(station.Id, other) == 0)
            {
                slot = other;
            }
        }

        if (random.Chance(5))
        {
            return LinesOrRoute(city, station.Id, slot, []);
        }

        List<(Building Target, ItemId Item, bool ForProject, bool ForInput)> pairs = CarryPairs(city, content);
        var stops = new List<RouteStop>();
        if (pairs.Count > 0 && random.Chance(80))
        {
            (Building target, ItemId item, _, _) = random.Pick(pairs);
            Building source = random.Pick(city.Buildings.Where(building => building.Id != target.Id && building.GetStock(item) > 0).ToList());
            stops.Add(new RouteStop(source.Id, [new RouteAction(RouteActionKind.Take, item, RandomAmount(random, city, content))]));
            stops.Add(new RouteStop(target.Id, [new RouteAction(RouteActionKind.Put, item, RandomAmount(random, city, content))]));
            List<Building> others = [.. city.Buildings.Where(building => building.Id != source.Id && building.Id != target.Id)];
            if (others.Count > 0 && random.Chance(25))
            {
                stops.Add(new RouteStop(random.Pick(others).Id, [new RouteAction(RouteActionKind.Put, item, RandomAmount(random, city, content))]));
            }
        }
        else
        {
            Building first = random.Pick(city.Buildings);
            Building second = random.Pick(city.Buildings.Where(building => building.Id != first.Id).ToList());
            foreach (Building stop in new[] { first, second })
            {
                ItemId item = random.Pick(content.Items.Where(candidate => !candidate.IsCurrency).ToList()).Id;
                RouteActionKind kind = random.Chance(50) ? RouteActionKind.Take : RouteActionKind.Put;
                stops.Add(new RouteStop(stop.Id, [new RouteAction(kind, item, RandomAmount(random, city, content))]));
            }
        }

        return LinesOrRoute(city, station.Id, slot, stops);
    }

    /// <summary>
    /// The route, or, when the slot's courier has delivery lines (it takes no route, CR-0008 D19), the cancel of all of them, or of its first
    /// line when it has 4 or more and that line's id is a multiple of 4. Decided after the route's draws and without one of its own, so the histories keep the
    /// random draws they had while such routes were refused.
    /// </summary>
    private static CityOperation.Command LinesOrRoute(CityState city, int stationId, int slot, List<RouteStop> stops)
    {
        int lines = city.CountLines(stationId, slot);
        if (lines == 0)
        {
            return new CityOperation.SetRoute(stationId, slot, stops);
        }

        // Cancelling all frees the courier for a later route; one line in four or more is cancelled alone, so both kinds happen.
        city.TryGetFirstLine(stationId, slot, out DeliveryLine? first);
        return new CityOperation.CancelDelivery(stationId, slot, lines >= 4 && first!.Id % 4 == 0 ? first.Id : null);
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
