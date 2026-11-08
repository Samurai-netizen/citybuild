namespace CityBuilder.Api.Contracts;

/// <summary>
/// One part of a courier's load in <see cref="SnapshotCourier.Load"/> (CR-0008): goods of one kind taken from one building, which gets back
/// what isn't delivered when the courier is stopped.
/// </summary>
/// <param name="ItemId">The good, JSON <c>itemId</c>.</param>
/// <param name="FromId">The building the goods came from, JSON <c>fromId</c>.</param>
/// <param name="Quantity">How many, JSON <c>quantity</c>; at least 1.</param>
public sealed record SnapshotCourierLoad(string ItemId, int FromId, long Quantity);
