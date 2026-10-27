namespace CityBuilder.Api.Contracts;

/// <summary>The <c>city</c> object of a <see cref="CitySnapshot"/>: the settled state of a player's city (<c>core-instructions/04-simulation-model.md</c>).</summary>
/// <param name="Clock">The city clock in seconds, JSON <c>clock</c>; the client's display clock is this plus the seconds since it received the snapshot,
/// times <c>rules.timeScale</c>.</param>
/// <param name="Era">The city's era number, JSON <c>era</c>.</param>
/// <param name="Inventory">The stock, JSON <c>inventory</c>: item id to quantity, by item id; an item the city has none of is absent.</param>
/// <param name="StorageCaps">The storage cap of each capped item now, JSON <c>storageCaps</c>: base cap plus the bonuses of completed research, by item id.</param>
/// <param name="Workers">The workers, JSON <c>workers</c>.</param>
/// <param name="Builders">The builder slots, JSON <c>builders</c>.</param>
/// <param name="Research">The research slot and the completed research, JSON <c>research</c>.</param>
/// <param name="Buildings">The placed buildings, JSON <c>buildings</c>, by ascending id.</param>
public sealed record SnapshotCity(
    long Clock,
    int Era,
    IReadOnlyDictionary<string, long> Inventory,
    IReadOnlyDictionary<string, long> StorageCaps,
    SnapshotWorkers Workers,
    SnapshotBuilders Builders,
    SnapshotResearch Research,
    IReadOnlyList<SnapshotBuilding> Buildings);
