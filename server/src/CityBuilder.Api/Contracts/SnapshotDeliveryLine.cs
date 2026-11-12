namespace CityBuilder.Api.Contracts;

/// <summary>
/// One line of a courier's delivery queue in <see cref="SnapshotCity.DeliveryLines"/> (<c>core-instructions/04-simulation-model.md</c>, section
/// 18; CR-0008 amendment 1): "take at the source, put at the target". Its courier runs its lines in id order; the first is the one it runs now.
/// </summary>
/// <param name="Id">The line's id in its city, JSON <c>id</c>; given in send order from 1 and never reused; <c>cancel-delivery</c> names it.</param>
/// <param name="StationId">The courier station of the courier that runs it, JSON <c>stationId</c>.</param>
/// <param name="Slot">That courier's slot, JSON <c>slot</c>.</param>
/// <param name="SourceId">The building the goods are taken from, JSON <c>sourceId</c>.</param>
/// <param name="TargetId">The building they are put into, JSON <c>targetId</c>.</param>
/// <param name="ItemId">The good, JSON <c>itemId</c>.</param>
/// <param name="Quantity">The most the line moves, JSON <c>quantity</c>.</param>
public sealed record SnapshotDeliveryLine(int Id, int StationId, int Slot, int SourceId, int TargetId, string ItemId, long Quantity);
