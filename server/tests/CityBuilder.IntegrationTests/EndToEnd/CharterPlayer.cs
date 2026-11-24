using CityBuilder.Infrastructure.Content;
using CityBuilder.IntegrationTests.Content;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.IntegrationTests.EndToEnd;

/// <summary>
/// A scripted player that plays the server's Era I content (<c>server/content/era1-content.json</c>, M1-02) from a new city to the last
/// research, the Town Charter. It looks at the city every <see cref="TurnSeconds"/> and decides from the engine's state alone, so it is
/// deterministic: it starts the next research of <see cref="_researchOrder"/> it can, places the next building of <see cref="_buildPlan"/>
/// it can, bridges the river once it may, staffs producers, and sends workers' trips (no courier station, so presence never matters) with the goods of every project
/// (constructions and research), with each producer's inputs, and with full producers' outputs to the warehouse. <see cref="Script"/> plays
/// it on the engine once and keeps every accepted act with its time; <see cref="CharterHttpTests"/> sends the same acts over HTTP.
/// The player chooses ids and tiles; every number it acts on (costs, workers, limits, recipes) comes from the content.
/// </summary>
internal static class CharterPlayer
{
    /// <summary>Seconds between two looks of the player at the city.</summary>
    public const long TurnSeconds = 600;

    /// <summary>The longest the script may play before it counts as stuck: 14 days.</summary>
    public const long LimitSeconds = 14 * 24 * 3600;

    /// <summary>The presence grace of the tests' server in seconds (<c>04</c> §2); only couriers care, and the player builds none.</summary>
    public const long GraceSeconds = 300;

    /// <summary>The last research of the era, the one the script plays to.</summary>
    public static readonly ResearchId Charter = new("town_charter");

    /// <summary>The free workers the player keeps for trips when it staffs producers.</summary>
    private const long _tripWorkers = 6;

    /// <summary>The most acts the player tries in one turn: a guard against a decision that loops.</summary>
    private const int _maxTriesPerTurn = 80;

    /// <summary>The research in the order the player starts it (each once its prerequisites are done and the purse holds its coins).</summary>
    private static readonly string[] _researchOrder =
    [
        "trade_charter", "sharp_axes", "tally_sticks", "sturdy_boots", "storehouses", "frame_saw", "stone_cutting", "timber_framing",
        "honest_scales", "open_fields", "bridge_building", "land_survey", "milling", "masonry", "guild_of_masons", "town_charter",
    ];

    /// <summary>
    /// The buildings in the order the player places them, each on its tile: a building waits for its research and its coins, and the ones
    /// after it wait too. Houses are not in the plan: <see cref="NextPlacement"/> places one on the next of <see cref="_houseTiles"/>
    /// whenever the workers of the planned buildings would leave fewer than <see cref="_tripWorkers"/> free.
    /// </summary>
    /// The tiles are on the Era I map (M1-03): area A west of the river, the Woodcutter's Huts beside forest, the Quarry beside rock, the Farm
    /// on the east bank's fertile soil (area C, opened by Open Fields), reached over <see cref="_bridge"/>.
    private static readonly (string Type, int X, int Y)[] _buildPlan =
    [
        ("scriptorium", 4, 4), ("woodcutter", 4, 2), ("sawmill", 5, 2), ("trading_post", 6, 3), ("warehouse", 6, 4), ("woodcutter", 4, 3),
        ("sawmill", 5, 3), ("woodcutter", 4, 5), ("sawmill", 5, 5), ("quarry", 8, 3), ("stonemason", 7, 4), ("stonemason", 8, 4),
        ("farm", 13, 3), ("windmill", 10, 5), ("bakery", 9, 5),
    ];

    /// <summary>The tiles of the houses, in the order the player uses them: rows 7 and 8 of area A, from column 2.</summary>
    private static readonly (int X, int Y)[] _houseTiles = [.. Enumerable.Range(0, 16).Select(index => (2 + (index % 8), 7 + (index / 8)))];

    /// <summary>The river tile the player bridges once Bridge Building is done, so walks reach the Farm (M1-03).</summary>
    private static readonly (int X, int Y) _bridge = (12, 5);

    /// <summary>The script and the engine's city at its end, played once per test run.</summary>
    private static readonly Lazy<(IReadOnlyList<(long Time, LoopAct[] Acts)> Steps, CityState End)> _played = new(Play);

    /// <summary>The server's Era I content, loaded as the server loads it.</summary>
    public static GameContent Content { get; } = ContentFileLoader.Load(ContentFiles.RealEra1ContentPath);

    /// <summary>The turns with at least one accepted act: city-clock second → the acts, in the order they were accepted.</summary>
    public static IReadOnlyList<(long Time, LoopAct[] Acts)> Script => _played.Value.Steps;

