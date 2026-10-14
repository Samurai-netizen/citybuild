using System.Text.Json;
using System.Text.Json.Serialization;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;

namespace CityBuilder.Infrastructure.Content;

/// <summary>
/// Reads a content file (core-instructions/03-architecture.md, content pipeline): System.Text.Json fills the engine's
/// <see cref="GameContentData"/>, and <see cref="GameContentFactory"/> checks it and builds the <see cref="GameContent"/>.
/// The reader is strict: field names are camelCase and case-sensitive, and an unknown field or a repeated key is an error,
/// so a typo can't silently drop a value.
/// </summary>
public static class ContentFileLoader
{
    private static readonly JsonSerializerOptions _options = CreateOptions();

    /// <summary>Reads, checks and builds the content in a file.</summary>
    /// <param name="path">The content file.</param>
    /// <returns>The validated content.</returns>
    /// <exception cref="ContentLoadException">
    /// The file can't be read or isn't valid JSON for the content (one error, from the first problem), or the content breaks rules
    /// (every error the factory found).
    /// </exception>
    public static GameContent Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        GameContentData? data;
        try
        {
            using FileStream stream = File.OpenRead(path);
            data = JsonSerializer.Deserialize<GameContentData>(stream, _options);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ContentLoadException(path, ["The file can't be read: " + exception.Message], exception);
        }
        catch (JsonException exception)
        {
            throw new ContentLoadException(path, ["The file is not valid content JSON: " + exception.Message], exception);
        }

        if (data is null)
        {
            throw new ContentLoadException(path, ["The file holds the JSON value null instead of a content object."]);
        }

        GameContentResult result = GameContentFactory.Create(data);
        if (result.Content is { } content)
        {
            return content;
        }

        throw new ContentLoadException(path, result.Errors.Select(error => error.ToString()).ToArray());
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            AllowDuplicateProperties = false,
            NumberHandling = JsonNumberHandling.Strict,
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
