namespace CityBuilder.Infrastructure.Persistence.Rows;

/// <summary>
/// A row of <c>city_route_actions</c> (core-instructions/06-data-model.md, CR-0008): one action of one stop of a station's slot's route
/// (<c>04</c> section 19). A route is its actions in order of stop, then action; every action of one stop names the same building.
/// </summary>
public sealed class CityRouteActionRow
{
    /// <summary><c>city_id</c>: the city; with the station, the slot, the stop and the action the primary key.</summary>
    public Guid CityId { get; set; }

    /// <summary><c>station_id</c>: the courier station's building id.</summary>
    public int StationId { get; set; }

    /// <summary><c>slot</c>: the slot the route belongs to, from 1.</summary>
    public short Slot { get; set; }

    /// <summary><c>stop_index</c>: the stop's place in the route, from 0.</summary>
    public short StopIndex { get; set; }

    /// <summary><c>action_index</c>: the action's place in its stop, from 0.</summary>
    public short ActionIndex { get; set; }

    /// <summary><c>building_id</c>: the stop's building.</summary>
    public int BuildingId { get; set; }

    /// <summary><c>kind</c>: <c>take</c> or <c>put</c>, as the API writes it.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary><c>item_id</c>: the good's content id.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary><c>amount</c>: the amount as the API writes it (<c>05</c> → Amounts): a preset id, a percentage or a number.</summary>
    public string Amount { get; set; } = string.Empty;
}