    /// <summary>The engine's city at the end of the script: the turn the Town Charter completed, or the limit.</summary>
    public static CityState End => _played.Value.End;

    /// <summary>
    /// Replays the script on the engine as the server runs it, up to the step at <paramref name="time"/>: each step settles the city to its
    /// time with the player away from <see cref="GraceSeconds"/> after the step before, then applies its acts, each followed by a
    /// zero-length advance.
    /// </summary>
    /// <param name="time">The city clock to stop at; one of the steps' times.</param>
    /// <returns>The city.</returns>
    public static CityState Replay(long time)
    {
        CityState city = CityFactory.CreateStarterCity(Content);
        long? lastRequest = null;
        foreach ((long stepTime, LoopAct[] acts) in Script.Where(step => step.Time <= time))
        {
            Simulator.Advance(city, Content, stepTime - city.Clock, lastRequest + GraceSeconds);
            foreach (LoopAct act in acts)
            {
                Assert.True(EraOneLoop.Mutate(city, Content, act).IsOk, $"t={stepTime} {act}\n{EraOneLoop.Describe(city)}");
                Simulator.Advance(city, Content, 0);
            }

            lastRequest = stepTime;
        }

        return city;
    }

    /// <summary>
    /// Plays the player on the engine from a new city, a turn every <see cref="TurnSeconds"/>, until the Town Charter is complete or the
    /// limit is reached. A turn settles the city to its time (away from <see cref="GraceSeconds"/> after the last turn that sent a request),
    /// then tries the player's candidate acts one by one; an act the engine refuses is dropped (it changed nothing), an accepted one is kept.
    /// </summary>
    /// <returns>The kept acts per turn and the city at the end.</returns>
    private static (IReadOnlyList<(long Time, LoopAct[] Acts)> Steps, CityState End) Play()
    {
        CityState city = CityFactory.CreateStarterCity(Content);
        var steps = new List<(long Time, LoopAct[] Acts)>();
        long? lastRequest = null;
        for (long time = 0; time <= LimitSeconds && !city.CompletedResearch.Contains(Charter); time += TurnSeconds)
        {
            Simulator.Advance(city, Content, time - city.Clock, lastRequest + GraceSeconds);
            var accepted = new List<LoopAct>();
            var tried = new HashSet<LoopAct>();
            while (tried.Count < _maxTriesPerTurn && Candidates(city).FirstOrDefault(candidate => !tried.Contains(candidate)) is { } act)
            {
                tried.Add(act);
                if (EraOneLoop.Mutate(city, Content, act).IsOk)
                {
                    Simulator.Advance(city, Content, 0);
                    accepted.Add(act);
                }
            }

            if (accepted.Count > 0)
            {
                steps.Add((time, [.. accepted]));
                lastRequest = time;
            }
        }

        return (steps, city);
    }

    /// <summary>The acts the player would like to send now, most wanted first: research, staffing, project goods, inputs, overflow, building, the bridge.</summary>
    /// <param name="city">The city, settled to the turn's time.</param>
    /// <returns>The candidates; the engine may refuse any of them.</returns>
    private static IEnumerable<LoopAct> Candidates(CityState city)
    {
        if (city.ActiveResearch is null && NextResearch(city) is { } research)
        {
            yield return new ResearchAct(research.Id.Value);
        }

        foreach (LoopAct act in Staffing(city).Concat(ProjectTrips(city)).Concat(InputTrips(city)).Concat(OverflowTrips(city)))
        {
            yield return act;
        }

        if (NextPlacement(city) is { } place)
        {
            yield return place;
        }

        if (CityQueries.IsBridgeUnlocked(city, Content) && !city.TryGetRoad(_bridge.X, _bridge.Y, out _) && city.Purse >= Coins(Content.Bridge!.Cost))
        {
            yield return new RoadAct(_bridge.X, _bridge.Y);
        }
    }

    /// <summary>
    /// The first research of <see cref="_researchOrder"/> not done whose prerequisites are done and whose coins the purse holds on top of the
    /// coins of the next building of the plan (locked or not), so research never starves the economy it pays from.
    /// </summary>
    /// <param name="city">The city.</param>
    /// <returns>The research, or null.</returns>
    private static ResearchDefinition? NextResearch(CityState city)
    {
        long reserve = NextPlanned(city) is { } planned ? Coins(planned.Cost) : 0;
        foreach (string id in _researchOrder)
        {
            Assert.True(Content.TryGetResearch(new ResearchId(id), out ResearchDefinition? node), id);
            if (!city.CompletedResearch.Contains(node.Id))
            {
                bool ready = node.Prerequisites.All(city.CompletedResearch.Contains) && city.Purse >= Coins(node.Cost) + reserve;
                if (ready)
                {
                    return node;
                }
            }
        }

        return null;
    }

