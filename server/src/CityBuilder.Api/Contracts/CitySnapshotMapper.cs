using System.Globalization;
using CityBuilder.Application.Cities;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Api.Contracts;

/// <summary>
/// Maps a settled city to the <see cref="CitySnapshot"/> of <c>GET /api/v1/city</c> and of the command endpoints (<c>CommandEndpoints</c>), which pass what
/// their <c>CommandSuccess</c> holds. It only reads: every number comes from the city and the engine's queries (<see cref="CityQueries"/>,
/// <see cref="StallReason"/>), so the client and the server never disagree on a derived value. Lists and maps are sorted explicitly, and
/// the building phases have written wire names, like the content (<see cref="ContentResponseMapper"/>).
/// </summary>
public static class CitySnapshotMapper
{
    /// <summary>The format of <c>serverTimeUtc</c>: whole seconds, UTC, a literal <c>Z</c> (<c>05</c>).</summary>
    private const string _serverTimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    /// <summary>Maps the settled city and its settlement to the snapshot.</summary>
    /// <param name="requestId">The request id of the command that produced it, or <see langword="null"/> for a read.</param>
    /// <param name="city">The city after the request, settled; the snapshot reads it and changes nothing.</param>
    /// <param name="settlement">The settlement that ran in the request.</param>
    /// <param name="stateVersion">The city's stored version after the request.</param>
    /// <param name="serverTime">The server time of the request; the snapshot shows it floored to the second.</param>
    /// <param name="content">The game content the city was settled with.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="ArgumentNullException">A reference is null.</exception>
    /// <exception cref="InvalidOperationException">The city has a building or research the content doesn't know, or a building phase this
    /// mapper doesn't know (the city and the content don't belong together, or an engine change forgot the contract).</exception>
    public static CitySnapshot ToSnapshot(
        Guid? requestId, CityAggregate city, Settlement settlement, long stateVersion, DateTimeOffset serverTime, GameContent content)
    {
        ArgumentNullException.ThrowIfNull(city);
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(content);

        CityState state = city.State;
        return new CitySnapshot(
            requestId,
            stateVersion,
            DateTimeOffset.FromUnixTimeSeconds(serverTime.ToUnixTimeSeconds()).ToString(_serverTimeFormat, CultureInfo.InvariantCulture),
            content.ContentVersion,
            new SnapshotCity(
                state.Clock,
                state.Era,
                ToAmounts(state.Inventory),
                ToStorageCaps(state, content),
                new SnapshotWorkers(
                    CityQueries.WorkersProvided(state, content), CityQueries.WorkersAssigned(state), CityQueries.FreeWorkers(state, content)),
                new SnapshotBuilders(content.Rules.BuilderSlots, CityQueries.BuildersBusy(state)),
                ToResearch(state),
                state.Buildings.OrderBy(building => building.Id).Select(building => ToBuilding(state, content, building)).ToArray()),
            new SnapshotSettlement(
                settlement.ElapsedSeconds,
                settlement.ProductionSeconds,
                ToAmounts(settlement.Produced),
                ToAmounts(settlement.Consumed),
                settlement.CompletedBuildings.Order().ToArray(),
                settlement.CompletedResearch.Select(id => id.Value).Order(StringComparer.Ordinal).ToArray(),
                settlement.EraChanged));
    }

    /// <summary>Maps the research slot and the completed research.</summary>
    /// <param name="state">The city.</param>
    /// <returns>The <c>research</c> object; the running research's fields are null when the slot is free.</returns>
    private static SnapshotResearch ToResearch(CityState state)
    {
        ActiveResearch? active = state.ActiveResearch;
        return new SnapshotResearch(
            active?.Id.Value,
            active?.StartedAt,
            active?.EndsAt,
            state.CompletedResearch.Select(id => id.Value).Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>Maps one building, with the values the engine derives for display.</summary>
    /// <param name="state">The city the building is in.</param>
    /// <param name="content">The game content.</param>
    /// <param name="building">The building.</param>
    /// <returns>Its snapshot object.</returns>
    private static SnapshotBuilding ToBuilding(CityState state, GameContent content, Building building)
    {
        if (!content.TryGetBuilding(building.TypeId, out BuildingDefinition? definition))
        {
            throw new InvalidOperationException($"Building {building.Id} has type {building.TypeId}, which the content does not know.");
        }

        return new SnapshotBuilding(
            building.Id,
            building.TypeId.Value,
            building.X,
            building.Y,
            building.Level,
            building.AssignedWorkers,
            definition.GetLevel(building.Level).MaxWorkers,
            building.Phase switch
            {
                BuildingPhase.Constructing => "Constructing",
                BuildingPhase.Upgrading => "Upgrading",
                BuildingPhase.Idle => "Idle",
                BuildingPhase.Producing => "Producing",
                _ => throw new InvalidOperationException($"Building phase {building.Phase} has no wire name; add it to the contract."),
            },
            building.PhaseStartedAt,
            building.PhaseEndsAt,
            building.CycleLabor,
            building.LaborRemaining,
            building.SegmentStartedAt,
            StallReason.Of(state, content, building)?.ToString());
    }

    /// <summary>The storage cap of each capped item now, by item id.</summary>
    /// <param name="state">The city, whose completed research raises the caps.</param>
    /// <param name="content">The game content.</param>
    /// <returns>Item id to cap; uncapped items are absent.</returns>
    private static SortedDictionary<string, long> ToStorageCaps(CityState state, GameContent content)
    {
        var caps = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (ItemDefinition item in content.Items)
        {
            if (CityQueries.TryGetEffectiveCap(state, content, item.Id, out long cap))
            {
                caps.Add(item.Id.Value, cap);
            }
        }

        return caps;
    }

    /// <summary>Maps item quantities to a JSON object: item id to quantity, in ordinal id order.</summary>
    /// <param name="quantities">The quantities, for example the stock or a settlement's totals.</param>
    /// <returns>The map, sorted by item id.</returns>
    private static SortedDictionary<string, long> ToAmounts(IReadOnlyDictionary<ItemId, long> quantities)
    {
        var amounts = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (KeyValuePair<ItemId, long> quantity in quantities)
        {
            amounts.Add(quantity.Key.Value, quantity.Value);
        }

        return amounts;
    }
}
