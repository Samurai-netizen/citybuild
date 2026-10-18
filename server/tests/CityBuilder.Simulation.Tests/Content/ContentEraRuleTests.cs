using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
using static CityBuilder.Simulation.Tests.Content.ContentFixture;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>
/// The era rule of item use (owner directive OD-10): an item in a cost, a recipe or the starting inventory belongs to the era of what uses it
/// or an earlier one. Each case adds the era-2 item <c>iron</c> to the fixture and uses it in exactly one place of era 1.
/// </summary>
public sealed class ContentEraRuleTests
{
    // "!" in these tests reaches into the valid fixture, which sets every value it touches (ContentFixture.Valid).

    private static readonly Dictionary<string, Action<GameContentData>> _laterEraUses = new()
    {
        ["startingCity.inventory.iron"] = data => data.StartingCity!.Inventory!["iron"] = 1,
        ["buildings[hut].cost.iron"] = data => Building(data, "hut").Cost!["iron"] = 1,
        ["buildings[hut].levels[2].upgradeCost.iron"] = data => Level(data, "hut", 2).UpgradeCost!["iron"] = 1,
        ["buildings[workshop].levels[1].recipe.inputs.iron"] = data => Level(data, "workshop", 1).Recipe!.Inputs!["iron"] = 1,
        ["buildings[lumber_camp].levels[1].recipe.outputs.iron"] = data => Level(data, "lumber_camp", 1).Recipe!.Outputs!["iron"] = 1,
        ["research[carpentry].cost.iron"] = data => Research(data, "carpentry").Cost!["iron"] = 1,
    };

    public static TheoryData<string> LaterEraUsePaths => new(_laterEraUses.Keys);

    [Theory]
    [MemberData(nameof(LaterEraUsePaths))]
    public void Create_ItemOfALaterEraUsed_ReportsItemInLaterEraAtItsPath(string path) =>
        ContentAssert.SingleError(WithIron(_laterEraUses[path]), ContentErrorCode.ItemInLaterEra, path);

    [Fact]
    public void Create_ItemUsedInItsOwnEra_IsAccepted()
    {
        ContentAssert.Valid(WithIron(data => Research(data, "warehouses").Cost!["iron"] = 1));
    }

    [Fact]
    public void Create_ItemOfAnEarlierEraUsed_IsAccepted()
    {
        // Warehouses (era 2) already costs gold (era 1); a later recipe may keep using earlier goods (25: old goods stay useful).
        Assert.Equal(2, Research(Valid(), "warehouses").Era);
        ContentAssert.Valid(Valid());
    }

    [Fact]
    public void Create_UserOfAnUnknownEra_ReportsOnlyTheUnknownEra()
    {
        GameContentData data = WithIron(data =>
        {
            Building(data, "hut").Era = 9;
            Building(data, "hut").Cost!["iron"] = 1;
        });

        ContentAssert.SingleError(data, ContentErrorCode.UnknownReference, "buildings[hut].era");
    }

    [Fact]
    public void Create_ItemOfAnUnknownEra_ReportsOnlyTheUnknownEra()
    {
        GameContentData data = WithIron(data =>
        {
            Item(data, "iron").Era = 9;
            Building(data, "hut").Cost!["iron"] = 1;
        });

        ContentAssert.SingleError(data, ContentErrorCode.UnknownReference, "items[iron].era");
    }

    /// <summary>The valid fixture with an uncapped era-2 item <c>iron</c>, and then the change.</summary>
    private static GameContentData WithIron(Action<GameContentData> change) => ValidWith(data =>
    {
        data.Items!.Add(new ItemData { Id = "iron", Name = "Iron", Era = 2, Capped = false, Tradable = true });
        change(data);
    });
}
