namespace CityBuilder.MetricsReport;

/// <summary>The detail block of an explorer row (docs/plan/CR-0009-T2-PLAN.md §3.4).</summary>
/// <param name="NextEvents">The five most common events right after the reach (share of the players who reached it).</param>
/// <param name="LastEventsOfLeft">The five most common last events of the players who reached it and left (share of those).</param>
/// <param name="OtherCheckpointsOfLeft">The other named checkpoints those who left reached (share of those), the most reached first.</param>
public sealed record ExplorerDetail(
    IReadOnlyList<DetailCount> NextEvents, IReadOnlyList<DetailCount> LastEventsOfLeft, IReadOnlyList<DetailCount> OtherCheckpointsOfLeft);
