using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>Runs the factory and checks its result, printing every error when a check fails.</summary>
internal static class ContentAssert
{
    /// <summary>The data is valid; returns the content.</summary>
    public static GameContent Valid(GameContentData data)
    {
        GameContentResult result = GameContentFactory.Create(data);

        Assert.True(result.IsValid, "Expected valid content, got:\n" + Describe(result.Errors));
        Assert.Empty(result.Errors);
        Assert.NotNull(result.Content);
        return result.Content;
    }

    /// <summary>The data has exactly one error, with this code at this path, and no content comes out.</summary>
    public static void SingleError(GameContentData data, ContentErrorCode code, string path)
    {
        GameContentResult result = GameContentFactory.Create(data);

        Assert.False(result.IsValid);
        Assert.Null(result.Content);
        Assert.True(
            result.Errors.Count == 1 && result.Errors[0].Code == code && result.Errors[0].Path == path,
            $"Expected exactly {code} at {path}, got:\n" + Describe(result.Errors));
        Assert.False(string.IsNullOrWhiteSpace(result.Errors[0].Message));
    }

    /// <summary>The data has errors, exactly these codes and paths in this order.</summary>
    public static void Errors(GameContentData data, params (ContentErrorCode Code, string Path)[] expected)
    {
        GameContentResult result = GameContentFactory.Create(data);

        Assert.False(result.IsValid);
        Assert.Null(result.Content);
        Assert.True(
            result.Errors.Select(error => (error.Code, error.Path)).SequenceEqual(expected),
            "Expected:\n" + string.Join("\n", expected.Select(error => $"{error.Code} at {error.Path}")) + "\nGot:\n" + Describe(result.Errors));
    }

    private static string Describe(IReadOnlyList<ContentError> errors) =>
        errors.Count == 0 ? "(no errors)" : string.Join("\n", errors.Select(error => error.ToString()));
}