    /// <summary>The type of the next building of <see cref="_buildPlan"/> not placed yet, or null when all are.</summary>
    /// <param name="city">The city.</param>
    /// <returns>The type.</returns>
    private static BuildingDefinition? NextPlanned(CityState city)
    {
        foreach ((string type, int x, int y) in _buildPlan)
        {
            if (!city.Buildings.Any(building => building.X == x && building.Y == y))
            {
                Assert.True(Content.TryGetBuilding(new BuildingTypeId(type), out BuildingDefinition? building), type);
                return building;
            }
        }

        return null;
    }

    /// <summary>
    /// The next building of <see cref="_buildPlan"/> not placed yet, when it is unlocked and the purse holds its coins; or first a house, when
    /// the houses (built or not) would provide fewer workers than every staffed building (built or not) and the next one take at most, plus
    /// <see cref="_tripWorkers"/>: the engine staffs a building when it completes, so the carriers must be housed before.
    /// </summary>
    /// <param name="city">The city.</param>
    /// <returns>The placement, or null.</returns>
    private static PlaceAct? NextPlacement(CityState city)
    {
        if (NextPlanned(city) is not { } next)
        {
            return null;
        }

        long provided = city.Buildings.Sum(building => (long)Definition(building).GetLevel(building.Level).WorkersProvided);
        long taken = city.Buildings.Sum(building => (long)Definition(building).GetLevel(building.Level).MaxWorkers) + next.GetLevel(1).MaxWorkers;
        if (provided < taken + _tripWorkers)
        {
            (int x, int y) = _houseTiles.First(tile => !city.Buildings.Any(building => building.X == tile.X && building.Y == tile.Y));
            return new PlaceAct("house", x, y);
        }

        (string type, int planX, int planY) = _buildPlan.First(entry => !city.Buildings.Any(building => building.X == entry.X && building.Y == entry.Y));
        return CityQueries.IsUnlocked(city, Content, next.Id) && city.Purse >= Coins(next.Cost) ? new PlaceAct(type, planX, planY) : null;
    }

    /// <summary>Workers for each producer short of its level's maximum, keeping <see cref="_tripWorkers"/> free for trips.</summary>
    /// <param name="city">The city.</param>
    /// <returns>The assignments.</returns>
    private static IEnumerable<LoopAct> Staffing(CityState city)
    {
        long spare = CityQueries.WorkersProvided(city, Content) - CityQueries.WorkersAssigned(city) - _tripWorkers;
        foreach (Building building in Producers(city).Where(building => building.Phase is BuildingPhase.Idle or BuildingPhase.Producing))
        {
            int max = Definition(building).GetLevel(building.Level).MaxWorkers;
            if (spare > 0 && building.AssignedWorkers < max)
            {
                int workers = (int)Math.Min(max, building.AssignedWorkers + spare);
                spare -= workers - building.AssignedWorkers;
                yield return new AssignAct(building.Id, workers);
            }
        }
    }

