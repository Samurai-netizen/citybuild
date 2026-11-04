namespace CityBuilder.Api.Contracts;

/// <summary>One level of a <see cref="ContentBuilding"/>.</summary>
/// <param name="Level">The level number, from 1, JSON <c>level</c>.</param>
/// <param name="MaxWorkers">How many workers a producer or courier station may have at this level, JSON <c>maxWorkers</c>; 0 for the other kinds.</param>
/// <param name="WorkersProvided">How many workers a completed house provides at this level, JSON <c>workersProvided</c>; 0 for the other kinds.</param>
/// <param name="Recipe">What a producer makes at this level, JSON <c>recipe</c>; <see langword="null"/> for the other kinds.</param>
/// <param name="Storage">The goods the level's store holds and the limit of each, JSON <c>storage</c>: item id to limit, by item id;
/// <see langword="null"/> for a level without a store (houses, courier stations, the science building) (CR-0008).</param>
/// <param name="CarryLimit">How much one courier carries at this level, JSON <c>carryLimit</c>; <see langword="null"/> but for courier stations.</param>
/// <param name="StaminaSeconds">A courier's stamina at this level in seconds, JSON <c>staminaSeconds</c>; <see langword="null"/> but for courier stations.</param>
/// <param name="UpgradeCost">What upgrading from the level below to this one costs, JSON <c>upgradeCost</c>: item id to quantity, by item id;
/// <see langword="null"/> for level 1, which is built, not upgraded to.</param>
/// <param name="UpgradeSeconds">How long that upgrade takes, in seconds, JSON <c>upgradeSeconds</c>; <see langword="null"/> for level 1.</param>
public sealed record ContentBuildingLevel(
    int Level,
    int MaxWorkers,
    int WorkersProvided,
    ContentRecipe? Recipe,
    IReadOnlyDictionary<string, long>? Storage,
    long? CarryLimit,
    long? StaminaSeconds,
    IReadOnlyDictionary<string, long>? UpgradeCost,
    long? UpgradeSeconds);
