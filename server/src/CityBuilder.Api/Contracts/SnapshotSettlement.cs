namespace CityBuilder.Api.Contracts;

/// <summary>
/// The <c>settlement</c> object of a <see cref="CitySnapshot"/>: what the settlement of this request did, from the city's last settled second
/// to the server time of the request (<c>core-instructions/04-simulation-model.md</c>, section 11). A request seconds after another one reports
/// only those seconds, so a client that wants "what happened while I was away" keeps the one from its first call.
/// </summary>
/// <param name="ElapsedSeconds">The seconds the city clock moved, JSON <c>elapsedSeconds</c>.</param>
/// <param name="ProductionSeconds">The seconds of the window in which cycles could start, JSON <c>productionSeconds</c>; at most the offline production cap.</param>
/// <param name="Produced">What the cycles that completed made, JSON <c>produced</c>: item id to quantity, by item id; only items above 0.</param>
/// <param name="Consumed">What the cycles that started took, JSON <c>consumed</c>: item id to quantity, by item id; only items above 0.</param>
/// <param name="Delivered">What walkers handed to projects, used up there, JSON <c>delivered</c>: item id to quantity, by item id; only items above
/// 0 (CR-0008).</param>
/// <param name="Moved">What walkers put into stores, JSON <c>moved</c>: item id to quantity, by item id; only items above 0 (CR-0008).</param>
/// <param name="Refunded">The inputs given back when an upgrade started at its last delivery, JSON <c>refunded</c>: item id to quantity, by item
/// id; only items above 0 (CR-0008).</param>
/// <param name="CompletedBuildings">The ids of the buildings whose construction or upgrade completed, JSON <c>completedBuildings</c>, ascending.</param>
/// <param name="CompletedResearch">The ids of the research that completed, JSON <c>completedResearch</c>, in ordinal order.</param>
/// <param name="EraChanged">Whether the city entered a new era, JSON <c>eraChanged</c>; false until the era transition exists (M2).</param>
public sealed record SnapshotSettlement(
    long ElapsedSeconds,
    long ProductionSeconds,
    IReadOnlyDictionary<string, long> Produced,
    IReadOnlyDictionary<string, long> Consumed,
    IReadOnlyDictionary<string, long> Delivered,
    IReadOnlyDictionary<string, long> Moved,
    IReadOnlyDictionary<string, long> Refunded,
    IReadOnlyList<int> CompletedBuildings,
    IReadOnlyList<string> CompletedResearch,
    bool EraChanged);
