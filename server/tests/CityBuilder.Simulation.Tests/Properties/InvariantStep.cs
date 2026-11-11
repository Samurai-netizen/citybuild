using System.Globalization;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// The invariants of <c>04</c> section 10 that the observer checks, numbered as there (1–3 are M0-14's properties). Invariant 12, the path
/// rule, is a property of the path search, checked against a brute force over small maps (<c>Map/PathFinderBruteForceTests</c>).
/// </summary>
internal enum Invariant
{
    /// <summary>Every item's city total (the purse, the stores, the loads) changes only by what was produced, consumed, delivered, spent and refunded.</summary>
    Conservation = 4,
    /// <summary>No quantity is below zero: the purse, a store, a project's needs.</summary>
    NoNegativeStock = 5,
    /// <summary>Production and walkers never take a store's stock + reserved above its effective limit; only refunds and returned loads may.</summary>
    StorageCap = 6,
    /// <summary>The clock never decreases: it moves by exactly the requested seconds, or not at all.</summary>
    ClockNeverDecreases = 7,
    /// <summary>Nothing is produced before a construction completes, and nothing during an upgrade.</summary>
    NoProductionWhileBuilding = 8,
    /// <summary>No more workers are assigned or on trips than the houses provide, and no building has more than its level allows.</summary>
    WorkerLimits = 9,
    /// <summary>Busy builders stay within the builder slots, and at most one research is active.</summary>
    BuilderAndResearchSlots = 10,
    /// <summary>A cycle completes at the first second its worker-seconds reach its labor, over every worker change (<see cref="LaborLedger"/>).</summary>
    LaborAccounting = 11,
    /// <summary>Locked buildings are never placed, research prerequisites are always respected, and a locked amount mode is never used.</summary>
    UnlockAndPrerequisites = 13,

    /// <summary>No stamina is spent before <c>absentFrom</c>, a walk spends exactly its absent seconds, and a load never exceeds its carry limit.</summary>
    StaminaAndLoads = 14,

    /// <summary>Delivery lines belong to a courier without a route, move 1 to its carry limit between two buildings, and fit its queue.</summary>
    DeliveryLines = 15,
}

/// <summary>
/// One observed step of a city's history (<see cref="InvariantObserver"/>): a command with its zero-length advance, or a piece of an advance
/// that ends at the next due time or earlier, so every start and completion of the step happens at its end. It holds a copy of the city before, the city after, and what the engine said
/// happened in between. The invariants are checked on every step.
/// </summary>
internal sealed class InvariantStep
{
    /// <summary>A copy of the city before the step.</summary>
    public required CityState Before { get; init; }

    /// <summary>The city after the step (the engine's own object: read it before the next step changes it).</summary>
    public required CityState After { get; init; }

    /// <summary>The operation of the step: the generated one, or a branch's command.</summary>
    public required CityOperation Operation { get; init; }

    /// <summary>The number of the generated operation during or after which the step runs (1-based; 0 after the history).</summary>
    public required int OperationNumber { get; init; }

    /// <summary>Null for a step of the history itself; for a step on a copy, how the copy left the history (<see cref="InvariantObserver"/>).</summary>
    public string? Branch { get; init; }

    /// <summary>The mutation's result when the step is a command; null for time passing.</summary>
    public MutationResult? Mutation { get; init; }

    /// <summary>The seconds the step asked the engine to advance (0 for a command).</summary>
    public required long Requested { get; init; }

    /// <summary>The seconds the step's report says passed.</summary>
    public required long ReportedElapsed { get; init; }

    /// <summary>The outputs of the cycles that completed during the step.</summary>
    public required IReadOnlyDictionary<ItemId, long> Produced { get; init; }

    /// <summary>The inputs of the cycles that started during the step.</summary>
    public required IReadOnlyDictionary<ItemId, long> Consumed { get; init; }

    /// <summary>The goods walkers handed to projects during the step.</summary>
    public required IReadOnlyDictionary<ItemId, long> Delivered { get; init; }

    /// <summary>The goods walkers put into stores during the step.</summary>
    public required IReadOnlyDictionary<ItemId, long> Moved { get; init; }

    /// <summary>The inputs refunded during the step by upgrades that a delivery started (an upgrade command's refund is in <see cref="Mutation"/>).</summary>
    public required IReadOnlyDictionary<ItemId, long> Refunded { get; init; }

    /// <summary>The city second from which the player was absent in the step's advance; null when present (and for a command).</summary>
    public long? AbsentFrom { get; init; }

    /// <summary>The most stamina one courier spent in the step, as its report says.</summary>
    public required long CourierSeconds { get; init; }

    /// <summary>The couriers whose stamina ran out in the step, as its report says.</summary>
    public required IReadOnlyList<TiredCourier> TiredCouriers { get; init; }

    /// <summary>The ids of the buildings whose construction or upgrade completed during the step.</summary>
    public required IReadOnlyList<int> CompletedBuildings { get; init; }

    /// <summary>The research that completed during the step.</summary>
    public required IReadOnlyList<ResearchId> CompletedResearch { get; init; }

    /// <summary>Where the step is, for a failure message.</summary>
    public string Describe() => string.Create(
        CultureInfo.InvariantCulture,
        $"operation {OperationNumber}{(Branch is null ? $" ({Operation})" : $", {Branch}")}, step from clock {Before.Clock} to {After.Clock}{(AbsentFrom is { } absent ? $" (absent from {absent})" : "")}");
}
