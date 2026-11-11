using System.Diagnostics;
using System.Text.Json;
using CityBuilder.Api.Contracts;
using CityBuilder.Api.Errors;
using CityBuilder.Api.Http;
using CityBuilder.Application.Commands;
using CityBuilder.Application.Commands.AssignWorkers;
using CityBuilder.Application.Commands.CancelDelivery;
using CityBuilder.Application.Commands.PlaceBuilding;
using CityBuilder.Application.Commands.ReenergizeCouriers;
using CityBuilder.Application.Commands.Roads;
using CityBuilder.Application.Commands.SetRoute;
using CityBuilder.Application.Commands.StartResearch;
using CityBuilder.Application.Commands.StartTrip;
using CityBuilder.Application.Commands.UpgradeBuilding;
using CityBuilder.Application.Content;
using CityBuilder.Application.Persistence;
using CityBuilder.Application.Players;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace CityBuilder.Api.Endpoints;

/// <summary>
/// The command endpoints under <c>/api/v1/commands</c> (core-instructions/05-api-contract.md). Each one only turns its body into an
/// <see cref="ICityCommand"/> and hands it to <see cref="CommandPipeline"/>, which locks, settles, applies and stores it (ADR-0003, ADR-0004);
/// none writes anything itself.
/// </summary>
public static class CommandEndpoints
{
    /// <summary>
    /// Maps the commands on a <c>/commands</c> group of the <c>/api/v1</c> group, which requires an access token. Each declares its 200 body,
    /// the snapshot, for the OpenAPI document: the handler writes it as text, which carries no schema of its own.
    /// </summary>
    /// <param name="api">The <c>/api/v1</c> route group.</param>
    /// <returns>The <c>/commands</c> group.</returns>
    public static RouteGroupBuilder MapCommands(this RouteGroupBuilder api)
    {
        RouteGroupBuilder commands = api.MapGroup("/commands").RequireRateLimit(RateLimitClass.Commands);
        commands.MapPost("/place-building", PlaceBuildingAsync).Produces<CitySnapshot>(StatusCodes.Status200OK, "application/json");
        commands.MapPost("/upgrade-building", UpgradeBuildingAsync).Produces<CitySnapshot>(StatusCodes.Status200OK, "application/json");
        commands.MapPost("/assign-workers", AssignWorkersAsync).Produces<CitySnapshot>(StatusCodes.Status200OK, "application/json");
        commands.MapPost("/start-research", StartResearchAsync).Produces<CitySnapshot>(StatusCodes.Status200OK, "application/json");
        commands.MapPost("/place-roads", PlaceRoadsAsync).Produces<CitySnapshot>(StatusCodes.Status200OK, "application/json");
        commands.MapPost("/upgrade-roads", UpgradeRoadsAsync).Produces<CitySnapshot>(StatusCodes.Status200OK, "application/json");
        commands.MapPost("/remove-roads", RemoveRoadsAsync).Produces<CitySnapshot>(StatusCodes.Status200OK, "application/json");
        commands.MapPost("/start-trip", StartTripAsync).Produces<CitySnapshot>(StatusCodes.Status200OK, "application/json");
        commands.MapPost("/set-route", SetRouteAsync).Produces<CitySnapshot>(StatusCodes.Status200OK, "application/json");
        commands.MapPost("/reenergize-couriers", ReenergizeCouriersAsync).Produces<CitySnapshot>(StatusCodes.Status200OK, "application/json");
        commands.MapPost("/cancel-delivery", CancelDeliveryAsync).Produces<CitySnapshot>(StatusCodes.Status200OK, "application/json");
        return commands;
    }

