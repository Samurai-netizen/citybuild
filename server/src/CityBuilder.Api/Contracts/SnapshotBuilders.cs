namespace CityBuilder.Api.Contracts;

/// <summary>The <c>builders</c> object of a <see cref="SnapshotCity"/>: the builder slots that limit constructions and upgrades at once.</summary>
/// <param name="Slots">How many buildings can be under construction or upgrade at once, JSON <c>slots</c>.</param>
/// <param name="Busy">How many are under construction or upgrade now, JSON <c>busy</c>.</param>
public sealed record SnapshotBuilders(int Slots, int Busy);
