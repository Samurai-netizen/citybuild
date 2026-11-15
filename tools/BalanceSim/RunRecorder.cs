using CityBuilder.BalanceSim.Output;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.BalanceSim;

/// <summary>Where a coin spent by an action counts in the summary (core-instructions/24-economy-design.md → Metrics we track).</summary>
public enum SpendCategory
{
    /// <summary>Placing a building.</summary>
    Placement,

    /// <summary>Upgrading a building.</summary>
    Upgrade,

    /// <summary>Starting a research node.</summary>
    Research,
}

/// <summary>
/// Everything one run measures: it advances the city (so it sees the exact completion times), adds up the settlement reports, measures stall
/// and idle-worker time per step, keeps the milestones and the timeline rows. All sums are integers in ordinal key order, so the same run
/// always records the same values.
/// </summary>
public sealed class RunRecorder
{
    /// <summary>The game content.</summary>
    private readonly GameContent _content;

    /// <summary>The timeline's column layout.</summary>
    private readonly Timeline _timeline;

    /// <summary>The coins spent per category.</summary>
    private readonly long[] _spent = new long[3];

    /// <summary>The rejected actions, by "action: error code".</summary>
    private readonly SortedDictionary<string, int> _rejections = new(StringComparer.Ordinal);

    /// <summary>The goods produced, per item id.</summary>
    private readonly SortedDictionary<string, long> _produced = new(StringComparer.Ordinal);

    /// <summary>The goods consumed by recipes, per item id.</summary>
    private readonly SortedDictionary<string, long> _consumed = new(StringComparer.Ordinal);

    /// <summary>The goods walkers handed to projects, per item id.</summary>
    private readonly SortedDictionary<string, long> _delivered = new(StringComparer.Ordinal);

    /// <summary>The goods walkers put into stores, per item id.</summary>
    private readonly SortedDictionary<string, long> _moved = new(StringComparer.Ordinal);

    /// <summary>The producer-seconds per building type and state (<c>Running</c>, <c>Upgrading</c> or a stall reason's API text).</summary>
    private readonly SortedDictionary<string, SortedDictionary<string, long>> _producerSeconds = new(StringComparer.Ordinal);

    /// <summary>The research starts in order, with their clock seconds.</summary>
    private readonly List<(ResearchId Research, long At)> _researchStarts = [];

    /// <summary>The research completions in order, with their clock seconds.</summary>
    private readonly List<(ResearchId Research, long At)> _researchDone = [];

    /// <summary>Per building type, the second its first building completed, and the second one first reached the type's max level.</summary>
    private readonly SortedDictionary<string, (long? FirstBuilt, long? FirstMax)> _buildingMilestones = new(StringComparer.Ordinal);

    /// <summary>The coins produced per day of the run (day 0 is the first 24 hours), at the step resolution.</summary>
    private readonly SortedDictionary<long, long> _coinsPerDay = [];

    /// <summary>The timeline rows written so far.</summary>
    private readonly List<string[]> _rows = [];

    /// <summary>The coins produced up to the last timeline row.</summary>
    private long _coinsAtLastRow;

    /// <summary>The clock second of the last timeline row.</summary>
    private long _lastRowAt;

    /// <summary>Creates the recorder of one run.</summary>
    /// <param name="content">The game content.</param>
    public RunRecorder(GameContent content)
    {
        _content = content;
        _timeline = new Timeline(content);
    }

    /// <summary>The coins produced so far (the Trading Post's output: the only coin faucet).</summary>
    public long CoinsProduced { get; private set; }

    /// <summary>The second the first coins arrived, at the run's step resolution; null before.</summary>
    public long? FirstCoinsAt { get; private set; }

    /// <summary>The courier seconds of every settlement (<see cref="SettlementReport.CourierSeconds"/>).</summary>
    public long CourierSeconds { get; private set; }

    /// <summary>How many times a courier ran out of stamina.</summary>
    public int TiredCouriers { get; private set; }

    /// <summary>The seconds measured for the shares below.</summary>
    public long MeasuredSeconds { get; private set; }

    /// <summary>The free workers summed over every measured second.</summary>
    public long IdleWorkerSeconds { get; private set; }

    /// <summary>The provided workers summed over every measured second.</summary>
    public long ProvidedWorkerSeconds { get; private set; }

