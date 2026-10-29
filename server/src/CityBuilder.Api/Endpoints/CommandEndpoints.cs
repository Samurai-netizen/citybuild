using System.Text.Json;
using CityBuilder.Api.Contracts;
using CityBuilder.Application.Commands;
using CityBuilder.Application.Commands.AssignWorkers;
using CityBuilder.Application.Commands.PlaceBuilding;
using CityBuilder.Application.Commands.StartResearch;
using CityBuilder.Application.Commands.UpgradeBuilding;
using CityBuilder.Application.Content;
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
        RouteGroupBuilder commands = api.MapGroup("/commands");
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
    /// <param name="cancellationToken">The request's cancellation; nothing is committed then.</param>
    /// <returns>200 with the snapshot, or Problem Details.</returns>
    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> PlaceBuildingAsync(
        PlaceBuildingRequest? request,
        ICurrentPlayer player,
        CommandPipeline pipeline,
        IContentProvider content,
        IOptions<HttpJsonOptions> json,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return CommandResponses.Problem(CommandResponses.InvalidArgument("The body must be a JSON object."), Guid.Empty);
        }

        var command = new PlaceBuildingCommand(request.RequestId, request.BuildingTypeId ?? string.Empty, request.X, request.Y);
        return await ExecuteAsync(command, player, pipeline, content, json, cancellationToken);
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
    /// <param name="cancellationToken">The request's cancellation; nothing is committed then.</param>
    /// <returns>200 with the snapshot, or Problem Details.</returns>
    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> UpgradeBuildingAsync(
        UpgradeBuildingRequest? request,
        ICurrentPlayer player,
        CommandPipeline pipeline,
        IContentProvider content,
        IOptions<HttpJsonOptions> json,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return CommandResponses.Problem(CommandResponses.InvalidArgument("The body must be a JSON object."), Guid.Empty);
        }

        var command = new UpgradeBuildingCommand(request.RequestId, request.BuildingId);
        return await ExecuteAsync(command, player, pipeline, content, json, cancellationToken);
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
    /// <param name="cancellationToken">The request's cancellation; nothing is committed then.</param>
    /// <returns>200 with the snapshot, or Problem Details.</returns>
    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> AssignWorkersAsync(
        AssignWorkersRequest? request,
        ICurrentPlayer player,
        CommandPipeline pipeline,
        IContentProvider content,
        IOptions<HttpJsonOptions> json,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return CommandResponses.Problem(CommandResponses.InvalidArgument("The body must be a JSON object."), Guid.Empty);
        }

        var command = new AssignWorkersCommand(request.RequestId, request.BuildingId, request.Workers);
        return await ExecuteAsync(command, player, pipeline, content, json, cancellationToken);
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
    /// <param name="cancellationToken">The request's cancellation; nothing is committed then.</param>
    /// <returns>200 with the snapshot, or Problem Details.</returns>
    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> StartResearchAsync(
        StartResearchRequest? request,
        ICurrentPlayer player,
        CommandPipeline pipeline,
        IContentProvider content,
        IOptions<HttpJsonOptions> json,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return CommandResponses.Problem(CommandResponses.InvalidArgument("The body must be a JSON object."), Guid.Empty);
        }

        var command = new StartResearchCommand(request.RequestId, request.ResearchId ?? string.Empty);
        return await ExecuteAsync(command, player, pipeline, content, json, cancellationToken);
    }

    /// <summary>
    /// Runs a command through the pipeline for the token's player, with the snapshot as its response, and answers with what the pipeline did.
    /// The snapshot is written to text inside the transaction and stored in the receipt as it is, so a replay answers with the same bytes.
    /// </summary>
    /// <typeparam name="TCommand">The command's type; one helper serves every command endpoint.</typeparam>
    /// <param name="command">The command.</param>
    /// <param name="player">The acting player, from the access token.</param>
    /// <param name="pipeline">Runs the command.</param>
    /// <param name="content">The server's content, for the snapshot.</param>
    /// <param name="json">The HTTP JSON settings.</param>
    /// <param name="cancellationToken">The request's cancellation.</param>
    /// <returns>200 with the snapshot, or Problem Details.</returns>
    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> ExecuteAsync<TCommand>(
        TCommand command,
        ICurrentPlayer player,
        CommandPipeline pipeline,
        IContentProvider content,
        IOptions<HttpJsonOptions> json,
        CancellationToken cancellationToken)
        where TCommand : ICityCommand
    {
        JsonSerializerOptions options = json.Value.SerializerOptions;
        CommandResult result = await pipeline.ExecuteAsync(
            player.PlayerId,
            command,
            success => JsonSerializer.Serialize(
                CitySnapshotMapper.ToSnapshot(
                    success.RequestId, success.City, success.Settlement, success.StateVersion, success.ServerTime, content.Content),
                options),
            cancellationToken);
        return CommandResponses.ToHttpResult(result, command.RequestId);
    }
}
