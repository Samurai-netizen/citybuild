using System.Text.Json.Nodes;
using CityBuilder.Infrastructure.Content;
using CityBuilder.Simulation.Content;

namespace CityBuilder.IntegrationTests.Content;

/// <summary>
/// The content file loader (core-instructions/03-architecture.md, content pipeline); runs in memory, no Docker. Each broken file is the real
/// <c>mvp-content.json</c> with one change, so no game number is copied into a test (core-instructions/24, ownership of numbers).
/// </summary>
public sealed class ContentFileLoaderTests : IDisposable
{
    private readonly ContentFileDirectory _files = new();

    public void Dispose() => _files.Dispose();

    [Fact]
    public void Load_RealMvpContent_ReturnsValidContent()
    {
        // The real server/content/mvp-content.json, as the Api build copies it next to the app. A rule error fails here with every
        // error in the message (ContentLoadException), so a broken content change names what to fix.
        GameContent content = ContentFileLoader.Load(ContentFiles.RealMvpContentPath);

        Assert.False(string.IsNullOrEmpty(content.ContentVersion));
    }

    [Fact]
    public void Load_MissingFile_ThrowsOneReadError()
    {
        string path = Path.Combine(_files.Directory, "missing.json");

        ContentLoadException exception = Assert.Throws<ContentLoadException>(() => ContentFileLoader.Load(path));

        Assert.Equal(path, exception.FilePath);
        string error = Assert.Single(exception.Errors);
        Assert.StartsWith("The file can't be read:", error, StringComparison.Ordinal);
        Assert.IsType<FileNotFoundException>(exception.InnerException);
    }

    [Fact]
    public void Load_TruncatedJson_ThrowsOneJsonError()
    {
        string text = File.ReadAllText(ContentFiles.RealMvpContentPath);
        string path = _files.Write(text[..(text.Length / 2)]);

        ContentLoadException exception = Assert.Throws<ContentLoadException>(() => ContentFileLoader.Load(path));

        AssertSingleJsonError(exception);
    }

    [Fact]
    public void Load_JsonNull_ThrowsOneError()
    {
        string path = _files.Write("null");

        ContentLoadException exception = Assert.Throws<ContentLoadException>(() => ContentFileLoader.Load(path));

        Assert.Equal("The file holds the JSON value null instead of a content object.", Assert.Single(exception.Errors));
    }

