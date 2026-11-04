namespace CityBuilder.Api.Contracts;

/// <summary>The <c>city</c> object of a <see cref="CitySnapshot"/>: the settled state of a player's city (<c>core-instructions/04-simulation-model.md</c>).</summary>
/// <param name="Clock">The city clock in seconds, JSON <c>clock</c>; the client's display clock is this plus the seconds since it received the snapshot,
/// times <c>rules.timeScale</c>.</param>
/// <param name="Era">The city's era number, JSON <c>era</c>.</param>
/// <param name="Purse">The coins, JSON <c>purse</c>: the currency, in no building (CR-0008).</param>
/// <param name="Totals">Every item's city total, JSON <c>totals</c>: item id to quantity, by item id, 0 included; a good's total adds every store
/// and every load on the way, the currency's is the purse (CR-0008).</param>
/// <param name="Workers">The workers, JSON <c>workers</c>.</param>
/// <param name="Builders">The builder slots, JSON <c>builders</c>.</param>
/// <param name="Research">The research slot and the completed research, JSON <c>research</c>.</param>
/// <param name="Buildings">The placed buildings, JSON <c>buildings</c>, by ascending id.</param>
/// <param name="Roads">The road tiles, JSON <c>roads</c>, by column, then row (CR-0008).</param>
/// <param name="Trips">The manual trips under way, JSON <c>trips</c>, by id (CR-0008).</param>
public sealed record SnapshotCity(
    long Clock,
    int Era,
    long Purse,
    IReadOnlyDictionary<string, long> Totals,
    SnapshotWorkers Workers,
    SnapshotBuilders Builders,
    SnapshotResearch Research,
    IReadOnlyList<SnapshotBuilding> Buildings,
    IReadOnlyList<SnapshotRoad> Roads,
    IReadOnlyList<SnapshotTrip> Trips);
