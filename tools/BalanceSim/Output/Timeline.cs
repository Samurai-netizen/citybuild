using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.BalanceSim.Output;

/// <summary>
/// The columns of <c>timeline.csv</c> (docs/modules/BalanceSim.md → Output) and one row of the city as it stands. The item and building
/// columns follow the content's order, so a content change adds or removes columns without code changes.
/// </summary>
public sealed class Timeline
{
    /// <summary>The stall kinds in the order of their columns.</summary>
    private static readonly StallKind[] _stallKinds = [StallKind.NoWorkers, StallKind.MissingInput, StallKind.StorageFull, StallKind.Paused];

    /// <summary>The game content.</summary>
    private readonly GameContent _content;

    /// <summary>The goods with a stock column: every item but the currency, in content order.</summary>
    private readonly ItemId[] _goods;

    /// <summary>Makes the layout for a content.</summary>
    /// <param name="content">The game content.</param>
    public Timeline(GameContent content)
    {
        _content = content;
        _goods = content.Items.Where(item => !item.IsCurrency).Select(item => item.Id).ToArray();
        var header = new List<string> { "time_s", "clock", "present", "purse", "coins_earned", "coins_per_h" };
        header.AddRange(_goods.Select(item => "stock_" + item.Value));
        foreach (BuildingDefinition building in content.Buildings)
        {
            header.Add(building.Id.Value + "_count");
            header.Add(building.Id.Value + "_levels");
        }

        header.AddRange(["builders_busy", "workers_provided", "workers_assigned", "workers_free", "couriers", "couriers_resting", "research_active", "research_done"]);
        header.Add("producing");
        header.AddRange(_stallKinds.Select(kind => "stalled_" + kind));
        Header = header;
    }

    /// <summary>The column names.</summary>
    public IReadOnlyList<string> Header { get; }

    /// <summary>One row: the city now, and the coins earned since the previous row.</summary>
    /// <param name="city">The city.</param>
    /// <param name="present">Whether the player is checked in.</param>
    /// <param name="coinsEarned">The coins produced since the previous row.</param>
    /// <param name="seconds">The seconds since the previous row.</param>
    /// <returns>The values, one per header column.</returns>
    public string[] Row(CityState city, bool present, long coinsEarned, long seconds)
    {
        var row = new List<string>
        {
            Format.Number(city.Clock), Format.Clock(city.Clock), present ? "1" : "0", Format.Number(city.Purse),
            Format.Number(coinsEarned), Format.Number(Format.PerHour(coinsEarned, seconds)),
        };
        row.AddRange(_goods.Select(item => Format.Number(CityQueries.Total(city, _content, item))));
        foreach (BuildingDefinition building in _content.Buildings)
        {
            Building[] ofType = city.Buildings.Where(candidate => candidate.TypeId == building.Id).ToArray();
            row.Add(Format.Number(ofType.Length));
            row.Add(Format.Number(ofType.Sum(candidate => (long)candidate.Level)));
        }

        long provided = CityQueries.WorkersProvided(city, _content);
        long assigned = CityQueries.WorkersAssigned(city);
        row.Add(Format.Number(CityQueries.BuildersBusy(city)));
        row.Add(Format.Number(provided));
        row.Add(Format.Number(assigned));
        row.Add(Format.Number(CityQueries.FreeWorkers(city, _content)));
        row.Add(Format.Number(city.Couriers.Count));
        row.Add(Format.Number(city.Couriers.Count(courier => courier.State == CourierState.Resting)));
        row.Add(city.ActiveResearch?.Id.Value ?? string.Empty);
        row.Add(Format.Number(city.CompletedResearch.Count));

        var stalled = new long[_stallKinds.Length];
        long producing = 0;
        foreach (Building building in city.Buildings)
        {
            if (!_content.TryGetBuilding(building.TypeId, out BuildingDefinition? definition)
                || definition.Kind != BuildingKind.Producer || building.Phase is BuildingPhase.Constructing or BuildingPhase.Upgrading)
            {
                continue;
            }

            if (StallReason.Of(city, _content, building) is { } reason)
            {
                int index = Array.IndexOf(_stallKinds, reason.Kind);
                if (index >= 0)
                {
                    stalled[index]++;
                }
            }
            else
            {
                producing++;
            }
        }

        row.Add(Format.Number(producing));
        row.AddRange(stalled.Select(Format.Number));
        return [.. row];
    }
}
