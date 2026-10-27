namespace CityBuilder.Api.Contracts;

/// <summary>One level of a <see cref="ContentBuilding"/>.</summary>
/// <param name="Level">The level number, from 1, JSON <c>level</c>.</param>
/// <param name="MaxWorkers">How many workers a producer may have at this level, JSON <c>maxWorkers</c>; 0 for houses.</param>
/// <param name="WorkersProvided">How many workers a completed house provides at this level, JSON <c>workersProvided</c>; 0 for producers.</param>
/// <param name="Recipe">What a producer makes at this level, JSON <c>recipe</c>; <see langword="null"/> for houses.</param>
/// <param name="UpgradeCost">What upgrading from the level below to this one costs, JSON <c>upgradeCost</c>: item id to quantity, by item id;
/// <see langword="null"/> for level 1, which is built, not upgraded to.</param>
/// <param name="UpgradeSeconds">How long that upgrade takes, in seconds, JSON <c>upgradeSeconds</c>; <see langword="null"/> for level 1.</param>
public sealed record ContentBuildingLevel(
    int Level,
    int MaxWorkers,
    int WorkersProvided,
    ContentRecipe? Recipe,
    IReadOnlyDictionary<string, long>? UpgradeCost,
    long? UpgradeSeconds);