    /// <summary>
    /// Places a building in the acting player's city (<see cref="PlaceBuildingCommand"/>). The body names only the type and the tile; a body
    /// with an unknown property, a missing field or a value of the wrong type is refused by the binding (400) before anything runs.
    /// </summary>
    /// <param name="request">The body, or <see langword="null"/> when there is none.</param>
    /// <param name="player">The acting player, from the access token.</param>
    /// <param name="pipeline">Runs the command.</param>
    /// <param name="content">The server's content, for the snapshot.</param>
    /// <param name="json">The HTTP JSON settings, so the snapshot reads as <c>GET /city</c>'s.</param>
    /// <param name="loggers">Gives the logger of the command lines.</param>
    /// <param name="cancellationToken">The request's cancellation; nothing is committed then.</param>
    /// <returns>200 with the snapshot, or Problem Details.</returns>
    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> PlaceBuildingAsync(
        PlaceBuildingRequest? request,
        ICurrentPlayer player,
        CommandPipeline pipeline,
        IContentProvider content,
        IOptions<HttpJsonOptions> json,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ApiProblem.Of(ErrorCatalogue.InvalidArgument.WithDetail("The body must be a JSON object."));
        }

        var command = new PlaceBuildingCommand(request.RequestId, request.BuildingTypeId ?? string.Empty, request.X, request.Y);
        return await ExecuteAsync(command, player, pipeline, content, json, loggers, cancellationToken);
    }

    /// <summary>
    /// Starts upgrading a building of the acting player's city (<see cref="UpgradeBuildingCommand"/>). The body names only the building; a
    /// body with an unknown property, a missing field or a value of the wrong type is refused by the binding (400) before anything runs.
    /// </summary>
    /// <param name="request">The body, or <see langword="null"/> when there is none.</param>
    /// <param name="player">The acting player, from the access token.</param>
    /// <param name="pipeline">Runs the command.</param>
    /// <param name="content">The server's content, for the snapshot.</param>
    /// <param name="json">The HTTP JSON settings, so the snapshot reads as <c>GET /city</c>'s.</param>
    /// <param name="loggers">Gives the logger of the command lines.</param>
    /// <param name="cancellationToken">The request's cancellation; nothing is committed then.</param>
    /// <returns>200 with the snapshot, or Problem Details.</returns>
    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> UpgradeBuildingAsync(
        UpgradeBuildingRequest? request,
        ICurrentPlayer player,
        CommandPipeline pipeline,
        IContentProvider content,
        IOptions<HttpJsonOptions> json,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ApiProblem.Of(ErrorCatalogue.InvalidArgument.WithDetail("The body must be a JSON object."));
        }

        var command = new UpgradeBuildingCommand(request.RequestId, request.BuildingId);
        return await ExecuteAsync(command, player, pipeline, content, json, loggers, cancellationToken);
    }

    /// <summary>
    /// Sets the worker count of a producer of the acting player's city (<see cref="AssignWorkersCommand"/>). The body names only the building
    /// and the count; a body with an unknown property, a missing field or a value of the wrong type is refused by the binding (400) before
    /// anything runs.
    /// </summary>
    /// <param name="request">The body, or <see langword="null"/> when there is none.</param>
    /// <param name="player">The acting player, from the access token.</param>
    /// <param name="pipeline">Runs the command.</param>
    /// <param name="content">The server's content, for the snapshot.</param>
    /// <param name="json">The HTTP JSON settings, so the snapshot reads as <c>GET /city</c>'s.</param>
    /// <param name="loggers">Gives the logger of the command lines.</param>
    /// <param name="cancellationToken">The request's cancellation; nothing is committed then.</param>
    /// <returns>200 with the snapshot, or Problem Details.</returns>
    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> AssignWorkersAsync(
        AssignWorkersRequest? request,
        ICurrentPlayer player,
        CommandPipeline pipeline,
        IContentProvider content,
        IOptions<HttpJsonOptions> json,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ApiProblem.Of(ErrorCatalogue.InvalidArgument.WithDetail("The body must be a JSON object."));
        }

        var command = new AssignWorkersCommand(request.RequestId, request.BuildingId, request.Workers);
        return await ExecuteAsync(command, player, pipeline, content, json, loggers, cancellationToken);
    }

    /// <summary>
    /// Starts a research node in the acting player's city (<see cref="StartResearchCommand"/>). The body names only the node; a body with an
    /// unknown property, a missing field or a value of the wrong type is refused by the binding (400) before anything runs.
    /// </summary>
    /// <param name="request">The body, or <see langword="null"/> when there is none.</param>
    /// <param name="player">The acting player, from the access token.</param>
    /// <param name="pipeline">Runs the command.</param>
    /// <param name="content">The server's content, for the snapshot.</param>
    /// <param name="json">The HTTP JSON settings, so the snapshot reads as <c>GET /city</c>'s.</param>
    /// <param name="loggers">Gives the logger of the command lines.</param>
    /// <param name="cancellationToken">The request's cancellation; nothing is committed then.</param>
    /// <returns>200 with the snapshot, or Problem Details.</returns>
    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> StartResearchAsync(
        StartResearchRequest? request,
        ICurrentPlayer player,
        CommandPipeline pipeline,
        IContentProvider content,
        IOptions<HttpJsonOptions> json,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ApiProblem.Of(ErrorCatalogue.InvalidArgument.WithDetail("The body must be a JSON object."));
        }

        var command = new StartResearchCommand(request.RequestId, request.ResearchId ?? string.Empty);
        return await ExecuteAsync(command, player, pipeline, content, json, loggers, cancellationToken);
    }

    /// <summary>
    /// Sends a free worker with goods from one building's store to another in the acting player's city (<see cref="StartTripCommand"/>,
    /// CR-0008). The body names only the two buildings, the good and the amount; a body with an unknown property, a missing field or a value of
    /// the wrong type is refused by the binding (400) before anything runs.
    /// </summary>
    /// <param name="request">The body, or <see langword="null"/> when there is none.</param>
    /// <param name="player">The acting player, from the access token.</param>
    /// <param name="pipeline">Runs the command.</param>
    /// <param name="content">The server's content, for the snapshot.</param>
    /// <param name="json">The HTTP JSON settings, so the snapshot reads as <c>GET /city</c>'s.</param>
    /// <param name="loggers">Gives the logger of the command lines.</param>
    /// <param name="cancellationToken">The request's cancellation; nothing is committed then.</param>
    /// <returns>200 with the snapshot, or Problem Details.</returns>
    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> StartTripAsync(
        StartTripRequest? request,
        ICurrentPlayer player,
        CommandPipeline pipeline,
        IContentProvider content,
        IOptions<HttpJsonOptions> json,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ApiProblem.Of(ErrorCatalogue.InvalidArgument.WithDetail("The body must be a JSON object."));
        }

        var command = new StartTripCommand(
            request.RequestId, request.SourceId, request.TargetId, request.ItemId ?? string.Empty, request.Amount ?? string.Empty);
        return await ExecuteAsync(command, player, pipeline, content, json, loggers, cancellationToken);
    }

    /// <summary>
    /// Sets or clears the route of a courier station's slot in the acting player's city (<see cref="SetRouteCommand"/>, CR-0008). The body
    /// names only the station, the slot and the stops; a body with an unknown property, a missing field or a value of the wrong type is
    /// refused by the binding (400) before anything runs.
    /// </summary>
    /// <param name="request">The body, or <see langword="null"/> when there is none.</param>
    /// <param name="player">The acting player, from the access token.</param>
    /// <param name="pipeline">Runs the command.</param>
    /// <param name="content">The server's content, for the snapshot.</param>
    /// <param name="json">The HTTP JSON settings, so the snapshot reads as <c>GET /city</c>'s.</param>
    /// <param name="loggers">Gives the logger of the command lines.</param>
    /// <param name="cancellationToken">The request's cancellation; nothing is committed then.</param>
    /// <returns>200 with the snapshot, or Problem Details.</returns>
    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> SetRouteAsync(
        SetRouteRequest? request,
        ICurrentPlayer player,
        CommandPipeline pipeline,
        IContentProvider content,
        IOptions<HttpJsonOptions> json,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ApiProblem.Of(ErrorCatalogue.InvalidArgument.WithDetail("The body must be a JSON object."));
        }

        var command = new SetRouteCommand(request.RequestId, request.StationId, request.Slot, ToCommandStops(request.Stops));
        return await ExecuteAsync(command, player, pipeline, content, json, loggers, cancellationToken);
    }

    /// <summary>
    /// Cancels one delivery line of a courier, or all of its lines and its route, in the acting player's city (<see cref="CancelDeliveryCommand"/>,
    /// CR-0008 amendment 1). The body names only the station, the slot and the line (or <c>null</c>); a body with an unknown property, a
    /// missing field or a value of the wrong type is refused by the binding (400) before anything runs.
    /// </summary>
    /// <param name="request">The body, or <see langword="null"/> when there is none.</param>
    /// <param name="player">The acting player, from the access token.</param>
    /// <param name="pipeline">Runs the command.</param>
    /// <param name="content">The server's content, for the snapshot.</param>
    /// <param name="json">The HTTP JSON settings, so the snapshot reads as <c>GET /city</c>'s.</param>
    /// <param name="loggers">Gives the logger of the command lines.</param>
    /// <param name="cancellationToken">The request's cancellation; nothing is committed then.</param>
    /// <returns>200 with the snapshot, or Problem Details.</returns>
    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> CancelDeliveryAsync(
        CancelDeliveryRequest? request,
        ICurrentPlayer player,
        CommandPipeline pipeline,
        IContentProvider content,
        IOptions<HttpJsonOptions> json,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ApiProblem.Of(ErrorCatalogue.InvalidArgument.WithDetail("The body must be a JSON object."));
        }

        var command = new CancelDeliveryCommand(request.RequestId, request.StationId, request.Slot, request.LineId);
        return await ExecuteAsync(command, player, pipeline, content, json, loggers, cancellationToken);
    }

    /// <summary>
    /// Re-energizes every courier of the acting player's city (<see cref="ReenergizeCouriersCommand"/>, CR-0008). The body names only the
    /// request id: intent, no stamina; a body with an unknown property, a missing field or a value of the wrong type is refused by the
    /// binding (400) before anything runs.
    /// </summary>
    /// <param name="request">The body, or <see langword="null"/> when there is none.</param>
    /// <param name="player">The acting player, from the access token.</param>
    /// <param name="pipeline">Runs the command.</param>
    /// <param name="content">The server's content, for the snapshot.</param>
    /// <param name="json">The HTTP JSON settings, so the snapshot reads as <c>GET /city</c>'s.</param>
    /// <param name="loggers">Gives the logger of the command lines.</param>
    /// <param name="cancellationToken">The request's cancellation; nothing is committed then.</param>
    /// <returns>200 with the snapshot, or Problem Details.</returns>
    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> ReenergizeCouriersAsync(
        ReenergizeCouriersRequest? request,
        ICurrentPlayer player,
        CommandPipeline pipeline,
        IContentProvider content,
        IOptions<HttpJsonOptions> json,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ApiProblem.Of(ErrorCatalogue.InvalidArgument.WithDetail("The body must be a JSON object."));
        }

        return await ExecuteAsync(new ReenergizeCouriersCommand(request.RequestId), player, pipeline, content, json, loggers, cancellationToken);
    }

    /// <summary>
    /// Builds level-1 roads on tiles of the acting player's city (<see cref="PlaceRoadsCommand"/>, CR-0008). The body names only the tiles; a
    /// body with an unknown property, a missing field or a value of the wrong type is refused by the binding (400) before anything runs.
    /// </summary>
    /// <param name="request">The body, or <see langword="null"/> when there is none.</param>
    /// <param name="player">The acting player, from the access token.</param>
    /// <param name="pipeline">Runs the command.</param>
    /// <param name="content">The server's content, for the snapshot.</param>
    /// <param name="json">The HTTP JSON settings, so the snapshot reads as <c>GET /city</c>'s.</param>
    /// <param name="loggers">Gives the logger of the command lines.</param>
    /// <param name="cancellationToken">The request's cancellation; nothing is committed then.</param>
    /// <returns>200 with the snapshot, or Problem Details.</returns>
    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> PlaceRoadsAsync(
        RoadTilesRequest? request,
        ICurrentPlayer player,
        CommandPipeline pipeline,
        IContentProvider content,
        IOptions<HttpJsonOptions> json,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ApiProblem.Of(ErrorCatalogue.InvalidArgument.WithDetail("The body must be a JSON object."));
        }

        var command = new PlaceRoadsCommand(request.RequestId, ToCommandTiles(request.Tiles));
        return await ExecuteAsync(command, player, pipeline, content, json, loggers, cancellationToken);
    }

    /// <summary>
    /// Raises roads of the acting player's city one level (<see cref="UpgradeRoadsCommand"/>, CR-0008). The body names only the tiles; a body
    /// with an unknown property, a missing field or a value of the wrong type is refused by the binding (400) before anything runs.
    /// </summary>
    /// <param name="request">The body, or <see langword="null"/> when there is none.</param>
    /// <param name="player">The acting player, from the access token.</param>
    /// <param name="pipeline">Runs the command.</param>
    /// <param name="content">The server's content, for the snapshot.</param>
    /// <param name="json">The HTTP JSON settings, so the snapshot reads as <c>GET /city</c>'s.</param>
    /// <param name="loggers">Gives the logger of the command lines.</param>
    /// <param name="cancellationToken">The request's cancellation; nothing is committed then.</param>
    /// <returns>200 with the snapshot, or Problem Details.</returns>
    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> UpgradeRoadsAsync(
        RoadTilesRequest? request,
        ICurrentPlayer player,
        CommandPipeline pipeline,
        IContentProvider content,
        IOptions<HttpJsonOptions> json,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ApiProblem.Of(ErrorCatalogue.InvalidArgument.WithDetail("The body must be a JSON object."));
        }

        var command = new UpgradeRoadsCommand(request.RequestId, ToCommandTiles(request.Tiles));
        return await ExecuteAsync(command, player, pipeline, content, json, loggers, cancellationToken);
    }

    /// <summary>
    /// Takes roads off tiles of the acting player's city (<see cref="RemoveRoadsCommand"/>, CR-0008). The body names only the tiles; a body
    /// with an unknown property, a missing field or a value of the wrong type is refused by the binding (400) before anything runs.
    /// </summary>
    /// <param name="request">The body, or <see langword="null"/> when there is none.</param>
    /// <param name="player">The acting player, from the access token.</param>
    /// <param name="pipeline">Runs the command.</param>
    /// <param name="content">The server's content, for the snapshot.</param>
    /// <param name="json">The HTTP JSON settings, so the snapshot reads as <c>GET /city</c>'s.</param>
    /// <param name="loggers">Gives the logger of the command lines.</param>
    /// <param name="cancellationToken">The request's cancellation; nothing is committed then.</param>
    /// <returns>200 with the snapshot, or Problem Details.</returns>
    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> RemoveRoadsAsync(
        RoadTilesRequest? request,
        ICurrentPlayer player,
        CommandPipeline pipeline,
        IContentProvider content,
        IOptions<HttpJsonOptions> json,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ApiProblem.Of(ErrorCatalogue.InvalidArgument.WithDetail("The body must be a JSON object."));
        }

        var command = new RemoveRoadsCommand(request.RequestId, ToCommandTiles(request.Tiles));
        return await ExecuteAsync(command, player, pipeline, content, json, loggers, cancellationToken);
    }

    /// <summary>
    /// The body's tiles as the command's, one for one: a JSON <c>null</c> tile stays null and a missing list becomes an empty one, so the
    /// command's shape check refuses both (<c>INVALID_ARGUMENT</c>) before the city is locked.
    /// </summary>
    /// <param name="tiles">The body's <c>tiles</c>.</param>
    /// <returns>The command's tiles.</returns>
    private static CommandTile?[] ToCommandTiles(IReadOnlyList<TileRequest?>? tiles) =>
        tiles is null ? [] : [.. tiles.Select(tile => tile is null ? null : new CommandTile(tile.X, tile.Y))];

    /// <summary>
    /// The body's stops as the command's, keeping a missing list, stop or action as null so the command's shape check names it (a missing
    /// list is not an empty one: only <c>[]</c> clears a route).
    /// </summary>
    /// <param name="stops">The body's <c>stops</c>.</param>
    /// <returns>The command's stops, or null.</returns>
    private static CommandRouteStop?[]? ToCommandStops(IReadOnlyList<RouteStopRequest?>? stops) =>
        stops is null
            ? null
            : [.. stops.Select(stop => stop is null
                ? null
                : new CommandRouteStop(
                    stop.BuildingId,
                    stop.Actions is null
                        ? null
                        : [.. stop.Actions.Select(action => action is null
                            ? null
                            : new CommandRouteAction(action.Kind ?? string.Empty, action.ItemId ?? string.Empty, action.Amount ?? string.Empty))]))];

    /// <summary>
    /// Runs a command through the pipeline for the token's player, with the snapshot as its response, and answers with what the pipeline did.
    /// The snapshot is written to text inside the transaction and stored in the receipt as it is, so a replay answers with the same bytes. A
    /// lock timeout or a changed city (<see cref="ConcurrencyConflictException"/>) is <c>CONCURRENCY_CONFLICT</c> with the command's request id.
    /// Every way it ends writes one line (<see cref="CommandLog"/>), and a command that settled the city first writes the settlement's line
    /// before it (<see cref="SettlementLog"/>), all with the player and the real time the request took.
    /// </summary>
    /// <typeparam name="TCommand">The command's type; one helper serves every command endpoint.</typeparam>
    /// <param name="command">The command.</param>
    /// <param name="player">The acting player, from the access token.</param>
    /// <param name="pipeline">Runs the command.</param>
    /// <param name="content">The server's content, for the snapshot.</param>
    /// <param name="json">The HTTP JSON settings.</param>
    /// <param name="loggers">Gives the logger of the command lines.</param>
    /// <param name="cancellationToken">The request's cancellation.</param>
    /// <returns>200 with the snapshot, or Problem Details.</returns>
    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> ExecuteAsync<TCommand>(
        TCommand command,
        ICurrentPlayer player,
        CommandPipeline pipeline,
        IContentProvider content,
        IOptions<HttpJsonOptions> json,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
        where TCommand : ICityCommand
    {
        long startedAt = Stopwatch.GetTimestamp();
        ILogger logger = loggers.CreateLogger(typeof(CommandEndpoints).FullName!);
        Guid playerId = player.PlayerId;
        JsonSerializerOptions options = json.Value.SerializerOptions;
        try
        {
            CommandResult result = await pipeline.ExecuteAsync(
                playerId,
                command,
                success => JsonSerializer.Serialize(
                    CitySnapshotMapper.ToSnapshot(
                        success.RequestId, success.City, success.Settlement, success.StateVersion, success.ServerTime, content.Content),
                    options),
                cancellationToken);
            Log(logger, command, playerId, result, RequestDuration.MillisecondsSince(startedAt));
            return CommandResponses.ToHttpResult(result, command.RequestId);
        }
        catch (ConcurrencyConflictException)
        {
            // Another request had the city, or changed it (06): nothing was committed, so the client retries with the same request id, which
            // the answer repeats. Every other exception is the global handler's (ApiExceptionHandler).
            CommandLog.Conflict(logger, command.CommandType, command.RequestId, playerId, RequestDuration.MillisecondsSince(startedAt));
            return ApiProblem.Of(ErrorCatalogue.ConcurrencyConflict, command.RequestId);
        }
    }

    /// <summary>Writes the lines of a command the pipeline ran: the settlement's, if it settled the city, then how the command ended.</summary>
    /// <param name="logger">The logger of the command endpoints.</param>
    /// <param name="command">The command.</param>
    /// <param name="playerId">The acting player.</param>
    /// <param name="result">What the pipeline did.</param>
    /// <param name="durationMs">How long the request took, in milliseconds.</param>
    private static void Log(ILogger logger, ICityCommand command, Guid playerId, CommandResult result, double durationMs)
    {
        if (result.Settlement is { } settlement)
        {
            SettlementLog.Write(logger, playerId, command.CommandType, settlement, durationMs);
        }

        switch (result.Outcome)
        {
            case CommandOutcome.Succeeded:
                CommandLog.Accepted(logger, command.CommandType, command.RequestId, playerId, durationMs);
                break;
            case CommandOutcome.Replayed:
                CommandLog.Replayed(logger, command.CommandType, command.RequestId, playerId, durationMs);
                break;
            default:
                string code = CommandResponses.ErrorOf(result)!.Code;
                CommandLog.Rejected(logger, command.CommandType, command.RequestId, playerId, code, durationMs);
                break;
        }
    }
}
