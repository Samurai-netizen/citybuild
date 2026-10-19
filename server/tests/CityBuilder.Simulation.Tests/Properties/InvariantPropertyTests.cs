namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// Invariants 4–13 of <c>04</c> section 10, one test each per content: every step of every property seed's history keeps it (the steps and
/// the checks: <see cref="InvariantObserver"/>, <see cref="InvariantChecks"/>). A failure names the invariant, content, seed and step and
/// prints the operation list; <c>docs/how-to/debug-determinism-failure.md</c> turns it into a rule test.
/// </summary>
public sealed class InvariantPropertyTests(ITestOutputHelper output)
{
    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_EveryStep_ConservesEachItem(string contentName) => Holds(contentName, Invariant.Conservation);

    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_EveryStep_LeavesNoStockBelowZero(string contentName) => Holds(contentName, Invariant.NoNegativeStock);

    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_EveryStep_ProductionKeepsStockPlusReservedWithinTheCap(string contentName) => Holds(contentName, Invariant.StorageCap);

    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_EveryStep_MovesTheClockByTheRequestedTimeOnly(string contentName) => Holds(contentName, Invariant.ClockNeverDecreases);

    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_EveryStep_ProducesNothingBeforeConstructionOrDuringAnUpgrade(string contentName) =>
        Holds(contentName, Invariant.NoProductionWhileBuilding);

    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_EveryStep_AssignsNoMoreWorkersThanProvidedOrAllowed(string contentName) => Holds(contentName, Invariant.WorkerLimits);

    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_EveryStep_KeepsBuildersWithinTheSlotsAndOneResearch(string contentName) =>
        Holds(contentName, Invariant.BuilderAndResearchSlots);

    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_EveryStep_CompletesEachCycleWhenItsWorkerSecondsReachItsLabor(string contentName) =>
        Holds(contentName, Invariant.LaborAccounting);

    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_EveryStep_StartsNoCycleAfterTheProductionCap(string contentName) => Holds(contentName, Invariant.NoStartAfterTheCap);

    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_EveryStep_NeverPlacesALockedBuildingOrSkipsAPrerequisite(string contentName) =>
        Holds(contentName, Invariant.UnlockAndPrerequisites);

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

    private static void Holds(string contentName, Invariant invariant) => InvariantObservation.For(contentName).AssertHolds(invariant);
}
