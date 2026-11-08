namespace CityBuilder.Api.Contracts;

/// <summary>
/// One courier in <see cref="SnapshotCity.Couriers"/> (<c>core-instructions/04-simulation-model.md</c>, section 19; CR-0008): the worker in a
/// courier station's slot, which walks the slot's route in a loop. The client draws the walk from the path rule and the two times.
/// </summary>
/// <param name="StationId">The courier station's building id, JSON <c>stationId</c>.</param>
/// <param name="Slot">The slot in the station, from 1, JSON <c>slot</c>; its route is the slot's.</param>
/// <param name="State">What it does, JSON <c>state</c>: <c>AtStation</c>, <c>Walking</c>, <c>Returning</c> or <c>Resting</c>.</param>
/// <param name="ToStop">The stop (1-based) it walks to while <c>Walking</c>, else 0, JSON <c>toStop</c>.</param>
/// <param name="DepartedAt">The city-clock second its walk started, JSON <c>departedAt</c>; <c>null</c> when it doesn't walk.</param>
/// <param name="ArrivesAt">The city-clock second its walk ends, JSON <c>arrivesAt</c>; <c>null</c> when it doesn't walk.</param>
/// <param name="Stamina">Its stamina in seconds, JSON <c>stamina</c>; walks after the absence start spend it (part H).</param>
/// <param name="Load">What it carries, JSON <c>load</c>, by item id, then the building each part came from.</param>
public sealed record SnapshotCourier(
    int StationId, int Slot, string State, int ToStop, long? DepartedAt, long? ArrivesAt, long Stamina, IReadOnlyList<SnapshotCourierLoad> Load);
