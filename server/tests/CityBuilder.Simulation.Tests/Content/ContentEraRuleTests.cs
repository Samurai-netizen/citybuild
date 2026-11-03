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

    /// <summary>Each place of era 1 where the era-2 item iron can be used, by the expected error path.</summary>
    private static readonly Dictionary<string, Action<GameContentData>> _laterEraUses = new()
    {
        ["buildings[hut].cost.iron"] = data => Building(data, "hut").Cost!["iron"] = 1,
        ["buildings[hut].levels[2].upgradeCost.iron"] = data => Level(data, "hut", 2).UpgradeCost!["iron"] = 1,
        ["buildings[depot].levels[2].storage.iron"] = data => Level(data, "depot", 2).Storage!["iron"] = 10,
        ["research[carpentry].cost.iron"] = data => Research(data, "carpentry").Cost!["iron"] = 1,
    };

    /// <summary>The error paths of <see cref="_laterEraUses"/>.</summary>
    public static TheoryData<string> LaterEraUsePaths => new(_laterEraUses.Keys);

    /// <summary>Using iron in era 1 gives one <see cref="ContentErrorCode.ItemInLaterEra"/> at that use, so no city meets an item before its era (OD-10).</summary>
    [Theory]
    [MemberData(nameof(LaterEraUsePaths))]
    public void Create_ItemOfALaterEraUsed_ReportsItemInLaterEraAtItsPath(string path) =>
        ContentAssert.SingleError(WithIron(_laterEraUses[path]), ContentErrorCode.ItemInLaterEra, path);

    /// <summary>
    /// Iron in an era-1 recipe is an item of a later era there, and the producer's store doesn't hold it either (rule 9, which the building
    /// rules check first): both errors, in the factory's order. With iron added to the store too, the store's iron is a third use of it.
    /// </summary>
    /// <param name="building">The producer.</param>
    /// <param name="side">The recipe side, <c>inputs</c> or <c>outputs</c>.</param>
    [Theory]
    [InlineData("workshop", "inputs")]
    [InlineData("lumber_camp", "outputs")]
    public void Create_ItemOfALaterEraInARecipe_ReportsItAndTheStoreWithoutIt(string building, string side)
    {
        GameContentData data = WithIron(data =>
        {
            RecipeData recipe = Level(data, building, 1).Recipe!;
            (side == "inputs" ? recipe.Inputs ??= [] : recipe.Outputs!)["iron"] = 1;
        });

        ContentAssert.Errors(
            data,
            (ContentErrorCode.StoreMismatch, $"buildings[{building}].levels[1].storage"),
            (ContentErrorCode.ItemInLaterEra, $"buildings[{building}].levels[1].recipe.{side}.iron"));
    }

    /// <summary>An era-2 research node may cost the era-2 item.</summary>
    [Fact]
    public void Create_ItemUsedInItsOwnEra_IsAccepted()
    {
        ContentAssert.Valid(WithIron(data => Research(data, "warehouses").Cost!["iron"] = 1));
    }

    /// <summary>A later era may keep using earlier goods: warehouses (era 2) costs gold (era 1).</summary>
    [Fact]
    public void Create_ItemOfAnEarlierEraUsed_IsAccepted()
    {
        // Warehouses (era 2) already costs gold (era 1); a later recipe may keep using earlier goods (25: old goods stay useful).
        Assert.Equal(2, Research(Valid(), "warehouses").Era);
        ContentAssert.Valid(Valid());
    }

    /// <summary>When the user's era doesn't exist, only that unknown era is reported, not the item's era as well.</summary>
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

    /// <summary>When the item's era doesn't exist, only that unknown era is reported.</summary>
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

    /// <summary>The valid fixture with an era-2 good <c>iron</c>, and then the change.</summary>
    private static GameContentData WithIron(Action<GameContentData> change) => ValidWith(data =>
    {
        data.Items!.Add(new ItemData { Id = "iron", Name = "Iron", Era = 2, Currency = false, Tradable = true });
        change(data);
    });
}