    [Fact]
    public void Load_UnknownField_ThrowsOneJsonErrorNamingTheField()
    {
        JsonNode content = ContentFiles.ReadRealMvpContent();
        content["buildings"]![0]!["colour"] = "red";
        string path = _files.Write(content.ToJsonString());

        ContentLoadException exception = Assert.Throws<ContentLoadException>(() => ContentFileLoader.Load(path));

        string error = AssertSingleJsonError(exception);
        Assert.Contains("'colour'", error, StringComparison.Ordinal);
        // System.Text.Json leaves the place out of this message; the loader adds it, so a typo says where it is.
        Assert.Contains("Path: $.buildings[0].colour | LineNumber: ", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_FieldNameInOtherCase_ThrowsOneJsonErrorNamingTheField()
    {
        // Names are case-sensitive: "BuildSeconds" is an unknown field, not "buildSeconds".
        JsonNode content = ContentFiles.ReadRealMvpContent();
        JsonObject building = content["buildings"]![0]!.AsObject();
        JsonNode? buildSeconds = building["buildSeconds"];
        building.Remove("buildSeconds");
        building["BuildSeconds"] = buildSeconds;
        string path = _files.Write(content.ToJsonString());

        ContentLoadException exception = Assert.Throws<ContentLoadException>(() => ContentFileLoader.Load(path));

        Assert.Contains("'BuildSeconds'", AssertSingleJsonError(exception), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("field", "\"contentVersion\":\"repeated\"")] // the root already has contentVersion
    [InlineData("map key", "\"twice\":1,\"twice\":1")] // a key of the starting inventory
    public void Load_RepeatedKey_ThrowsOneJsonError(string where, string repeated)
    {
        // System.Text.Json writes no repeated key, so the test writes a marker and replaces it in the text. A repeated field must be a
        // known one: an unknown name would already fail as an unknown field.
        JsonNode content = ContentFiles.ReadRealMvpContent();
        JsonObject target = where == "field" ? content.AsObject() : content["startingCity"]!["inventory"]!.AsObject();
        target["repeated_key_marker"] = 0;
        string text = content.ToJsonString().Replace("\"repeated_key_marker\":0", repeated, StringComparison.Ordinal);
        string path = _files.Write(text);

        ContentLoadException exception = Assert.Throws<ContentLoadException>(() => ContentFileLoader.Load(path));

        AssertSingleJsonError(exception);
    }

    [Theory]
    [InlineData("\"8\"")]
    [InlineData("8.5")]
    [InlineData("true")]
    public void Load_NumberOfWrongType_ThrowsOneJsonError(string gridWidth)
    {
        JsonNode content = ContentFiles.ReadRealMvpContent();
        content["rules"]!["gridWidth"] = JsonNode.Parse(gridWidth);
        string path = _files.Write(content.ToJsonString());

        ContentLoadException exception = Assert.Throws<ContentLoadException>(() => ContentFileLoader.Load(path));

        Assert.Contains("$.rules.gridWidth", AssertSingleJsonError(exception), StringComparison.Ordinal);
    }

    [Fact]
    public void Load_MissingRequiredValue_ThrowsTheFactorysShapeError()
    {
        JsonNode content = ContentFiles.ReadRealMvpContent();
        JsonObject item = content["items"]![0]!.AsObject();
        item.Remove("name");
        string path = _files.Write(content.ToJsonString());

        ContentLoadException exception = Assert.Throws<ContentLoadException>(() => ContentFileLoader.Load(path));

        string error = Assert.Single(exception.Errors);
        Assert.StartsWith($"{ContentErrorCode.MissingValue} at items[{item["id"]}].name:", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_TwoRuleErrors_ListsBothInTheErrorsAndTheMessage()
    {
        JsonNode content = ContentFiles.ReadRealMvpContent();
        JsonNode building = content["buildings"]![0]!;
        JsonNode research = content["research"]![0]!;
        building["buildSeconds"] = 0;
        research["durationSeconds"] = 0;
        string path = _files.Write(content.ToJsonString());

        ContentLoadException exception = Assert.Throws<ContentLoadException>(() => ContentFileLoader.Load(path));

        Assert.Collection(
            exception.Errors,
            error => Assert.StartsWith($"{ContentErrorCode.NotPositive} at buildings[{building["id"]}].buildSeconds:", error, StringComparison.Ordinal),
            error => Assert.StartsWith($"{ContentErrorCode.NotPositive} at research[{research["id"]}].durationSeconds:", error, StringComparison.Ordinal));
        string expectedMessage = $"The content file '{path}' can't be loaded (2 errors):"
            + Environment.NewLine + "  - " + exception.Errors[0]
            + Environment.NewLine + "  - " + exception.Errors[1];
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void Load_EmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ContentFileLoader.Load(string.Empty));
    }

    [Fact]
    public void ContentLoadException_NoErrors_ThrowsArgumentException()
    {
        // A failed load always says why: an empty list is a programmer error.
        Assert.Throws<ArgumentException>("errors", () => new ContentLoadException("content.json", []));
    }

    private static string AssertSingleJsonError(ContentLoadException exception)
    {
        string error = Assert.Single(exception.Errors);
        Assert.StartsWith("The file is not valid content JSON:", error, StringComparison.Ordinal);
        // Every JSON error says where it is, once (System.Text.Json's own suffix or the loader's).
        Assert.Single(error.Split("Path: $"), part => part.Contains("| LineNumber: ", StringComparison.Ordinal));
        Assert.IsAssignableFrom<System.Text.Json.JsonException>(exception.InnerException);
        return error;
    }
}
