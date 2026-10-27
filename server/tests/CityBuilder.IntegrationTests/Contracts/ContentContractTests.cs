using System.Net;
using System.Text.Json.Nodes;
using CityBuilder.Application.Content;
using Microsoft.Extensions.DependencyInjection;

namespace CityBuilder.IntegrationTests.Contracts;

/// <summary>
/// <c>GET /api/v1/content</c> against the contract (<c>core-instructions/05-api-contract.md</c>) on the raw JSON, with the server's real
/// content: the names and types of every object, the nulls the contract writes, the sort orders, and that the text never changes between
/// calls. The numbers are checked against frozen content in <see cref="ContentGoldenTests"/>, so a balance change doesn't fail these.
/// In memory, no Docker.
/// </summary>
/// <param name="factory">The in-memory server with the real content.</param>
public sealed class ContentContractTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>A player id for the tokens; the content endpoint reads no city, so no player has to exist.</summary>
    private static readonly Guid _playerId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");

    /// <summary>With a token the answer is 200 JSON that carries the version of the content the server loaded.</summary>
    [Fact]
    public async Task Get_WithToken_ReturnsTheLoadedContentAsJson()
    {
        ApiResponse response = await ApiClient.GetAsync(factory, "/api/v1/content", _playerId);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("application/json; charset=utf-8", response.ContentType);
        Assert.Equal(factory.Services.GetRequiredService<IContentProvider>().Content.ContentVersion, (string?)response.Json()["contentVersion"]);
    }

    /// <summary>Every object of the document has exactly the contract's property names and JSON types; nothing is missing, extra or renamed.</summary>
    [Fact]
    public async Task Get_EveryObject_HasExactlyTheContractNamesAndTypes()
    {
        JsonNode content = (await ApiClient.GetAsync(factory, "/api/v1/content", _playerId)).Json();

        JsonShape.AssertObject(content, "contentVersion:string rules:object eras:array items:array buildings:array research:array", "$");
        JsonShape.AssertObject(
            content["rules"],
            "gridWidth:int gridHeight:int builderSlots:int researchSlots:int baseStorageCaps:object maxOfflineProductionSeconds:int " +
            "minCycleSeconds:int maxLaborReductionPermille:int timeScale:int",
            "$.rules");
        AssertEach(content["eras"], "number:int id:string name:string", "$.eras");
        AssertEach(content["items"], "id:string name:string era:int capped:bool tradable:bool", "$.items");
        AssertEach(
            content["buildings"],
            "id:string name:string kind:string era:int availableAtStart:bool unlockedBy:string? cost:object buildSeconds:int levels:array",
            "$.buildings");
        AssertEach(
            content["research"],
            "id:string name:string era:int cost:object durationSeconds:int prerequisites:array effects:array",
            "$.research");
        foreach ((JsonNode building, int b) in content["buildings"]!.AsArray().Select((node, index) => (node!, index)))
        {
            AssertEach(
                building["levels"],
                "level:int maxWorkers:int workersProvided:int recipe:object? upgradeCost:object? upgradeSeconds:int?",
                $"$.buildings[{b}].levels");
            foreach ((JsonNode level, int l) in building["levels"]!.AsArray().Select((node, index) => (node!, index)))
            {
                if (level["recipe"] is { } recipe)
                {
                    JsonShape.AssertObject(recipe, "inputs:object outputs:object labor:int", $"$.buildings[{b}].levels[{l}].recipe");
                }
            }
        }

        foreach ((JsonNode research, int r) in content["research"]!.AsArray().Select((node, index) => (node!, index)))
        {
            AssertEach(research["effects"], "type:string building:string? item:string? permille:int? amount:int?", $"$.research[{r}].effects");
        }
    }

    /// <summary>The contract writes its nulls: a house has no recipe, level 1 has no upgrade, and an effect leaves the fields of other types null.</summary>
    [Fact]
    public async Task Get_NotApplicableFields_AreNullNotMissing()
    {
        JsonNode content = (await ApiClient.GetAsync(factory, "/api/v1/content", _playerId)).Json();

        foreach (JsonNode building in content["buildings"]!.AsArray().Select(node => node!))
        {
            string kind = (string)building["kind"]!;
            Assert.True(kind is "House" or "Producer", $"Unknown building kind {kind}.");
            foreach (JsonNode level in building["levels"]!.AsArray().Select(node => node!))
            {
                // A house has no recipe; a producer always has one.
                Assert.Equal(kind == "Producer", level["recipe"] is not null);
                // Level 1 is built, not upgraded to: no cost and no time, not an empty cost and 0 seconds.
                bool isFirst = (int)level["level"]! == 1;
                Assert.Equal(!isFirst, level["upgradeCost"] is not null);
                Assert.Equal(!isFirst, level["upgradeSeconds"] is not null);
            }

            // A building that needs no research has no research to name.
            Assert.Equal(!(bool)building["availableAtStart"]!, building["unlockedBy"] is not null);
        }

        foreach (JsonNode effect in content["research"]!.AsArray().SelectMany(research => research!["effects"]!.AsArray()).Select(node => node!))
        {
            (bool building, bool item, bool permille, bool amount) = ((string)effect["type"]!) switch
            {
                "unlockBuilding" => (true, false, false, false),
                "laborReduction" => (true, false, true, false),
                "storageCapBonus" => (false, true, false, true),
                var other => throw new InvalidOperationException($"Unknown effect type {other}."),
            };
            Assert.Equal(building, effect["building"] is not null);
            Assert.Equal(item, effect["item"] is not null);
            Assert.Equal(permille, effect["permille"] is not null);
            Assert.Equal(amount, effect["amount"] is not null);
        }
    }

    /// <summary>The lists and the maps come in a fixed order: eras by number, the rest by id in ordinal order, levels by level, keys by item id.</summary>
    [Fact]
    public async Task Get_ListsAndMaps_AreSorted()
    {
        JsonNode content = (await ApiClient.GetAsync(factory, "/api/v1/content", _playerId)).Json();

        Assert.Equal(content["eras"]!.AsArray().Select(era => (int)era!["number"]!).Order(), content["eras"]!.AsArray().Select(era => (int)era!["number"]!));
        AssertIdsAreOrdinal(content["items"]!);
        AssertIdsAreOrdinal(content["buildings"]!);
        AssertIdsAreOrdinal(content["research"]!);
        AssertKeysAreOrdinal(content["rules"]!["baseStorageCaps"]!);
        foreach (JsonNode building in content["buildings"]!.AsArray().Select(node => node!))
        {
            AssertKeysAreOrdinal(building["cost"]!);
            Assert.Equal(Enumerable.Range(1, building["levels"]!.AsArray().Count), building["levels"]!.AsArray().Select(level => (int)level!["level"]!));
            foreach (JsonNode level in building["levels"]!.AsArray().Select(node => node!))
            {
                if (level["upgradeCost"] is { } upgradeCost)
                {
                    AssertKeysAreOrdinal(upgradeCost);
                }

                if (level["recipe"] is { } recipe)
                {
                    AssertKeysAreOrdinal(recipe["inputs"]!);
                    AssertKeysAreOrdinal(recipe["outputs"]!);
                }
            }
        }

        foreach (JsonNode research in content["research"]!.AsArray().Select(node => node!))
        {
            AssertKeysAreOrdinal(research["cost"]!);
            string[] prerequisites = research["prerequisites"]!.AsArray().Select(id => (string)id!).ToArray();
            Assert.Equal(prerequisites.Order(StringComparer.Ordinal), prerequisites);
        }
    }

    /// <summary>The same content gives the same text on every call, to the byte, and the time scale is 1 outside Development.</summary>
    [Fact]
    public async Task Get_Twice_ReturnsTheSameTextWithTimeScaleOne()
    {
        ApiResponse first = await ApiClient.GetAsync(factory, "/api/v1/content", _playerId);
        ApiResponse second = await ApiClient.GetAsync(factory, "/api/v1/content", Guid.NewGuid());

        Assert.Equal(first.Body, second.Body);
        Assert.Equal(1, (int)first.Json()["rules"]!["timeScale"]!);
    }

    /// <summary>Asserts that every element of a JSON array has the shape.</summary>
    /// <param name="array">The array.</param>
    /// <param name="shape">The shape of each element (<see cref="JsonShape.AssertObject"/>).</param>
    /// <param name="path">Where the array is in the document.</param>
    private static void AssertEach(JsonNode? array, string shape, string path)
    {
        JsonArray elements = Assert.IsType<JsonArray>(array);
        Assert.NotEmpty(elements);
        for (int index = 0; index < elements.Count; index++)
        {
            JsonShape.AssertObject(elements[index], shape, $"{path}[{index}]");
        }
    }

    /// <summary>Asserts that the <c>id</c> of each element of an array is in ordinal order (no culture, upper case before lower case).</summary>
    /// <param name="array">An array of objects that have an <c>id</c>.</param>
    private static void AssertIdsAreOrdinal(JsonNode array)
    {
        string[] ids = array.AsArray().Select(element => (string)element!["id"]!).ToArray();
        Assert.Equal(ids.Order(StringComparer.Ordinal), ids);
    }

    /// <summary>Asserts that the property names of a JSON object are in ordinal order.</summary>
    /// <param name="map">An object such as a cost.</param>
    private static void AssertKeysAreOrdinal(JsonNode map)
    {
        string[] keys = map.AsObject().Select(property => property.Key).ToArray();
        Assert.Equal(keys.Order(StringComparer.Ordinal), keys);
    }
}
