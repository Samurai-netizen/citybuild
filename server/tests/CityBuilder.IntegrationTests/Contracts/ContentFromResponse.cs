using System.Text.Json;
using System.Text.Json.Nodes;
using CityBuilder.Api.Contracts;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;

namespace CityBuilder.IntegrationTests.Contracts;

/// <summary>
/// What the client does with <c>GET /api/v1/content</c> (M0-38, <c>core-instructions/09-unity-client.md</c>): reads the JSON into the engine's
/// <see cref="GameContentData"/> and builds a <see cref="GameContent"/> through the same validating factory the server uses. The tests use it
/// to prove the response holds everything the factory needs, so a field dropped from the contract is found here and not in the client.
/// </summary>
internal static class ContentFromResponse
{
    /// <summary>
    /// Builds the content from a response body. The one adaptation is the building <c>kind</c>: the response names it <c>House</c> or
    /// <c>Producer</c>, the content file <c>house</c> or <c>producer</c>; extra properties (<c>unlockedBy</c>, <c>timeScale</c>) are ignored.
    /// </summary>
    /// <param name="body">The response text.</param>
    /// <returns>The content, validated by the factory.</returns>
    public static GameContent Rebuild(string body)
    {
        JsonNode document = JsonNode.Parse(body)!;
        foreach (JsonNode? building in document["buildings"]!.AsArray())
        {
            building!["kind"] = ((string)building["kind"]!).ToLowerInvariant();
        }

        GameContentData data = document.Deserialize<GameContentData>(JsonSerializerOptions.Web)!;
        GameContentResult result = GameContentFactory.Create(data);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(error => error.ToString())));
        return result.Content!;
    }

    /// <summary>
    /// The response text for the content as a test compares it: the mapper's response written with the web defaults and read back as a tree,
    /// so the result is spelled like <see cref="Normalize"/> spells a received body.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <returns>The JSON text, with the time scale of a server that doesn't scale its clock.</returns>
    public static string ResponseOf(GameContent content) =>
        Normalize(JsonSerializer.Serialize(ContentResponseMapper.ToResponse(content, timeScale: 1), JsonSerializerOptions.Web));

    /// <summary>
    /// Writes JSON text again with one encoder, so two texts that differ only in how they escape characters (the server writes
    /// <c>Woodcutter's</c>, the serializer's defaults write <c>Woodcutter\u0027s</c>) compare equal; the order of properties stays.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The same document, compact, in the tree writer's spelling.</returns>
    public static string Normalize(string json) => JsonNode.Parse(json)!.ToJsonString();
}
