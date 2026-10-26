namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// Invariants 4–13 of <c>04</c> section 10, one test each per content: every step of every property seed's history keeps it (the steps and
/// the checks: <see cref="InvariantObserver"/>, <see cref="InvariantChecks"/>). A failure names the invariant, content, seed and step and
/// prints the operation list; <c>docs/how-to/debug-determinism-failure.md</c> turns it into a rule test.
/// </summary>
/// <param name="output">Receives the counts of what the checks exercised, one line per content.</param>
public sealed class InvariantPropertyTests(ITestOutputHelper output)
{
    /// <summary>Invariant 4: on every step each item's stock changes only by what was produced, consumed, spent and refunded.</summary>
    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_EveryStep_ConservesEachItem(string contentName) => Holds(contentName, Invariant.Conservation);

    /// <summary>Invariant 5: no step leaves an item below zero.</summary>
    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_EveryStep_LeavesNoStockBelowZero(string contentName) => Holds(contentName, Invariant.NoNegativeStock);

    /// <summary>Invariant 6: no step's production takes stock + reserved of a capped item above its cap.</summary>
    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_EveryStep_ProductionKeepsStockPlusReservedWithinTheCap(string contentName) => Holds(contentName, Invariant.StorageCap);

    /// <summary>Invariant 7: every step moves the clock by exactly the requested seconds, a negative request by none.</summary>
    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_EveryStep_MovesTheClockByTheRequestedTimeOnly(string contentName) => Holds(contentName, Invariant.ClockNeverDecreases);

    /// <summary>Invariant 8: no step produces anything from a building before its construction completed or during an upgrade.</summary>
    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_EveryStep_ProducesNothingBeforeConstructionOrDuringAnUpgrade(string contentName) =>
        Holds(contentName, Invariant.NoProductionWhileBuilding);

    /// <summary>Invariant 9: after every step the assigned workers are within what the houses provide and each building's limit.</summary>
    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_EveryStep_AssignsNoMoreWorkersThanProvidedOrAllowed(string contentName) => Holds(contentName, Invariant.WorkerLimits);

    /// <summary>Invariant 10: after every step the busy builders are within the slots, and at most one research is active.</summary>
    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_EveryStep_KeepsBuildersWithinTheSlotsAndOneResearch(string contentName) =>
        Holds(contentName, Invariant.BuilderAndResearchSlots);

    /// <summary>Invariant 11: every cycle completes at the first second its worker-seconds reach its labor, over every worker change.</summary>
    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_EveryStep_CompletesEachCycleWhenItsWorkerSecondsReachItsLabor(string contentName) =>
        Holds(contentName, Invariant.LaborAccounting);

    /// <summary>Invariant 12: no step starts a cycle after its window's last second, and every dormant tail does what the model says.</summary>
    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_EveryStep_StartsNoCycleAfterTheProductionCap(string contentName) => Holds(contentName, Invariant.NoStartAfterTheCap);

    /// <summary>Invariant 13: no step places a locked building or starts a research without its prerequisites and era.</summary>
    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_EveryStep_NeverPlacesALockedBuildingOrSkipsAPrerequisite(string contentName) =>
        Holds(contentName, Invariant.UnlockAndPrerequisites);

    /// <summary>
    /// The seeds exercise every check: each counted case happened at least a floor number of times, so a check that never fires can't pass
    /// for one that holds. The counts are printed.
    /// </summary>
    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_PropertySeeds_ExerciseEveryCheck(string contentName)
    {
        InvariantObservation observation = InvariantObservation.For(contentName);
        output.WriteLine($"{contentName}: {observation.CountsText()}");

        observation.AssertAtLeast("steps that spent", 1000);
        observation.AssertAtLeast("steps that refunded", 100);
        observation.AssertAtLeast("steps asked to go back in time", PropertyContent.Seeds);
        observation.AssertAtLeast("steps ending with an item exactly at its cap", 1000);
        observation.AssertAtLeast("steps ending with an item above its cap", 20);
        observation.AssertAtLeast("cycles completed", 10_000);
        observation.AssertAtLeast("cycles started when a construction or upgrade completed", 100);
        observation.AssertAtLeast("steps with every worker assigned", 1000);
        observation.AssertAtLeast("buildings at their worker limit", 1000);
        observation.AssertAtLeast("steps with every builder busy", 50);
        observation.AssertAtLeast("research starts", 100);
        observation.AssertAtLeast("cycles completed after a worker change", 100);
        observation.AssertAtLeast("cycles ended by an upgrade", 50);
        observation.AssertAtLeast("frozen cycles carried", 1000);
        observation.AssertAtLeast("cycles started with a labor reduction", 100);
        observation.AssertAtLeast("cycles started at the last second of a window", 5);
        observation.AssertAtLeast("dormant tails", 500);
        observation.AssertAtLeast("dormant tails with a completion", 10);
        observation.AssertAtLeast("placements of a type locked at the start", 20);
        observation.AssertAtLeast("research starts with prerequisites", 20);
    }

    /// <summary>Fails with the first failure of the invariant on the content, if any seed had one.</summary>
    private static void Holds(string contentName, Invariant invariant) => InvariantObservation.For(contentName).AssertHolds(invariant);
}