    /// <summary>The measured seconds in which at least one worker was free.</summary>
    public long SecondsWithIdleWorkers { get; private set; }

    /// <summary>The timeline's header.</summary>
    public IReadOnlyList<string> TimelineHeader => _timeline.Header;

    /// <summary>The timeline rows.</summary>
    public IReadOnlyList<string[]> TimelineRows => _rows;

    /// <summary>The coins spent in one category.</summary>
    /// <param name="category">The category.</param>
    /// <returns>The coins.</returns>
    public long SpentOn(SpendCategory category) => _spent[(int)category];

    /// <summary>The rejected actions, by "action: error code", in ordinal order.</summary>
    public IReadOnlyDictionary<string, int> Rejections => _rejections;

    /// <summary>The goods produced, per item id.</summary>
    public IReadOnlyDictionary<string, long> Produced => _produced;

    /// <summary>The coins produced per day of the run (day 0 is the first 24 hours).</summary>
    public IReadOnlyDictionary<long, long> CoinsPerDay => _coinsPerDay;

    /// <summary>The goods consumed by recipes, per item id.</summary>
    public IReadOnlyDictionary<string, long> Consumed => _consumed;

    /// <summary>The goods handed to projects, per item id.</summary>
    public IReadOnlyDictionary<string, long> Delivered => _delivered;

    /// <summary>The goods put into stores by walkers, per item id.</summary>
    public IReadOnlyDictionary<string, long> Moved => _moved;

    /// <summary>The producer-seconds per building type and state.</summary>
    public IReadOnlyDictionary<string, SortedDictionary<string, long>> ProducerSeconds => _producerSeconds;

    /// <summary>The research starts in order.</summary>
    public IReadOnlyList<(ResearchId Research, long At)> ResearchStarts => _researchStarts;

    /// <summary>The research completions in order.</summary>
    public IReadOnlyList<(ResearchId Research, long At)> ResearchDone => _researchDone;

    /// <summary>The building milestones per type id.</summary>
    public IReadOnlyDictionary<string, (long? FirstBuilt, long? FirstMax)> BuildingMilestones => _buildingMilestones;

    /// <summary>Notes coins an action spent.</summary>
    /// <param name="category">Where they count.</param>
    /// <param name="coins">How many.</param>
    public void Spent(SpendCategory category, long coins) => _spent[(int)category] = checked(_spent[(int)category] + coins);

    /// <summary>Notes an action the engine rejected.</summary>
    /// <param name="action">The rule's name.</param>
    /// <param name="code">The error code.</param>
    public void Rejected(string action, MutationErrorCode code)
    {
        string key = action + ": " + code;
        _rejections[key] = _rejections.GetValueOrDefault(key) + 1;
    }

    /// <summary>Notes a research start.</summary>
    /// <param name="research">The node.</param>
    /// <param name="at">The clock second.</param>
    public void ResearchStarted(ResearchId research, long at) => _researchStarts.Add((research, at));

    /// <summary>
    /// Advances the city and records what happened: the report's sums, the first coins, and each completed research and building at the second
    /// its timer ended (known before the advance; a project whose goods arrived inside the window counts at the window's end).
    /// </summary>
    /// <param name="city">The city.</param>
    /// <param name="elapsedSeconds">The seconds to advance, at least 0.</param>
    /// <param name="absentFrom">The second the player is absent from, or null when present (04 §2).</param>
    public void Advance(CityState city, long elapsedSeconds, long? absentFrom = null)
    {
        ResearchId? running = city.ActiveResearch?.Id;
        long? researchEnds = city.ActiveResearch?.EndsAt;
        var buildingEnds = new Dictionary<int, long>();
        foreach (Building building in city.Buildings)
        {
            if (building.PhaseEndsAt is long endsAt && building.Phase is BuildingPhase.Constructing or BuildingPhase.Upgrading)
            {
                buildingEnds[building.Id] = endsAt;
            }
        }

        SettlementReport report = Simulator.Advance(city, _content, elapsedSeconds, absentFrom);
        Settled(report, city.Clock);
        foreach (ResearchId done in report.CompletedResearch)
        {
            _researchDone.Add((done, done == running && researchEnds.HasValue ? researchEnds.Value : city.Clock));
        }

        foreach (int id in report.CompletedBuildings)
        {
            if (city.TryGetBuilding(id, out Building? building) && _content.TryGetBuilding(building.TypeId, out BuildingDefinition? definition))
            {
                long at = buildingEnds.TryGetValue(id, out long endsAt) ? endsAt : city.Clock;
                (long? firstBuilt, long? firstMax) = _buildingMilestones.GetValueOrDefault(building.TypeId.Value);
                firstBuilt ??= at;
                if (building.Level >= definition.MaxLevel)
                {
                    firstMax ??= at;
                }

                _buildingMilestones[building.TypeId.Value] = (firstBuilt, firstMax);
            }
        }
    }

