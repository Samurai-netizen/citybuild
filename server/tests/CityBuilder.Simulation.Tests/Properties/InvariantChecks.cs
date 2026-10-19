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
    private readonly GameContent _content;
    private readonly Action<Invariant, string> _fail;
    private readonly Action<string> _count;

    public InvariantChecks(GameContent content, Action<Invariant, string> fail, Action<string> count)
    {
        _content = content;
        _fail = fail;
        _count = count;
    }

    /// <summary>Checks for a branch that leaves the history here: the same callbacks, and a copy of what these checks track so far.</summary>
    public InvariantChecks Fork() => new(_content, _fail, _count);

    public void Check(InvariantStep step)
    {
        _count("steps");
        Report(Invariant.Conservation, step, Conservation(step));
        Report(Invariant.NoNegativeStock, step, NoNegativeStock(step.After));
        Report(Invariant.StorageCap, step, StorageCap(step));
        Report(Invariant.ClockNeverDecreases, step, Clock(step));
        Report(Invariant.NoProductionWhileBuilding, step, Production(step) ?? Building(step));
        Report(Invariant.WorkerLimits, step, Workers(step.After));
        Report(Invariant.BuilderAndResearchSlots, step, Slots(step));
    }

    private void Report(Invariant invariant, InvariantStep step, string? failure)
    {
        if (failure is not null)
        {
            _fail(invariant, $"{step.Describe()}: {failure}");
        }
    }

    // 4: final = initial + produced − consumed − spent + refunded, per item, over every item any of them names.
    private string? Conservation(InvariantStep step)
    {
        Dictionary<ItemId, long> spent = Quantities(step.Mutation?.Spent ?? []);
        Dictionary<ItemId, long> refunded = Quantities(step.Mutation?.Refunded ?? []);
        CountIf(spent.Count > 0, "steps that spent");
        CountIf(refunded.Count > 0, "steps that refunded");
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

    // 5: no item below zero.
    private static string? NoNegativeStock(CityState city)
    {
        foreach (KeyValuePair<ItemId, long> stock in city.Inventory.Where(stock => stock.Value < 0))
        {
            return $"{stock.Key} is {stock.Value}";
        }

        return null;
    }

    // 6: production never takes stock + reserved of a capped item above its cap. Only a refund may: so after the step it is at most the cap,
    // or what it was before plus the refund, whichever is more (a start needs room, a completion moves reserved to stock, inputs only lower it).
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

    // 7: the clock moves by exactly the requested seconds (0 for a command or a negative request), and the report says the same.
    private string? Clock(InvariantStep step)
    {
        long expected = Math.Max(step.Requested, 0);
        CountIf(step.Requested < 0, "steps asked to go back in time");
        return step.After.Clock == step.Before.Clock + expected && step.ReportedElapsed == expected
            ? null
            : $"asked for {step.Requested} s: the clock moved {step.After.Clock - step.Before.Clock} s, the report says {step.ReportedElapsed} s";
    }

    // 8, first half: the outputs of a step are exactly those of the cycles that were running before it and end by its end. So nothing a
    // constructing or upgrading building did shows up as production, and an upgrade's interrupted cycle delivers nothing.
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

    // 8, second half: a construction or upgrade not due yet goes on unchanged; a cycle starts only after the construction or upgrade before
    // it ended (an upgrade at the new level); constructing and upgrading buildings hold no cycle.
    private string? Building(InvariantStep step)
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

    // 9: the houses provide at least the workers assigned, every building stays within its level's limit, and a building under construction
    // has none.
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

    // 10: busy builders within the slots; a research starts only when none runs, and the running one is not completed.
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

    // The storage cap of an item now: its base cap plus the bonuses of the completed research.
    private long Cap(CityState city, ItemId item) =>
        _content.Rules.BaseStorageCaps.Single(cap => cap.Item == item).Quantity
        + city.CompletedResearch.SelectMany(id => Research(id).Effects).OfType<StorageCapBonusEffect>()
            .Where(bonus => bonus.Item == item).Sum(bonus => bonus.Amount);

    // What the running cycles (frozen ones too) will deliver of an item.
    private long Reserved(CityState city, ItemId item) => city.Buildings
        .Where(building => building.Phase == BuildingPhase.Producing)
        .SelectMany(building => Level(building).Recipe!.Outputs)
        .Where(output => output.Item == item)
        .Sum(output => output.Quantity);

    private BuildingLevelDefinition Level(Building building) => DormantTail.Definition(_content, building.TypeId).GetLevel(building.Level);

    private ResearchDefinition Research(ResearchId id) =>
        _content.TryGetResearch(id, out ResearchDefinition? node) ? node : throw new InvalidOperationException($"Unknown research {id}.");

    private void CountIf(bool happened, string what)
    {
        if (happened)
        {
            _count(what);
        }
    }

    private static Dictionary<ItemId, long> Quantities(IEnumerable<ItemQuantity> lines) =>
        lines.GroupBy(line => line.Item).ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));
}
