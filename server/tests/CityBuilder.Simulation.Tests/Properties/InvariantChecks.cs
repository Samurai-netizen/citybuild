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
        Report(Invariant.UnlockAndPrerequisites, step, Unlocks(step));
        Report(Invariant.StaminaAndLoads, step, Stamina(step) ?? Loads(step));
        Report(Invariant.DeliveryLines, step, Lines(step.After));
    }

    /// <summary>
    /// Invariant 15 (CR-0008 D16–D19): every delivery line names a courier of the city, which has no route, two different buildings and a
    /// quantity of 1 to its station level's carry limit; no courier has more lines than the content allows.
    /// </summary>
    private string? Lines(CityState city)
    {
        CountIf(city.DeliveryLines.Count > 0, "steps with delivery lines");
        foreach (DeliveryLine line in city.DeliveryLines)
        {
            Courier? courier = city.Couriers.FirstOrDefault(other => other.StationId == line.StationId && other.Slot == line.Slot);
            if (courier is null || city.TryGetRoute(line.StationId, line.Slot, out _))
            {
                return $"line {line.Id} belongs to courier {line.StationId}/{line.Slot}, which is gone or has a route";
            }

            city.TryGetBuilding(line.StationId, out Building? station);
            if (line.SourceId == line.TargetId || line.Quantity < 1 || line.Quantity > Level(station!).CarryLimit)
            {
                return $"line {line.Id} moves {line.Quantity} from {line.SourceId} to {line.TargetId}, the station's carry limit is {Level(station!).CarryLimit}";
            }
        }

        foreach (Courier courier in city.Couriers)
        {
            int lines = city.DeliveryLines.Count(line => line.StationId == courier.StationId && line.Slot == courier.Slot);
            CountIf(lines > 1, "couriers with several lines");
            if (lines > _content.Rules.MaxDeliveryLines)
            {
                return $"courier {courier.StationId}/{courier.Slot} has {lines} lines, the most is {_content.Rules.MaxDeliveryLines}";
            }
        }

        return null;
    }

    /// <summary>Hands a failure, if there is one, to the callback, prefixed with where the step is.</summary>
    private void Report(Invariant invariant, InvariantStep step, string? failure)
    {
        if (failure is not null)
        {
            _fail(invariant, $"{step.Describe()}: {failure}");
        }
    }

    /// <summary>
    /// Invariant 4: per item, final total = initial total + produced − consumed − delivered + refunded − spent, where a total is the purse for
    /// the currency and for a good every building's store, every trip's load and every courier's load (<c>04</c> section 16). Refunds come from an upgrade command
    /// or from an upgrade a delivery started; moving goods between stores and loads changes no total.
    /// </summary>
    private string? Conservation(InvariantStep step)
    {
        Dictionary<ItemId, long> spent = Quantities(step.Mutation?.Spent ?? []);
        Dictionary<ItemId, long> refunded = Quantities(step.Mutation?.Refunded ?? []);
        foreach (KeyValuePair<ItemId, long> line in step.Refunded)
        {
            refunded[line.Key] = refunded.GetValueOrDefault(line.Key) + line.Value;
        }

        CountIf(spent.Values.Any(quantity => quantity > 0), "steps that spent");
        CountIf(refunded.Values.Any(quantity => quantity > 0), "steps that refunded");
        CountIf(step.Refunded.Values.Any(quantity => quantity > 0), "steps that refunded by a delivery");
        CountIf(step.Delivered.Values.Any(quantity => quantity > 0), "steps that delivered to a project");
        CountIf(step.Moved.Values.Any(quantity => quantity > 0), "steps that moved goods into a store");
        foreach (ItemDefinition definition in _content.Items)
        {
            ItemId item = definition.Id;
            long before = CityQueries.Total(step.Before, _content, item);
            long after = CityQueries.Total(step.After, _content, item);
            long expected = before + step.Produced.GetValueOrDefault(item) - step.Consumed.GetValueOrDefault(item)
                - step.Delivered.GetValueOrDefault(item) - spent.GetValueOrDefault(item) + refunded.GetValueOrDefault(item);
            if (after != expected)
            {
                return $"{item}: {before} + produced {step.Produced.GetValueOrDefault(item)} − consumed "
                    + $"{step.Consumed.GetValueOrDefault(item)} − delivered {step.Delivered.GetValueOrDefault(item)} − spent "
                    + $"{spent.GetValueOrDefault(item)} + refunded {refunded.GetValueOrDefault(item)} = {expected}, but the city total is {after}";
            }
        }

        return null;
    }

    /// <summary>Invariant 5: no quantity below zero in the purse, a store, a project's needs, or a load (a trip carries at least 1).</summary>
    private static string? NoNegativeStock(CityState city)
    {
        if (city.Purse < 0)
        {
            return $"the purse is {city.Purse}";
        }

        foreach (Building building in city.Buildings)
        {
            foreach (KeyValuePair<ItemId, long> amount in building.Store.Concat(building.Needs).Where(amount => amount.Value < 0))
            {
                return $"building {building.Id}: {amount.Key} is {amount.Value}";
            }
        }

        // A worker carries a load of at least 1 to the target and nothing on the way back for the next one (04 section 18).
        Trip? wrong = city.Trips.FirstOrDefault(trip => trip.Remaining < 0 || (trip.Leg == TripLeg.ToTarget ? trip.Quantity < 1 : trip.Quantity != 0));
        return wrong is null ? null : $"trip {wrong.Id} carries {wrong.Quantity} on its way {wrong.Leg} with {wrong.Remaining} to fetch";
    }

    /// <summary>
    /// Invariant 6: production and walkers never push a store's <c>stock + reserved</c> above its effective limit (<c>04</c> section 16);
    /// only a refund into that building's store (an upgrade command, or an upgrade a delivery started in the step) and a returned load (a trip
    /// from it that arrived in the step, counted whole since part of it may have been delivered; or what couriers carried from it less what
    /// they still carry from it, when a courier stopped working) may, on top of the limit or of what the store held before, and a store
    /// already above its limit may only fall otherwise.
    /// </summary>
    private string? StorageCap(InvariantStep step)
    {
        foreach (Building after in step.After.Buildings)
        {
            step.Before.TryGetBuilding(after.Id, out Building? before);
            foreach (ItemQuantity good in Level(after).Storage)
            {
                ItemId item = good.Item;
                CityQueries.TryGetEffectiveLimit(step.After, _content, after, item, out long limit);
                long afterHeld = after.GetStock(item) + CityQueries.Reserved(_content, after, item);
                long beforeHeld = before is null ? 0 : before.GetStock(item) + CityQueries.Reserved(_content, before, item);
                long refunded = step.Operation is CityOperation.UpgradeBuilding upgrade && upgrade.BuildingId == after.Id
                    ? step.Mutation?.Refunded.Where(line => line.Item == item).Sum(line => line.Quantity) ?? 0
                    : 0;
                if (step.Mutation is null && before?.Phase == BuildingPhase.Producing && after.Phase == BuildingPhase.Upgrading)
                {
                    refunded += Level(before).Recipe!.Inputs.Where(line => line.Item == item).Sum(line => line.Quantity);
                }

                // A load whose leg to the target ended in the step (the trip ended, or turned back for the next load) may come back whole.
                long returned = step.Before.Trips
                    .Where(trip => trip.SourceId == after.Id && trip.Item == item && trip.Leg == TripLeg.ToTarget && LegEnded(step, trip))
                    .Sum(trip => trip.Quantity);
                refunded += returned + Math.Max(0, CarriedFrom(step.Before, after.Id, item) - CarriedFrom(step.After, after.Id, item));
                CountIf(afterHeld == limit, "stores ending with a good exactly at its limit");
                CountIf(afterHeld > limit, "stores ending with a good above its limit");
                // Deliveries and starts fill a store only up to its limit; refunds and returned loads come on top, also in the same second.
                if (afterHeld > Math.Max(limit, beforeHeld) + refunded)
                {
                    return $"building {after.Id}, {item}: stock + reserved went from {beforeHeld} to {afterHeld} (refund and returned load {refunded}), the limit is {limit}";
                }
            }
        }

        return null;
    }

    /// <summary>Whether the leg <paramref name="trip"/> walked before the step ended in it: the trip is gone, or walks a leg that began later.</summary>
    private static bool LegEnded(InvariantStep step, Trip trip)
    {
        Trip? now = step.After.Trips.FirstOrDefault(other => other.Id == trip.Id);
        return now is null || now.DepartedAt != trip.DepartedAt;
    }

    /// <summary>What every courier of the city carries of <paramref name="item"/> that came from building <paramref name="buildingId"/>.</summary>
    private static long CarriedFrom(CityState city, int buildingId, ItemId item) =>
        city.Couriers.SelectMany(courier => courier.Loads).Where(part => part.FromId == buildingId && part.Item == item).Sum(part => part.Quantity);

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
    /// Invariant 9: the houses provide at least the workers assigned plus one per trip under way, every building stays within its level's
    /// limit, a building under construction has none, and a courier station has exactly one courier per worker, in slots 1 to the count
    /// (couriers belong to stations only).
    /// </summary>
    private string? Workers(CityState city)
    {
        long provided = ContentLookup.Provided(_content, city.Buildings);
        long assigned = city.Buildings.Sum(building => (long)building.AssignedWorkers);
        CountIf(assigned + city.Trips.Count == provided && provided > 0, "steps with every worker assigned or on a trip");
        CountIf(city.Trips.Count > 0, "steps with workers on trips");
        if (assigned + city.Trips.Count > provided)
        {
            return $"{assigned} workers assigned and {city.Trips.Count} on trips, the houses provide {provided}";
        }

        foreach (Building building in city.Buildings)
        {
            int limit = building.Phase == BuildingPhase.Constructing ? 0 : Level(building).MaxWorkers;
            CountIf(building.AssignedWorkers == limit && limit > 0, "buildings at their worker limit");
            if (building.AssignedWorkers > limit)
            {
                return $"building {building.Id} ({building.Phase}, level {building.Level}) has {building.AssignedWorkers} workers, the limit is {limit}";
            }

            int[] slots = [.. city.Couriers.Where(courier => courier.StationId == building.Id).Select(courier => courier.Slot)];
            bool isStation = ContentLookup.Definition(_content, building.TypeId).Kind == BuildingKind.CourierStation;
            int expected = isStation ? building.AssignedWorkers : 0;
            CountIf(isStation && expected > 0, "steps with a staffed courier station");
            if (!slots.SequenceEqual(Enumerable.Range(1, expected)))
            {
                return $"building {building.Id} has {building.AssignedWorkers} workers and couriers in slots [{string.Join(",", slots)}]";
            }
        }

        Courier? stray = city.Couriers.FirstOrDefault(courier => !city.TryGetBuilding(courier.StationId, out _));
        return stray is null ? null : $"courier {stray.StationId}/{stray.Slot} has no station";
    }

    /// <summary>Invariant 10: busy builders within the slots; a research starts only when none runs, and the running one is not completed.</summary>
    private string? Slots(InvariantStep step)
    {
        int busy = CityQueries.BuildersBusy(step.After);
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
    /// Invariant 13: a placed type was unlocked and of the city's era or earlier when it was placed; a research started had its
    /// prerequisites and the city's era; a trip's percentage or exact amount had its mode unlocked; and at every step the completed research
    /// has all its prerequisites, and every building's type is unlocked.
    /// </summary>
    private string? Unlocks(InvariantStep step)
    {
        if (step.Mutation is not null && step.Operation is CityOperation.StartTrip { Amount.Mode: { } mode } trip)
        {
            _count("trips with a percentage or exact amount");
            bool unlocked = step.Before.CompletedResearch.Any(id => Research(id).Effects.OfType<UnlockAmountModeEffect>().Any(effect => effect.Mode == mode));
            if (!unlocked)
            {
                return $"{trip} used the {mode} mode, which no completed research unlocks";
            }
        }

        CountIf(step.Mutation is not null && step.Operation is CityOperation.CancelDelivery, "deliveries cancelled");
        if (step.Mutation is not null && step.Operation is CityOperation.SetRoute route)
        {
            _count("routes set");
            foreach (RouteAction action in route.Stops.SelectMany(stop => stop.Actions).Where(action => action.Amount.Mode.HasValue))
            {
                _count("route actions with a percentage or exact amount");
                AmountMode routeMode = action.Amount.Mode!.Value;
                if (!step.Before.CompletedResearch.Any(id => Research(id).Effects.OfType<UnlockAmountModeEffect>().Any(effect => effect.Mode == routeMode)))
                {
                    return $"{route} used the {routeMode} mode, which no completed research unlocks";
                }
            }
        }

        if (step.Mutation is not null && step.Operation is CityOperation.PlaceBuilding
            && step.After.TryGetBuilding(step.Before.NextBuildingId, out Building? placed))
        {
            BuildingDefinition type = ContentLookup.Definition(_content, placed.TypeId);
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

        Building? locked = step.After.Buildings.FirstOrDefault(building => !IsUnlocked(step.After, ContentLookup.Definition(_content, building.TypeId)));
        return locked is null ? null : $"building {locked.Id} of the locked type {locked.TypeId} stands in the city";
    }

    /// <summary>
    /// Invariant 14, the stamina half (<c>04</c> sections 2 and 19): every courier's stamina stays between 0 and its station level's; a new
    /// courier and a re-energized one have all of it; in a time step a courier spends only when its walk ends at the step's end, and then
    /// exactly the walk's seconds after <c>absentFrom</c> (none when the player is present), at most what it had; a command spends none. A
    /// courier left with none walks home or rests, and the step's report names exactly the couriers that ran out in it, at its end, and the
    /// most one of them spent.
    /// </summary>
    private string? Stamina(InvariantStep step)
    {
        long most = 0;
        var tired = new List<(int StationId, int Slot)>();
        foreach (Courier after in step.After.Couriers)
        {
            step.After.TryGetBuilding(after.StationId, out Building? station);
            long full = station is null ? 0 : Level(station).StaminaSeconds;
            string name = $"courier {after.StationId}/{after.Slot}";
            if (after.Stamina < 0 || after.Stamina > full)
            {
                return $"{name} has {after.Stamina} s of stamina, its station level's is {full}";
            }

            if (after.Stamina == 0 && after.State is not (CourierState.Returning or CourierState.Resting))
            {
                return $"{name} has no stamina left and is {after.State}";
            }

            Courier? before = step.Before.Couriers.FirstOrDefault(courier => courier.StationId == after.StationId && courier.Slot == after.Slot);
            bool reenergized = step.Operation is CityOperation.ReenergizeCouriers && step.Mutation is not null;
            if (before is null || reenergized)
            {
                CountIf(reenergized && before is not null && before.Stamina < full, "couriers re-energized");
                if (after.Stamina != full)
                {
                    return $"{name} is {(before is null ? "new" : "re-energized")} with {after.Stamina} s of stamina, not its station level's {full}";
                }

                continue;
            }

            long expected = 0;
            if (step.Mutation is null && before.ArrivesAt == step.After.Clock && before.DepartedAt is { } departed
                && step.After.Clock > step.Before.Clock && step.AbsentFrom is { } absentFrom)
            {
                expected = Math.Min(before.Stamina, Math.Max(0, step.After.Clock - Math.Max(departed, absentFrom)));
            }

            long spent = before.Stamina - after.Stamina;
            if (spent != expected)
            {
                return $"{name} spent {spent} s of stamina in the step, the model says {expected} (its walk {before.DepartedAt}..{before.ArrivesAt})";
            }

            CountIf(spent > 0, "courier walks that spent stamina");
            CountIf(spent > 0 && before.DepartedAt < step.AbsentFrom, "courier walks that spent part of their seconds");
            most = Math.Max(most, spent);
            if (spent > 0 && after.Stamina == 0)
            {
                tired.Add((after.StationId, after.Slot));
            }
        }

        CountIf(tired.Count > 0, "steps with a courier tiring");
        CountIf(step.After.Couriers.Any(courier => courier.State == CourierState.Resting), "steps with a courier resting");
        string expectedTired = string.Join(", ", tired.Select(courier => $"{courier.StationId}/{courier.Slot}@{step.After.Clock}"));
        string reportedTired = string.Join(", ", step.TiredCouriers.Select(courier => $"{courier.StationId}/{courier.Slot}@{courier.At}"));
        return Differ("tired couriers", expectedTired, reportedTired) ?? Differ("courier seconds", StructuralEquality.Text(most), StructuralEquality.Text(step.CourierSeconds));
    }

    /// <summary>
    /// Invariant 14, the carry half: every trip carries at most the content's trip carry limit, every courier at
    /// most its station level's, and a trip a step started took its load out of its source's store, at most what the store had.
    /// </summary>
    private string? Loads(InvariantStep step)
    {
        foreach (Courier courier in step.After.Couriers)
        {
            step.After.TryGetBuilding(courier.StationId, out Building? station);
            long limit = station is null ? 0 : Level(station).CarryLimit;
            CountIf(courier.Carried > 0, "steps with a courier carrying");
            CountIf(courier.Carried == limit && limit > 0, "couriers at their carry limit");
            if (courier.Carried > limit)
            {
                return $"courier {courier.StationId}/{courier.Slot} carries {courier.Carried}, its station's limit is {limit}";
            }
        }

        CountIf(step.Before.Couriers.Any(courier => courier.ArrivesAt <= step.After.Clock) && step.After.Clock > step.Before.Clock, "steps with a courier arriving");
        Trip? heavy = step.After.Trips.FirstOrDefault(trip => trip.Quantity > _content.Rules.TripCarryLimit);
        if (heavy is not null)
        {
            return $"trip {heavy.Id} carries {heavy.Quantity}, the limit is {_content.Rules.TripCarryLimit}";
        }

        foreach (Trip trip in step.After.Trips.Where(trip => !step.Before.Trips.Any(other => other.Id == trip.Id)))
        {
            _count("trips started");
            CountIf(trip.Quantity == _content.Rules.TripCarryLimit, "trips at the carry limit");
            step.Before.TryGetBuilding(trip.SourceId, out Building? source);
            if (source is null || source.GetStock(trip.Item) < trip.Quantity)
            {
                return $"trip {trip.Id} took {trip.Quantity} {trip.Item} from building {trip.SourceId}, which had {source?.GetStock(trip.Item) ?? 0}";
            }
        }

        CountIf(step.Before.Trips.Any(trip => !step.After.Trips.Any(other => other.Id == trip.Id)), "steps with a trip arriving");
        return null;
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

    /// <summary>The definition of a building's current level.</summary>
    private BuildingLevelDefinition Level(Building building) => ContentLookup.Definition(_content, building.TypeId).GetLevel(building.Level);

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
