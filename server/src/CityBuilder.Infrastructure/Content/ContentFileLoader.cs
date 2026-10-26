using System.Globalization;
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
    /// <summary>The strict reader settings, made once and frozen: thread-safe to share between loads (<see cref="CreateOptions"/>).</summary>
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
            throw new ContentLoadException(path, ["The file is not valid content JSON: " + DescribeWithPlace(exception)], exception);
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

    /// <summary>
    /// The message of a JSON error with its place in the file. Most System.Text.Json messages end with
    /// "Path: … | LineNumber: … | BytePositionInLine: …", but some leave the place out (an unknown field, for example); those get it from the
    /// exception's properties, in the same format. Line and position count from 0, as in System.Text.Json's own messages.
    /// </summary>
    /// <param name="exception">The reader's error.</param>
    private static string DescribeWithPlace(JsonException exception)
    {
        if (exception.Path is null || exception.Message.Contains("Path: ", StringComparison.Ordinal))
        {
            return exception.Message;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{exception.Message} Path: {exception.Path} | LineNumber: {exception.LineNumber} | BytePositionInLine: {exception.BytePositionInLine}.");
    }

    /// <summary>
    /// The settings that make the reader strict: camelCase names compared case-sensitively, an unknown field, a repeated key or a number
    /// written as text is an error, so a typo in the content can't silently drop or change a value (decision 028). Read-only once made.
    /// </summary>
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
