using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// The invariants of <c>04</c> section 10, checked on every observed step of one history (<see cref="InvariantObserver"/>). Each check reads
/// only public state, content and what the engine reported, and computes what it needs (reservations, caps, workers) itself, so a wrong
/// engine query can't hide a wrong state. A failure goes to the callback with the invariant; counts of the cases each check really
/// exercised go to the other one, so the coverage test can tell a check that never fires from one that holds.
/// </summary>
internal sealed class InvariantChecks
{
    /// <summary>The game content the history runs on.</summary>
    private readonly GameContent _content;
    /// <summary>Receives each failure: the invariant, and where and how it failed.</summary>
    private readonly Action<Invariant, string> _fail;
    /// <summary>Receives the name of each case a check exercised, for the coverage test.</summary>
    private readonly Action<string> _count;
    /// <summary>The worker-seconds of every running cycle so far, for invariant 11.</summary>
    private readonly LaborLedger _labor;

    /// <summary>Checks for a new history on <paramref name="content"/>: failures go to <paramref name="fail"/>, exercised cases to <paramref name="count"/>.</summary>
    public InvariantChecks(GameContent content, Action<Invariant, string> fail, Action<string> count)
        : this(content, fail, count, new LaborLedger(content))
    {
    }

    /// <summary>Checks that go on with an existing labor ledger (<see cref="Fork"/>).</summary>
    private InvariantChecks(GameContent content, Action<Invariant, string> fail, Action<string> count, LaborLedger labor)
    {
        _content = content;
        _fail = fail;
        _count = count;
        _labor = labor;
    }

    /// <summary>Checks for a branch that leaves the history here: the same callbacks, and a copy of what these checks track so far.</summary>
    public InvariantChecks Fork() => new(_content, _fail, _count, _labor.Fork());

    /// <summary>
    /// Checks every invariant on one step and reports each that fails. Each check returns a description of its first failure, or null when
    /// its invariant holds.
    /// </summary>
    public void Check(InvariantStep step)
    {
        _count("steps");
        Report(Invariant.Conservation, step, Conservation(step));
        Report(Invariant.NoNegativeStock, step, NoNegativeStock(step.After));
        Report(Invariant.StorageCap, step, StorageCap(step));
        Report(Invariant.ClockNeverDecreases, step, Clock(step));
        Report(Invariant.NoProductionWhileBuilding, step, Production(step) ?? BusyBuildings(step));
        Report(Invariant.WorkerLimits, step, Workers(step.After));
        Report(Invariant.BuilderAndResearchSlots, step, Slots(step));
        Report(Invariant.LaborAccounting, step, _labor.Check(step, _count));
        Report(Invariant.NoStartAfterTheCap, step, NoStartAfterTheCap(step));
        Report(Invariant.UnlockAndPrerequisites, step, Unlocks(step));
    }

    /// <summary>Hands a failure, if there is one, to the callback, prefixed with where the step is.</summary>
    private void Report(Invariant invariant, InvariantStep step, string? failure)
    {
        if (failure is not null)
        {
            _fail(invariant, $"{step.Describe()}: {failure}");
        }
    }

    /// <summary>Invariant 4: final = initial + produced − consumed − spent + refunded, per item, over every item any of them names.</summary>
    private string? Conservation(InvariantStep step)
    {
        Dictionary<ItemId, long> spent = Quantities(step.Mutation?.Spent ?? []);
        Dictionary<ItemId, long> refunded = Quantities(step.Mutation?.Refunded ?? []);
        CountIf(spent.Values.Any(quantity => quantity > 0), "steps that spent");
        CountIf(refunded.Values.Any(quantity => quantity > 0), "steps that refunded");
        IEnumerable<ItemId> items = step.Before.Inventory.Keys.Concat(step.After.Inventory.Keys).Concat(step.Produced.Keys)
            .Concat(step.Consumed.Keys).Concat(spent.Keys).Concat(refunded.Keys).Distinct().Order();
        foreach (ItemId item in items)
        {
            long expected = step.Before.GetQuantity(item) + step.Produced.GetValueOrDefault(item) - step.Consumed.GetValueOrDefault(item)
                - spent.GetValueOrDefault(item) + refunded.GetValueOrDefault(item);
            if (step.After.GetQuantity(item) != expected)
            {
                return $"{item}: {step.Before.GetQuantity(item)} + produced {step.Produced.GetValueOrDefault(item)} − consumed "
                    + $"{step.Consumed.GetValueOrDefault(item)} − spent {spent.GetValueOrDefault(item)} + refunded "
                    + $"{refunded.GetValueOrDefault(item)} = {expected}, but the stock is {step.After.GetQuantity(item)}";
            }
        }

        return null;
    }

