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
    public void Observe_EveryStep_MovesTheClockByTheRequestedTimeOnly(string contentName) => Holds(contentName, Invariant.ClockNeverDecreases);

    [Theory]
    [MemberData(nameof(PropertyContent.Names), MemberType = typeof(PropertyContent))]
    public void Observe_PropertySeeds_ExerciseEveryCheck(string contentName)
    {
        InvariantObservation observation = InvariantObservation.For(contentName);
        output.WriteLine($"{contentName}: {observation.CountsText()}");

        observation.AssertAtLeast("steps that spent", 1000);
        observation.AssertAtLeast("steps that refunded", 100);
        observation.AssertAtLeast("steps asked to go back in time", PropertyContent.Seeds);
    }

    private static void Holds(string contentName, Invariant invariant) => InvariantObservation.For(contentName).AssertHolds(invariant);
}
