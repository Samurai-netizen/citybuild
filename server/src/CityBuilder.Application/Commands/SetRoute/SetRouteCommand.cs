using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Commands.SetRoute;

/// <summary>
/// Sets or clears the route of one slot of a courier station in the acting player's city (<c>core-instructions/05-api-contract.md</c>,
/// <c>POST /api/v1/commands/set-route</c>; the rule is <see cref="CourierRules.SetRoute"/>, <c>04</c> sections 8 and 19). A new route stops
/// the slot's courier (its load goes back to where it came from); no stops clear the route. Goods only move, so it pays and refunds nothing
/// and writes no ledger row. The data is what the player chose: the walks and the loads come from the city and the content.
/// <see cref="CommandPipeline"/> runs it.
/// </summary>
/// <param name="RequestId">The client's id of the command, reused on every retry.</param>
/// <param name="StationId">The courier station.</param>
/// <param name="Slot">The slot, from 1.</param>
/// <param name="Stops">The stops in walking order; 0 to <see cref="MaxStops"/>.</param>
public sealed record SetRouteCommand(Guid RequestId, int StationId, int Slot, IReadOnlyList<CommandRouteStop?>? Stops) : ICityCommand
{
    /// <summary>The command type stored in the receipt and hashed into the payload: the last segment of the endpoint's path.</summary>
    public const string Type = "set-route";

    /// <summary>
    /// The most stops a <c>set-route</c> body may name (<c>05</c>): a fixed limit of the API above any content's <c>maxRouteStops</c>, so the
    /// boundary checks it without reading the content (owner directive OD-13); the content's own limit is the rule's <c>INVALID_ROUTE</c>.
    /// </summary>
    public const int MaxStops = 16;

    /// <summary>The most actions a stop may have in a <c>set-route</c> body (<c>05</c>); the content's <c>maxStopActions</c> is the rule's.</summary>
    public const int MaxStopActions = 8;

    /// <summary>The wire name of a take action.</summary>
    private const string _take = "take";

    /// <summary>The wire name of a put action.</summary>
    private const string _put = "put";

    /// <inheritdoc />
    public string CommandType => Type;

    /// <inheritdoc />
    public LedgerReason? CostReason => null;

    /// <inheritdoc />
    public LedgerReason? RefundReason => null;

    /// <summary>
    /// Refuses what no city and no content could make valid, before the city is locked (OD-13): a station id or slot below 1, no stop list,
    /// more than <see cref="MaxStops"/> stops, a missing stop, a building id below 1, no action list, 0 or more than
    /// <see cref="MaxStopActions"/> actions, a missing action, a kind other than <c>take</c> or <c>put</c>, an item id that can't be a content
    /// id, and an amount that is none of the three forms (<see cref="Amount.TryParse"/>). The rest is the rule's: the content's stop and
    /// action limits and two stops in a row at one building are <c>INVALID_ROUTE</c>, an unknown good <c>UNKNOWN_ITEM</c>.
    /// </summary>
    /// <returns><see langword="null"/>, or what is wrong, naming the first stop or action that is.</returns>
    public string? ValidateShape()
    {
        if (StationId < 1 || Slot < 1)
        {
            return "stationId and slot must be at least 1.";
        }

        if (Stops is null || Stops.Count > MaxStops)
        {
            return $"stops must list 0 to {MaxStops} stops.";
        }

        for (int stop = 0; stop < Stops.Count; stop++)
        {
            string? error = StopError(Stops[stop], stop + 1);
            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">A stop or action has no valid shape: the pipeline checks it first, so this is a programmer error.</exception>
    public MutationResult Apply(CityState city, GameContent content) =>
        CourierRules.SetRoute(city, content, StationId, Slot, [.. (Stops ?? []).Select(ToEngine)]);

    /// <summary>What is wrong with one stop, or <see langword="null"/>.</summary>
    /// <param name="stop">The stop.</param>
    /// <param name="number">Its number in the route, from 1, for the message.</param>
    /// <returns>The message, or <see langword="null"/>.</returns>
    private static string? StopError(CommandRouteStop? stop, int number)
    {
        if (stop is null)
        {
            return $"stops must hold stop objects {{ buildingId, actions }}, not null (stop {number}).";
        }

        if (stop.BuildingId < 1)
        {
            return $"stops must name buildings of at least 1 (stop {number}).";
        }

        if (stop.Actions is null || stop.Actions.Count == 0 || stop.Actions.Count > MaxStopActions)
        {
            return $"actions must list 1 to {MaxStopActions} actions (stop {number}).";
        }

        foreach (CommandRouteAction? action in stop.Actions)
        {
            if (action is null)
            {
                return $"actions must hold action objects {{ kind, itemId, amount }}, not null (stop {number}).";
            }

            if (action.Kind is not (_take or _put))
            {
                return $"kind must be \"take\" or \"put\" (stop {number}).";
            }

            if (!ContentIds.IsValid(action.ItemId))
            {
                return $"itemId must be a content id: {ContentIds.FormatDescription} (stop {number}).";
            }

            if (!Amount.TryParse(action.Amount, out _))
            {
                return $"amount must be a preset id, a percentage from \"1%\" to \"100%\", or a number of 1 to 12 digits without a leading zero (stop {number}).";
            }
        }

        return null;
    }

    /// <summary>A stop in the engine's form. Call it only after <see cref="ValidateShape"/> accepted the stops.</summary>
    /// <param name="stop">The stop, its shape checked.</param>
    /// <returns>The engine's stop.</returns>
    private static RouteStop ToEngine(CommandRouteStop? stop) =>
        stop?.Actions is null
            ? throw new InvalidOperationException("The route's shape was not checked before the command was applied.")
            : new RouteStop(stop.BuildingId, stop.Actions.Select(action => new RouteAction(
                action!.Kind == _take ? RouteActionKind.Take : RouteActionKind.Put,
                new ItemId(action.ItemId),
                Amount.TryParse(action.Amount, out Amount? amount)
                    ? amount
                    : throw new InvalidOperationException("The route's shape was not checked before the command was applied."))));
}
