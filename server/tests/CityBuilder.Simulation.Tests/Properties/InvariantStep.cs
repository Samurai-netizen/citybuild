using System.Globalization;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>The invariants of <c>04</c> section 10 that the observer checks, numbered as there (1–3 are M0-14's properties).</summary>
internal enum Invariant
{
    /// <summary>Every item's stock changes only by what was produced, consumed, spent and refunded.</summary>
    Conservation = 4,
    /// <summary>No item's stock is below zero.</summary>
    NoNegativeStock = 5,
    /// <summary>Production never takes stock + reserved of a capped item above its cap; only refunds may.</summary>
    StorageCap = 6,
    /// <summary>The clock never decreases: it moves by exactly the requested seconds, or not at all.</summary>
    ClockNeverDecreases = 7,
    /// <summary>Nothing is produced before a construction completes, and nothing during an upgrade.</summary>
    NoProductionWhileBuilding = 8,
    /// <summary>No more workers are assigned than the houses provide, and no building has more than its level allows.</summary>
    WorkerLimits = 9,
    /// <summary>Busy builders stay within the builder slots, and at most one research is active.</summary>
    BuilderAndResearchSlots = 10,
    /// <summary>A cycle completes at the first second its worker-seconds reach its labor, over every worker change (<see cref="LaborLedger"/>).</summary>
    LaborAccounting = 11,
    /// <summary>No cycle starts after the last second of its settlement window.</summary>
    NoStartAfterTheCap = 12,
    /// <summary>Locked buildings are never placed, and research prerequisites are always respected.</summary>
    UnlockAndPrerequisites = 13,
}

/// <summary>
/// One observed step of a city's history (<see cref="InvariantObserver"/>): a command with its zero-length advance, a piece of an advance, or
/// the dormant tail of an advance beyond the production cap. It holds a copy of the city before, the city after, and what the engine said
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

    /// <summary>
    /// Whether no event can lie strictly inside the step: it ends at the next due time or earlier. Then every start and completion of the
    /// step happens at its end. False only for the dormant tail.
    /// </summary>
    public required bool EventAligned { get; init; }

    /// <summary>The last second at which the step's settlement window lets cycles start (its start plus the production cap).</summary>
    public required long ProductionUntil { get; init; }

    /// <summary>For the dormant tail: what the test model says the tail does. Null for every other step.</summary>
    public DormantTail? ExpectedTail { get; init; }

    /// <summary>The ids of the buildings whose construction or upgrade completed during the step.</summary>
    public required IReadOnlyList<int> CompletedBuildings { get; init; }

    /// <summary>The research that completed during the step.</summary>
    public required IReadOnlyList<ResearchId> CompletedResearch { get; init; }

    /// <summary>Where the step is, for a failure message.</summary>
    public string Describe() => string.Create(
        CultureInfo.InvariantCulture,
        $"operation {OperationNumber}{(Branch is null ? $" ({Operation})" : $", {Branch}")}, step from clock {Before.Clock} to {After.Clock}{(EventAligned ? "" : " (dormant tail)")}");
}
