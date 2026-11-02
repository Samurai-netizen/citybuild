using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace CityBuilder.IntegrationTests.Security;

/// <summary>
/// The inventory of what a client can send (core-instructions/07-security-model.md → What the client sends, M0-60): for every endpoint of
/// <c>/api/v1</c>, every value its handler binds from the request (body properties, route, query, header, form), read from the framework's
/// own description of the binding, equals the table in <c>07</c>: a request id and the player's intent, the dev login's name, and nothing
/// else. A later endpoint or field that binds a cost, a time, a player id or any other value from the client fails here until <c>07</c> and
/// this table say why. The token and the client version header are read by middleware, not by a handler (their tests are in <c>Auth</c> and
/// <c>Logging</c>). In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server (Development, no reachable database).</param>
public sealed class ClientInputInventoryTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>
    /// Every endpoint binds exactly the values of the inventory: each as <c>source:name</c>, a body property by its JSON name. A GET binds
    /// nothing; a command binds its request id and its intent from the body.
    /// </summary>
    [Fact]
    public void Security_EveryEndpoint_BindsOnlyTheInventorysValues()
    {
        var expected = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["POST /api/v1/auth/dev-login"] = ["Body:displayName"],
            ["GET /api/v1/city"] = [],
            ["GET /api/v1/content"] = [],
            ["POST /api/v1/commands/place-building"] = ["Body:requestId", "Body:buildingTypeId", "Body:x", "Body:y"],
            ["POST /api/v1/commands/upgrade-building"] = ["Body:requestId", "Body:buildingId"],
            ["POST /api/v1/commands/assign-workers"] = ["Body:requestId", "Body:buildingId", "Body:workers"],
            ["POST /api/v1/commands/start-research"] = ["Body:requestId", "Body:researchId"],
        };
        using WebApplicationFactory<Program> server = factory.WithWebHostBuilder(_ => { });
        JsonSerializerOptions json = server.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

        Dictionary<string, string[]> actual = server.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items
            .SelectMany(group => group.Items)
            .Where(description => description.RelativePath!.StartsWith("api/v1/", StringComparison.Ordinal))
            .ToDictionary(description => description.HttpMethod + " /" + description.RelativePath, description => BoundValues(description, json));

        Assert.Equal(Sorted(expected), Sorted(actual));
    }

    /// <summary>
    /// The inventory covers every routed endpoint of <c>/api/v1</c>: an endpoint the framework's description left out (and so the test above
    /// could not see) fails here.
    /// </summary>
    [Fact]
    public void Security_EveryApiEndpoint_IsInTheDescription()
    {
        using WebApplicationFactory<Program> server = factory.WithWebHostBuilder(_ => { });

        string[] routed = [.. server.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .Where(path => path.StartsWith("/api/v1/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];
        string[] described = [.. server.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items
            .SelectMany(group => group.Items)
            .Select(description => "/" + description.RelativePath)
            .Where(path => path.StartsWith("/api/v1/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];

        Assert.Equal(7, routed.Length);
        Assert.Equal(routed, described);
    }

    /// <summary>
    /// The values one endpoint binds from the request: a body parameter is expanded into the JSON names of its type's properties (what the
    /// strict binder reads, unknown ones being refused); any other parameter the framework binds from the request is listed by its source and
    /// name. Services, the cancellation token and the player (a service read from the token) are not from the request and are not listed.
    /// </summary>
    /// <param name="description">The framework's description of the endpoint.</param>
    /// <param name="json">The server's JSON options, which name the body's properties.</param>
    /// <returns>The values as <c>source:name</c>, in the order the framework and the type give them.</returns>
    private static string[] BoundValues(ApiDescription description, JsonSerializerOptions json) =>
        [.. description.ParameterDescriptions
            .Where(parameter => parameter.Source != BindingSource.Services && parameter.Source != BindingSource.Special)
            .SelectMany(parameter => parameter.Source == BindingSource.Body
                ? json.GetTypeInfo(parameter.Type).Properties.Select(property => "Body:" + property.Name)
                : [parameter.Source.Id + ":" + parameter.Name])];

    /// <summary>An inventory in a fixed order, so two of them compare as equal when they hold the same values.</summary>
    /// <param name="inventory">Endpoints and their values.</param>
    /// <returns>The endpoints by name, each with its values sorted.</returns>
    private static List<KeyValuePair<string, string>> Sorted(Dictionary<string, string[]> inventory) =>
        [.. inventory
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new KeyValuePair<string, string>(pair.Key, string.Join(", ", pair.Value.Order(StringComparer.Ordinal))))];
}