    /// <summary>
    /// Measures the city as it stands for the next <paramref name="seconds"/>: each producer's state (running, upgrading or its stall reason)
    /// and the free workers. The run calls it at the start of every step, so the shares are exact at the step's resolution.
    /// </summary>
    /// <param name="city">The city.</param>
    /// <param name="seconds">The length of the step.</param>
    public void Measure(CityState city, long seconds)
    {
        MeasuredSeconds += seconds;
        long free = CityQueries.FreeWorkers(city, _content);
        IdleWorkerSeconds = checked(IdleWorkerSeconds + (free * seconds));
        ProvidedWorkerSeconds = checked(ProvidedWorkerSeconds + (CityQueries.WorkersProvided(city, _content) * seconds));
        if (free > 0)
        {
            SecondsWithIdleWorkers += seconds;
        }

        foreach (Building building in city.Buildings)
        {
            if (!_content.TryGetBuilding(building.TypeId, out BuildingDefinition? definition)
                || definition.Kind != BuildingKind.Producer || building.Phase == BuildingPhase.Constructing)
            {
                continue;
            }

            string state = building.Phase == BuildingPhase.Upgrading ? "Upgrading"
                : StallReason.Of(city, _content, building)?.ToString() ?? "Running";
            if (!_producerSeconds.TryGetValue(building.TypeId.Value, out SortedDictionary<string, long>? states))
            {
                states = new SortedDictionary<string, long>(StringComparer.Ordinal);
                _producerSeconds[building.TypeId.Value] = states;
            }

            states[state] = states.GetValueOrDefault(state) + seconds;
        }
    }

    /// <summary>Writes a timeline row for the city as it stands.</summary>
    /// <param name="city">The city.</param>
    /// <param name="present">Whether the player is checked in.</param>
    public void Sample(CityState city, bool present)
    {
        long earned = CoinsProduced - _coinsAtLastRow;
        long seconds = city.Clock - _lastRowAt;
        _rows.Add(_timeline.Row(city, present, earned, seconds));
        _coinsAtLastRow = CoinsProduced;
        _lastRowAt = city.Clock;
    }

    /// <summary>Adds a settlement report's sums.</summary>
    /// <param name="report">The report.</param>
    /// <param name="clock">The city clock after the advance.</param>
    public void Settled(SettlementReport report, long clock)
    {
        foreach (KeyValuePair<ItemId, long> pair in report.Produced)
        {
            Add(_produced, pair.Key, pair.Value);
            if (pair.Key == _content.Currency && pair.Value > 0)
            {
                CoinsProduced = checked(CoinsProduced + pair.Value);
                FirstCoinsAt ??= clock;
                long day = Math.Max(0, clock - 1) / 86_400;
                _coinsPerDay[day] = _coinsPerDay.GetValueOrDefault(day) + pair.Value;
            }
        }

        foreach (KeyValuePair<ItemId, long> pair in report.Consumed)
        {
            Add(_consumed, pair.Key, pair.Value);
        }

        foreach (KeyValuePair<ItemId, long> pair in report.Delivered)
        {
            Add(_delivered, pair.Key, pair.Value);
        }

        foreach (KeyValuePair<ItemId, long> pair in report.Moved)
        {
            Add(_moved, pair.Key, pair.Value);
        }

        CourierSeconds = checked(CourierSeconds + report.CourierSeconds);
        TiredCouriers += report.TiredCouriers.Count;
    }

    /// <summary>Adds a quantity to a per-item sum.</summary>
    /// <param name="sums">The sums.</param>
    /// <param name="item">The item.</param>
    /// <param name="quantity">The quantity.</param>
    private static void Add(SortedDictionary<string, long> sums, ItemId item, long quantity) =>
        sums[item.Value] = checked(sums.GetValueOrDefault(item.Value) + quantity);
}
