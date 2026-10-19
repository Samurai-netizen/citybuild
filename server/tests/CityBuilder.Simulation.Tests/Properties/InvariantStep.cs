using System.Globalization;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>The invariants of <c>04</c> section 10 that the observer checks, numbered as there (1–3 are M0-14's properties).</summary>
internal enum Invariant
{
    Conservation = 4,
    NoNegativeStock = 5,
    StorageCap = 6,
    ClockNeverDecreases = 7,
    NoProductionWhileBuilding = 8,
    WorkerLimits = 9,
    BuilderAndResearchSlots = 10,
    LaborAccounting = 11,
    NoStartAfterTheCap = 12,
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

    /// <summary>The generated operation this step belongs to, and its number in the history (1-based; 0 after the history).</summary>
    public required CityOperation Operation { get; init; }

    public required int OperationNumber { get; init; }

    /// <summary>Null for a step of the history itself; for a step on a copy, how the copy left the history (<see cref="InvariantObserver"/>).</summary>
    public string? Branch { get; init; }

    /// <summary>The mutation's result when the step is a command; null for time passing.</summary>
    public MutationResult? Mutation { get; init; }

    /// <summary>The seconds the step asked the engine to advance (0 for a command), and the seconds its report says passed.</summary>
    public required long Requested { get; init; }

    public required long ReportedElapsed { get; init; }

    /// <summary>The outputs of the cycles that completed, and the inputs of the cycles that started, during the step.</summary>
    public required IReadOnlyDictionary<ItemId, long> Produced { get; init; }

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

    /// <summary>The constructions and upgrades, and the research, that completed during the step.</summary>
    public required IReadOnlyList<int> CompletedBuildings { get; init; }

    public required IReadOnlyList<ResearchId> CompletedResearch { get; init; }

    /// <summary>Where the step is, for a failure message.</summary>
    public string Describe() => string.Create(
        CultureInfo.InvariantCulture,
        $"operation {OperationNumber} ({Operation}){(Branch is null ? "" : $", {Branch}")}, step from clock {Before.Clock} to {After.Clock}{(EventAligned ? "" : " (dormant tail)")}");
}
