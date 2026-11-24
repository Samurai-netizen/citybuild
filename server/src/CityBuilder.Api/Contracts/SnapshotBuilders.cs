namespace CityBuilder.Api.Contracts;

/// <summary>The <c>builders</c> object of a <see cref="SnapshotCity"/>: the builder slots that limit constructions and upgrades at once.</summary>
/// <param name="Slots">How many buildings can be under construction or upgrade at once, JSON <c>slots</c>: the content's plus completed <c>builderSlotBonus</c> research (M1-02).</param>
/// <param name="Busy">How many are under construction or upgrade now, JSON <c>busy</c>.</param>
/// <param name="Queued">How many orders wait in the builder queue for a free builder, JSON <c>queued</c> (CR-0013).</param>
/// <param name="QueueLength">How many orders the builder queue holds, JSON <c>queueLength</c>: the content's <c>builderQueueLength</c>; 0 means
/// no queue (CR-0013).</param>
public sealed record SnapshotBuilders(int Slots, int Busy, int Queued, int QueueLength);
