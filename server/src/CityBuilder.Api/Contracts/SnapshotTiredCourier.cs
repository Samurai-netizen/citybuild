namespace CityBuilder.Api.Contracts;

/// <summary>
/// One entry of a <see cref="SnapshotSettlement"/>'s <c>tiredCouriers</c>: a courier whose stamina ran out during the settlement
/// (<c>core-instructions/04-simulation-model.md</c>, sections 2 and 11). It walked home and rests until the player re-energizes it.
/// </summary>
/// <param name="StationId">The courier's station, JSON <c>stationId</c>.</param>
/// <param name="Slot">The courier's slot at its station, from 1, JSON <c>slot</c>.</param>
/// <param name="At">The city second its stamina ran out, JSON <c>at</c>.</param>
public sealed record SnapshotTiredCourier(int StationId, int Slot, long At);
