namespace CityBuilder.MetricsReport;

/// <summary>One row of the checkpoint explorer: the columns of docs/plan/CR-0009-T2-PLAN.md §3.4. A median is null over nobody.</summary>
/// <param name="Checkpoint">The checkpoint, named or unnamed.</param>
/// <param name="Reached">The players who reached it.</param>
/// <param name="ReachedShare">Their share of the population.</param>
/// <param name="TimesTotal">The matching events of every player, also past the reach.</param>
/// <param name="MedianTimesPerPlayer">The median matches per player who reached it.</param>
/// <param name="MedianSecondsToReach">The median real seconds from the player's creation to the reach.</param>
/// <param name="MedianCityClockAtReach">The median city clock at the reach.</param>
/// <param name="LeftAfter">Of the players who reached it, the share who left.</param>
/// <param name="LeftWithout">Of the players who never reached it, the share who left.</param>
/// <param name="Difference"><paramref name="LeftAfter"/> minus <paramref name="LeftWithout"/>, in percentage points.</param>
/// <param name="LastStop">The players who left with this as their last reached checkpoint.</param>
/// <param name="LastStopShare">Their share of all who left.</param>
/// <param name="MedianSecondsAfterReach">The median real seconds from the reach to the last time the city was seen.</param>
/// <param name="MedianSessionsAfterReach">The median sessions started after the reach.</param>
/// <param name="Detail">The detail block: every named row and the 50 most reached unnamed ones; otherwise <see langword="null"/>.</param>
public sealed record ExplorerRow(
    ReportCheckpoint Checkpoint,
    int Reached,
    Rate ReachedShare,
    long TimesTotal,
    long? MedianTimesPerPlayer,
    long? MedianSecondsToReach,
    long? MedianCityClockAtReach,
    Rate LeftAfter,
    Rate LeftWithout,
    Rate Difference,
    int LastStop,
    Rate LastStopShare,
    long? MedianSecondsAfterReach,
    long? MedianSessionsAfterReach,
    ExplorerDetail? Detail);