    /// <summary>Invariant 5: no item below zero.</summary>
    private static string? NoNegativeStock(CityState city)
    {
        foreach (KeyValuePair<ItemId, long> stock in city.Inventory.Where(stock => stock.Value < 0))
        {
            return $"{stock.Key} is {stock.Value}";
        }

        return null;
    }

    /// <summary>
    /// Invariant 6: production never takes stock + reserved of a capped item above its cap. Only a refund may: so after the step it is at most
    /// the cap, or what it was before plus the refund, whichever is more (a start needs room, a completion moves reserved to stock, inputs only
    /// lower it).
    /// </summary>
    private string? StorageCap(InvariantStep step)
    {
        foreach (ItemQuantity baseCap in _content.Rules.BaseStorageCaps)
        {
            ItemId item = baseCap.Item;
            long cap = Cap(step.After, item);
            long before = step.Before.GetQuantity(item) + Reserved(step.Before, item);
            long after = step.After.GetQuantity(item) + Reserved(step.After, item);
            long refunded = step.Mutation?.Refunded.Where(line => line.Item == item).Sum(line => line.Quantity) ?? 0;
            CountIf(after == cap, "steps ending with an item exactly at its cap");
            CountIf(after > cap, "steps ending with an item above its cap");
            if (after > Math.Max(cap, before + refunded))
            {
                return $"{item}: stock + reserved went from {before} to {after} (refund {refunded}), the cap is {cap}";
            }
        }

        return null;
    }

    /// <summary>Invariant 7: the clock moves by exactly the requested seconds (0 for a command or a negative request), and the report says the same.</summary>
    private string? Clock(InvariantStep step)
    {
        long expected = Math.Max(step.Requested, 0);
        CountIf(step.Requested < 0, "steps asked to go back in time");
        return step.After.Clock == step.Before.Clock + expected && step.ReportedElapsed == expected
            ? null
            : $"asked for {step.Requested} s: the clock moved {step.After.Clock - step.Before.Clock} s, the report says {step.ReportedElapsed} s";
    }

    /// <summary>
    /// Invariant 8, first half: the outputs of a step are exactly those of the cycles that were running before it and end by its end. So
    /// nothing a constructing or upgrading building did shows up as production, and an upgrade's interrupted cycle delivers nothing.
    /// </summary>
    private string? Production(InvariantStep step)
    {
        var expected = new Dictionary<ItemId, long>();
        foreach (Building building in step.Before.Buildings.Where(building => building.Phase == BuildingPhase.Producing))
        {
            if (building.PhaseEndsAt <= step.After.Clock)
            {
                _count("cycles completed");
                foreach (ItemQuantity output in Level(building).Recipe!.Outputs)
                {
                    expected[output.Item] = expected.GetValueOrDefault(output.Item) + output.Quantity;
                }
            }
        }

        foreach (ItemId item in expected.Keys.Concat(step.Produced.Keys).Distinct().Order())
        {
            if (expected.GetValueOrDefault(item) != step.Produced.GetValueOrDefault(item))
            {
                return $"{item}: the cycles that ended delivered {expected.GetValueOrDefault(item)}, the report says {step.Produced.GetValueOrDefault(item)}";
            }
        }

        return null;
    }

