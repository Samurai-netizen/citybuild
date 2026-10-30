using System.Diagnostics;
using System.Text.Json;
using CityBuilder.Api.Contracts;
using CityBuilder.Api.Errors;
using CityBuilder.Api.Http;
using CityBuilder.Application.Commands;
using CityBuilder.Application.Commands.AssignWorkers;
using CityBuilder.Application.Commands.PlaceBuilding;
using CityBuilder.Application.Commands.StartResearch;
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
