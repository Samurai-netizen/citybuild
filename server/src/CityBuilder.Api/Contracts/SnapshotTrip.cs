namespace CityBuilder.Api.Contracts;

/// <summary>
/// One manual trip under way in <see cref="SnapshotCity.Trips"/> (<c>core-instructions/04-simulation-model.md</c>, section 18; CR-0008): a
/// worker carrying goods from a building's store to another building. The client draws the walk from the path rule and the two times.
/// </summary>
/// <param name="Id">The trip's id in its city, JSON <c>id</c>; given in start order from 1 and never reused.</param>
/// <param name="SourceId">The building the goods came from, JSON <c>sourceId</c>; it takes back what doesn't fit.</param>
/// <param name="TargetId">The building the goods go to, JSON <c>targetId</c>.</param>
/// <param name="ItemId">The good, JSON <c>itemId</c>.</param>
/// <param name="Quantity">The load, JSON <c>quantity</c>.</param>
/// <param name="DepartedAt">The city-clock second the worker left, JSON <c>departedAt</c>.</param>
/// <param name="ArrivesAt">The city-clock second the worker arrives and delivers, JSON <c>arrivesAt</c>.</param>
public sealed record SnapshotTrip(int Id, int SourceId, int TargetId, string ItemId, long Quantity, long DepartedAt, long ArrivesAt);