    /// <summary>Trips with the goods each project still waits for (construction sites, upgrades, the running research), beyond what is on its way.</summary>
    /// <param name="city">The city.</param>
    /// <returns>The trips.</returns>
    private static IEnumerable<LoopAct> ProjectTrips(CityState city)
    {
        var projects = city.Buildings.Select(building => (Target: building, Needs: building.Needs)).ToList();
        if (city.ActiveResearch is { } research && CityQueries.ScienceBuilding(city, Content) is { } science)
        {
            projects.Add((science, research.Needs));
        }

        foreach ((Building target, IReadOnlyDictionary<ItemId, long> needs) in projects)
        {
            foreach ((ItemId item, long need) in needs.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal))
            {
                long missing = need - OnTheWay(city, target.Id, item);
                if (missing > 0 && Trip(city, target, item, missing) is { } trip)
                {
                    yield return trip;
                }
            }
        }
    }

    /// <summary>Trips with each producer's recipe inputs, while its store has room for at least a worker's load more than is on its way.</summary>
    /// <param name="city">The city.</param>
    /// <returns>The trips.</returns>
    private static IEnumerable<LoopAct> InputTrips(CityState city)
    {
        foreach (Building consumer in Producers(city).Where(building => building.IsBuilt))
        {
            foreach (ItemQuantity input in Recipe(consumer).Inputs)
            {
                long room = CityQueries.Room(city, Content, consumer, input.Item) - OnTheWay(city, consumer.Id, input.Item);
                if (room >= Content.Rules.TripCarryLimit && Trip(city, consumer, input.Item, room) is { } trip)
                {
                    yield return trip;
                }
            }
        }
    }

    /// <summary>Half the output of each producer whose store is at least half full, to the first warehouse with room, unless such a trip is on its way.</summary>
    /// <param name="city">The city.</param>
    /// <returns>The trips.</returns>
    private static IEnumerable<LoopAct> OverflowTrips(CityState city)
    {
        Building? warehouse = city.Buildings.FirstOrDefault(building => Definition(building).Kind == BuildingKind.Warehouse && building.IsBuilt);
        if (warehouse is null)
        {
            yield break;
        }

        foreach (Building producer in Producers(city))
        {
            foreach (ItemQuantity output in Recipe(producer).Outputs.Where(output => output.Item != Content.Currency))
            {
                long stock = producer.Store.GetValueOrDefault(output.Item);
                bool full = CityQueries.TryGetEffectiveLimit(city, Content, producer, output.Item, out long limit) && stock * 2 >= limit;
                bool sending = city.Trips.Any(trip => trip.SourceId == producer.Id && trip.Item == output.Item);
                if (full && !sending && CityQueries.Room(city, Content, warehouse, output.Item) >= stock)
                {
                    yield return new TripAct(producer.Id, warehouse.Id, output.Item.Value, "half");
                }
            }
        }
    }

    /// <summary>
    /// A trip of <paramref name="item"/> to <paramref name="target"/> from the building with the most of it (a warehouse or a producer that
    /// makes it, never another consumer's input store), with the largest preset that sends no more than <paramref name="wanted"/> (a quarter
    /// when even that is more).
    /// </summary>
    /// <param name="city">The city.</param>
    /// <param name="target">Where the goods go.</param>
    /// <param name="item">The good.</param>
    /// <param name="wanted">How many the target still wants.</param>
    /// <returns>The trip, or null when no source has any.</returns>
    private static TripAct? Trip(CityState city, Building target, ItemId item, long wanted)
    {
        Building? source = city.Buildings
            .Where(building => building.Id != target.Id && building.Store.GetValueOrDefault(item) > 0 && IsSourceOf(building, item))
            .OrderByDescending(building => building.Store.GetValueOrDefault(item))
            .ThenBy(building => building.Id)
            .FirstOrDefault();
        if (source is null)
        {
            return null;
        }

        long stock = source.Store[item];
        string amount = Content.AmountPresets.OrderByDescending(preset => preset.Percent)
            .FirstOrDefault(preset => stock * preset.Percent / 100 <= wanted)?.Id.Value ?? "quarter";
        return new TripAct(source.Id, target.Id, item.Value, amount);
    }

    /// <summary>Whether goods of <paramref name="item"/> may be taken from <paramref name="building"/>: a warehouse, or a producer whose recipe makes it.</summary>
    /// <param name="building">The building.</param>
    /// <param name="item">The good.</param>
    /// <returns>True when it is a source.</returns>
    private static bool IsSourceOf(Building building, ItemId item)
    {
        BuildingDefinition definition = Definition(building);
        return definition.Kind == BuildingKind.Warehouse
            || (definition.Kind == BuildingKind.Producer && Recipe(building).Outputs.Any(output => output.Item == item));
    }

    /// <summary>How many of <paramref name="item"/> the workers' trips still bring to <paramref name="targetId"/>: their loads and their rests.</summary>
    /// <param name="city">The city.</param>
    /// <param name="targetId">The target building.</param>
    /// <param name="item">The good.</param>
    /// <returns>The quantity.</returns>
    private static long OnTheWay(CityState city, int targetId, ItemId item) =>
        city.Trips.Where(trip => trip.TargetId == targetId && trip.Item == item).Sum(trip => trip.Quantity + trip.Remaining);

    /// <summary>The city's producers in id order.</summary>
    /// <param name="city">The city.</param>
    /// <returns>The producers.</returns>
    private static IEnumerable<Building> Producers(CityState city) =>
        city.Buildings.Where(building => Definition(building).Kind == BuildingKind.Producer);

    /// <summary>A producer's recipe at its level.</summary>
    /// <param name="building">The producer.</param>
    /// <returns>The recipe.</returns>
    private static RecipeDefinition Recipe(Building building) => Definition(building).GetLevel(building.Level).Recipe!;

    /// <summary>A building's type in the content.</summary>
    /// <param name="building">The building.</param>
    /// <returns>The type.</returns>
    private static BuildingDefinition Definition(Building building)
    {
        Assert.True(Content.TryGetBuilding(building.TypeId, out BuildingDefinition? definition), building.TypeId.Value);
        return definition;
    }

    /// <summary>The coins of a cost.</summary>
    /// <param name="cost">The cost.</param>
    /// <returns>The coins, 0 when it has none.</returns>
    private static long Coins(IReadOnlyList<ItemQuantity> cost) => cost.Where(amount => amount.Item == Content.Currency).Sum(amount => amount.Quantity);
}
