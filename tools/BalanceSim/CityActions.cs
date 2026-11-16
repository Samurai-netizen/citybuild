using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.BalanceSim;

/// <summary>
/// The player's actions, as the strategies use them: each calls the engine's own rule (<see cref="CityRules"/>, <see cref="TripRules"/>,
/// <see cref="CourierRules"/>), and each that applies ends with a zero-length advance, as every server request does (core-instructions/
/// 03-architecture.md, request flow). The recorder gets the coins spent, every rejection and the zero-length advance's report.
/// </summary>
public sealed class CityActions
{
    /// <summary>The city the actions change.</summary>
    private readonly CityState _city;

    /// <summary>The game content.</summary>
    private readonly GameContent _content;

    /// <summary>Where spending, rejections and reports go.</summary>
    private readonly RunRecorder _recorder;

    /// <summary>Creates the actions of one run.</summary>
    /// <param name="city">The city.</param>
    /// <param name="content">The game content.</param>
    /// <param name="recorder">The run's recorder.</param>
    public CityActions(CityState city, GameContent content, RunRecorder recorder)
    {
        _city = city;
        _content = content;
        _recorder = recorder;
    }

    /// <summary>Places a building (<see cref="CityRules.PlaceBuilding"/>).</summary>
    /// <param name="type">The building type.</param>
    /// <param name="x">The tile column.</param>
    /// <param name="y">The tile row.</param>
    /// <returns>Whether it applied.</returns>
    public bool Place(BuildingTypeId type, int x, int y) =>
        Apply("PlaceBuilding", CityRules.PlaceBuilding(_city, _content, type, x, y), SpendCategory.Placement);

    /// <summary>Upgrades a building (<see cref="CityRules.UpgradeBuilding"/>).</summary>
    /// <param name="buildingId">The building's id.</param>
    /// <returns>Whether it applied.</returns>
    public bool Upgrade(int buildingId) =>
        Apply("UpgradeBuilding", CityRules.UpgradeBuilding(_city, _content, buildingId), SpendCategory.Upgrade);

    /// <summary>Sets a building's worker count (<see cref="CityRules.AssignWorkers"/>).</summary>
    /// <param name="buildingId">The building's id.</param>
    /// <param name="workers">The new count.</param>
    /// <returns>Whether it applied.</returns>
    public bool AssignWorkers(int buildingId, int workers) =>
        Apply("AssignWorkers", CityRules.AssignWorkers(_city, _content, buildingId, workers), null);

    /// <summary>Starts a research node (<see cref="CityRules.StartResearch"/>); the recorder notes the start time.</summary>
    /// <param name="research">The node.</param>
    /// <returns>Whether it applied.</returns>
    public bool StartResearch(ResearchId research)
    {
        long clock = _city.Clock;
        bool applied = Apply("StartResearch", CityRules.StartResearch(_city, _content, research), SpendCategory.Research);
        if (applied)
        {
            _recorder.ResearchStarted(research, clock);
        }

        return applied;
    }

    /// <summary>Sends a delivery (<see cref="TripRules.StartTrip"/>).</summary>
    /// <param name="sourceId">The building the goods come from.</param>
    /// <param name="targetId">The building they go to.</param>
    /// <param name="item">The good.</param>
    /// <param name="amount">How much of the source's stock.</param>
    /// <returns>Whether it applied.</returns>
    public bool SendDelivery(int sourceId, int targetId, ItemId item, Amount amount) =>
        Apply("StartTrip", TripRules.StartTrip(_city, _content, sourceId, targetId, item, amount), null);

    /// <summary>Sets a courier's route (<see cref="CourierRules.SetRoute"/>).</summary>
    /// <param name="stationId">The courier station's id.</param>
    /// <param name="slot">The courier's slot.</param>
    /// <param name="stops">The stops.</param>
    /// <returns>Whether it applied.</returns>
    public bool SetRoute(int stationId, int slot, IReadOnlyList<RouteStop> stops) =>
        Apply("SetRoute", CourierRules.SetRoute(_city, _content, stationId, slot, stops), null);

    /// <summary>Re-energizes every courier (<see cref="CourierRules.ReenergizeCouriers"/>).</summary>
    /// <returns>Whether it applied (always).</returns>
    public bool ReenergizeCouriers() => Apply("ReenergizeCouriers", CourierRules.ReenergizeCouriers(_city, _content), null);

    /// <summary>Records a rule's result and, when it applied, the coins it spent and the request's closing zero-length advance.</summary>
    /// <param name="action">The rule's name, for the rejection table.</param>
    /// <param name="result">What the rule returned.</param>
    /// <param name="category">Where its coins count, or null for an action that spends none.</param>
    /// <returns>Whether it applied.</returns>
    private bool Apply(string action, MutationResult result, SpendCategory? category)
    {
        if (!result.IsOk)
        {
            _recorder.Rejected(action, result.ErrorCode!.Value);
            return false;
        }

        if (category.HasValue)
        {
            foreach (ItemQuantity spent in result.Spent)
            {
                if (spent.Item == _content.Currency)
                {
                    _recorder.Spent(category.Value, spent.Quantity);
                }
            }
        }

        // The player is present while the strategy acts, so the advance has no absence (04 §2).
        _recorder.Advance(_city, 0);
        return true;
    }
}
