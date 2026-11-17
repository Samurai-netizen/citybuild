namespace CityBuilder.Api.Contracts;

/// <summary>
/// One worker's delivery under way in <see cref="SnapshotCity.Trips"/> (<c>core-instructions/04-simulation-model.md</c>, section 18; CR-0008
/// D21): a worker shuttling goods from a building's store to another building, one load at a time. The client draws the current leg from the
/// path rule and the two times.
/// </summary>
/// <param name="Id">The trip's id in its city, JSON <c>id</c>; given in start order from 1 and never reused.</param>
/// <param name="SourceId">The building the goods came from, JSON <c>sourceId</c>; the worker carries back what the target doesn't take.</param>
/// <param name="TargetId">The building the goods go to, JSON <c>targetId</c>.</param>
/// <param name="ItemId">The good, JSON <c>itemId</c>.</param>
/// <param name="Quantity">The load, JSON <c>quantity</c>: above 0 on the way to the target and while waiting; on the way back 0, or the goods
/// the target didn't take (then nothing remains to fetch).</param>
/// <param name="DepartedAt">The city-clock second the worker began the current leg, JSON <c>departedAt</c>.</param>
/// <param name="ArrivesAt">The city-clock second the current leg ends, JSON <c>arrivesAt</c>: a delivery at the target or a pickup at the source;
/// while waiting, the end of the walk back.</param>
/// <param name="Remaining">How many are still to be fetched from the source by later loads, JSON <c>remaining</c>.</param>
/// <param name="Leg">Which way the worker walks, JSON <c>leg</c>: <c>ToTarget</c>, <c>ToSource</c>, or <c>Waiting</c> at the source with goods
/// its full store can't take yet (M0-C-3).</param>
public sealed record SnapshotTrip(int Id, int SourceId, int TargetId, string ItemId, long Quantity, long DepartedAt, long ArrivesAt, long Remaining, string Leg);