    /// <summary>
    /// Invariant 8, second half: a construction or upgrade not due yet goes on unchanged; a cycle starts only after the construction or
    /// upgrade before it ended (an upgrade at the new level); constructing and upgrading buildings hold no cycle.
    /// </summary>
    private string? BusyBuildings(InvariantStep step)
    {
        foreach (Building after in step.After.Buildings)
        {
            bool busy = after.Phase is BuildingPhase.Constructing or BuildingPhase.Upgrading;
            if (busy && (after.CycleLabor != 0 || after.LaborRemaining != 0 || after.SegmentStartedAt != 0))
            {
                return $"building {after.Id} is {after.Phase} with cycle fields";
            }

            if (!step.Before.TryGetBuilding(after.Id, out Building? before) || before.Phase is not (BuildingPhase.Constructing or BuildingPhase.Upgrading))
            {
                continue;
            }

            if (before.PhaseEndsAt > step.After.Clock
                && (after.Phase, after.Level, after.PhaseStartedAt, after.PhaseEndsAt, after.TargetLevel)
                    != (before.Phase, before.Level, before.PhaseStartedAt, before.PhaseEndsAt, before.TargetLevel))
            {
                return $"building {after.Id} changed from {before.Phase} until {before.PhaseEndsAt} to {after.Phase} before its end";
            }

            if (after.Phase == BuildingPhase.Producing)
            {
                _count("cycles started when a construction or upgrade completed");
                int level = before.Phase == BuildingPhase.Upgrading ? before.TargetLevel : before.Level;
                if (after.PhaseStartedAt < before.PhaseEndsAt || after.Level != level)
                {
                    return $"building {after.Id} started a level {after.Level} cycle at {after.PhaseStartedAt}, its {before.Phase} ended at {before.PhaseEndsAt}";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Invariant 9: the houses provide at least the workers assigned, every building stays within its level's limit, and a building under
    /// construction has none.
    /// </summary>
    private string? Workers(CityState city)
    {
        long provided = DormantTail.Provided(_content, city.Buildings);
        long assigned = city.Buildings.Sum(building => (long)building.AssignedWorkers);
        CountIf(assigned == provided && provided > 0, "steps with every worker assigned");
        if (assigned > provided)
        {
            return $"{assigned} workers assigned, the houses provide {provided}";
        }

        foreach (Building building in city.Buildings)
        {
            int limit = building.Phase == BuildingPhase.Constructing ? 0 : Level(building).MaxWorkers;
            CountIf(building.AssignedWorkers == limit && limit > 0, "buildings at their worker limit");
            if (building.AssignedWorkers > limit)
            {
                return $"building {building.Id} ({building.Phase}, level {building.Level}) has {building.AssignedWorkers} workers, the limit is {limit}";
            }
        }

        return null;
    }

    /// <summary>Invariant 10: busy builders within the slots; a research starts only when none runs, and the running one is not completed.</summary>
    private string? Slots(InvariantStep step)
    {
        int busy = step.After.Buildings.Count(building => building.Phase is BuildingPhase.Constructing or BuildingPhase.Upgrading);
        CountIf(busy == _content.Rules.BuilderSlots, "steps with every builder busy");
        if (busy > _content.Rules.BuilderSlots)
        {
            return $"{busy} builders busy, the city has {_content.Rules.BuilderSlots}";
        }

        if (step.Operation is CityOperation.StartResearch && step.Mutation is not null)
        {
            _count("research starts");
            if (step.Before.ActiveResearch is not null)
            {
                return $"research started while {step.Before.ActiveResearch.Id} was running";
            }
        }

        ActiveResearch? active = step.After.ActiveResearch;
        return active is not null && step.After.HasCompletedResearch(active.Id) ? $"research {active.Id} is running and completed" : null;
    }

    /// <summary>
    /// Invariant 12: every cycle that starts in a step starts within its window; and the dormant tail of an advance beyond the cap does
    /// exactly what the model says: timers complete, nothing starts, nothing is consumed.
    /// </summary>
    private string? NoStartAfterTheCap(InvariantStep step)
    {
        foreach (Building after in step.After.Buildings.Where(building => building.Phase == BuildingPhase.Producing))
        {
            bool running = step.Before.TryGetBuilding(after.Id, out Building? before)
                && before.Phase == BuildingPhase.Producing && before.PhaseStartedAt == after.PhaseStartedAt;
            CountIf(!running && after.PhaseStartedAt == step.ProductionUntil, "cycles started at the last second of a window");
            if (!running && after.PhaseStartedAt > step.ProductionUntil)
            {
                return $"building {after.Id} started a cycle at {after.PhaseStartedAt}, after the window's last second {step.ProductionUntil}";
            }
        }

        if (step.ExpectedTail is not { } tail)
        {
            return null;
        }

        _count("dormant tails");
        CountIf(tail.CompletedBuildings.Count > 0 || tail.CompletedResearch.Count > 0 || tail.Produced.Count > 0, "dormant tails with a completion");
        string? consumed = step.Consumed.Where(item => item.Value != 0).Select(item => $"{item.Key} {item.Value}").FirstOrDefault();
        return StructuralEquality.FirstDifference(tail.City, step.After)
            ?? (consumed is not null ? $"the tail consumed {consumed}" : null)
            ?? Differ("produced", Text(tail.Produced), Text(step.Produced))
            ?? Differ("completed buildings", string.Join(",", tail.CompletedBuildings), string.Join(",", step.CompletedBuildings.Order()))
            ?? Differ("completed research", string.Join(",", tail.CompletedResearch), string.Join(",", step.CompletedResearch));
    }

    /// <summary>
    /// Invariant 13: a placed type was unlocked and of the city's era or earlier when it was placed; a research started had its
    /// prerequisites and the city's era; and at every step the completed research has all its prerequisites, and every building's type is
    /// unlocked.
    /// </summary>
    private string? Unlocks(InvariantStep step)
    {
        if (step.Mutation is not null && step.Operation is CityOperation.PlaceBuilding
            && step.After.TryGetBuilding(step.Before.NextBuildingId, out Building? placed))
        {
            BuildingDefinition type = DormantTail.Definition(_content, placed.TypeId);
            CountIf(!type.IsAvailableAtStart, "placements of a type locked at the start");
            if (!IsUnlocked(step.Before, type))
            {
                return $"building {placed.Id} of type {type.Id} (era {type.Era}) was placed while locked";
            }
        }

        if (step.Mutation is not null && step.Operation is CityOperation.StartResearch start)
        {
            ResearchDefinition node = Research(new ResearchId(start.ResearchId));
            CountIf(node.Prerequisites.Count > 0, "research starts with prerequisites");
            if (node.Era != step.Before.Era || node.Prerequisites.Any(prerequisite => !step.Before.HasCompletedResearch(prerequisite)))
            {
                return $"research {node.Id} (era {node.Era}) started in era {step.Before.Era} with completed [{string.Join(",", step.Before.CompletedResearch)}]";
            }
        }

        IEnumerable<ResearchId> researched = step.After.ActiveResearch is null
            ? step.After.CompletedResearch
            : step.After.CompletedResearch.Append(step.After.ActiveResearch.Id);
        foreach (ResearchId id in researched)
        {
            ResearchId[] missing = [.. Research(id).Prerequisites.Where(prerequisite => !step.After.HasCompletedResearch(prerequisite))];
            if (missing.Length > 0)
            {
                return $"research {id} is completed or running without {string.Join(",", missing)}";
            }
        }

        Building? locked = step.After.Buildings.FirstOrDefault(building => !IsUnlocked(step.After, DormantTail.Definition(_content, building.TypeId)));
        return locked is null ? null : $"building {locked.Id} of the locked type {locked.TypeId} stands in the city";
    }

    /// <summary>
    /// Whether the city may have a building of the type: of its era or earlier, and available at the start or unlocked by completed research.
    /// Worked out here, not with <see cref="CityQueries.IsUnlocked"/>, so a wrong engine query can't hide a wrong state.
    /// </summary>
    private static bool IsUnlocked(CityState city, BuildingDefinition type) =>
        type.Era <= city.Era && (type.IsAvailableAtStart || (type.UnlockedByResearch is { } research && city.HasCompletedResearch(research)));

    /// <summary>A failure when the expected and the actual text differ; null when they are the same.</summary>
    private static string? Differ(string what, string expected, string actual) =>
        expected == actual ? null : $"{what}: expected [{expected}], actual [{actual}]";

    /// <summary>The non-zero quantities as text in item order, for <see cref="Differ"/>.</summary>
    private static string Text(IReadOnlyDictionary<ItemId, long> quantities) =>
        string.Join(",", quantities.Where(item => item.Value != 0).OrderBy(item => item.Key).Select(item => $"{item.Key} {item.Value}"));

    /// <summary>The storage cap of an item now: its base cap plus the bonuses of the completed research.</summary>
    private long Cap(CityState city, ItemId item) =>
        _content.Rules.BaseStorageCaps.Single(cap => cap.Item == item).Quantity
        + city.CompletedResearch.SelectMany(id => Research(id).Effects).OfType<StorageCapBonusEffect>()
            .Where(bonus => bonus.Item == item).Sum(bonus => bonus.Amount);

    /// <summary>What the running cycles (frozen ones too) will deliver of an item.</summary>
    private long Reserved(CityState city, ItemId item) => city.Buildings
        .Where(building => building.Phase == BuildingPhase.Producing)
        .SelectMany(building => Level(building).Recipe!.Outputs)
        .Where(output => output.Item == item)
        .Sum(output => output.Quantity);

    /// <summary>The definition of a building's current level.</summary>
    private BuildingLevelDefinition Level(Building building) => DormantTail.Definition(_content, building.TypeId).GetLevel(building.Level);

    /// <summary>The definition of a research node; throws for one the content doesn't know.</summary>
    private ResearchDefinition Research(ResearchId id) =>
        _content.TryGetResearch(id, out ResearchDefinition? node) ? node : throw new InvalidOperationException($"Unknown research {id}.");

    /// <summary>Counts the case when it happened.</summary>
    private void CountIf(bool happened, string what)
    {
        if (happened)
        {
            _count(what);
        }
    }

    /// <summary>The total per item of a list of lines (an item may appear in several).</summary>
    private static Dictionary<ItemId, long> Quantities(IEnumerable<ItemQuantity> lines) =>
        lines.GroupBy(line => line.Item).ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));
}
