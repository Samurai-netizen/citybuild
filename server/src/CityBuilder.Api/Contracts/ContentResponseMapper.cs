using CityBuilder.Simulation.Content;

namespace CityBuilder.Api.Contracts;

/// <summary>
/// Maps the validated <see cref="GameContent"/> to the <see cref="ContentResponse"/> of <c>GET /api/v1/content</c>. It only renames and
/// reshapes: every number comes from the content, and the lists keep the engine's order (eras by number, the rest by id in ordinal order),
/// so the same content always gives the same response text. The wire names of the closed sets (building kinds, effect types) are written
/// here on purpose, so renaming an engine enum member can't change the contract unnoticed.
/// </summary>
public static class ContentResponseMapper
{
    /// <summary>Maps the content to its response.</summary>
    /// <param name="content">The game content the server runs on.</param>
    /// <param name="timeScale">The server's time scale, <c>rules.timeScale</c>; 1 outside Development.</param>
    /// <returns>The response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The content holds a building kind or an effect type this mapper doesn't know (an engine
    /// change that forgot the contract).</exception>
    public static ContentResponse ToResponse(GameContent content, int timeScale)
    {
        ArgumentNullException.ThrowIfNull(content);

        return new ContentResponse(
            content.ContentVersion,
            ToRules(content.Rules, timeScale),
            content.Eras.Select(era => new ContentEra(era.Number, era.Id.Value, era.Name)).ToArray(),
            content.Items.Select(item => new ContentItem(item.Id.Value, item.Name, item.Era, item.IsCapped, item.IsTradable)).ToArray(),
            content.Buildings.Select(ToBuilding).ToArray(),
            content.Research.Select(ToResearch).ToArray());
    }

    /// <summary>Maps the rules, adding the server's time scale.</summary>
    /// <param name="rules">The content's rules.</param>
    /// <param name="timeScale">The server's time scale.</param>
    /// <returns>The <c>rules</c> object.</returns>
    private static ContentRules ToRules(GameRules rules, int timeScale) => new(
        rules.GridWidth,
        rules.GridHeight,
        rules.BuilderSlots,
        rules.ResearchSlots,
        ToAmounts(rules.BaseStorageCaps),
        rules.MaxOfflineProductionSeconds,
        rules.MinCycleSeconds,
        rules.MaxLaborReductionPermille,
        timeScale);

    /// <summary>Maps a building type with its levels.</summary>
    /// <param name="building">The building type.</param>
    /// <returns>Its response object.</returns>
    private static ContentBuilding ToBuilding(BuildingDefinition building) => new(
        building.Id.Value,
        building.Name,
        building.Kind switch
        {
            BuildingKind.House => "House",
            BuildingKind.Producer => "Producer",
            _ => throw new InvalidOperationException($"Building kind {building.Kind} has no wire name; add it to the contract."),
        },
        building.Era,
        building.IsAvailableAtStart,
        building.UnlockedByResearch?.Value,
        ToAmounts(building.Cost),
        building.BuildSeconds,
        building.Levels.Select(ToLevel).ToArray());

    /// <summary>Maps one level; level 1 has no upgrade, so its upgrade cost and time are null rather than an empty cost and 0 seconds.</summary>
    /// <param name="level">The level.</param>
    /// <returns>Its response object.</returns>
    private static ContentBuildingLevel ToLevel(BuildingLevelDefinition level) => new(
        level.Level,
        level.MaxWorkers,
        level.WorkersProvided,
        level.Recipe is null
            ? null
            : new ContentRecipe(ToAmounts(level.Recipe.Inputs), ToAmounts(level.Recipe.Outputs), level.Recipe.Labor),
        level.Level == 1 ? null : ToAmounts(level.UpgradeCost),
        level.Level == 1 ? null : level.UpgradeSeconds);

    /// <summary>Maps a research node.</summary>
    /// <param name="research">The node.</param>
    /// <returns>Its response object.</returns>
    private static ContentResearch ToResearch(ResearchDefinition research) => new(
        research.Id.Value,
        research.Name,
        research.Era,
        ToAmounts(research.Cost),
        research.DurationSeconds,
        research.Prerequisites.Select(prerequisite => prerequisite.Value).ToArray(),
        research.Effects.Select(ToEffect).ToArray());

    /// <summary>Maps one effect to the flat shape: its type name and the fields that type uses.</summary>
    /// <param name="effect">The effect.</param>
    /// <returns>Its response object.</returns>
    private static ContentResearchEffect ToEffect(ResearchEffect effect) => effect switch
    {
        UnlockBuildingEffect unlock => new ContentResearchEffect("unlockBuilding", unlock.Building.Value, null, null, null),
        LaborReductionEffect labor => new ContentResearchEffect("laborReduction", labor.Building.Value, null, labor.Permille, null),
        StorageCapBonusEffect bonus => new ContentResearchEffect("storageCapBonus", null, bonus.Item.Value, null, bonus.Amount),
        _ => throw new InvalidOperationException($"Effect type {effect.GetType().Name} has no wire shape; add it to the contract."),
    };

    /// <summary>Maps an item map to a JSON object: item id to quantity, in ordinal id order whatever the order of the lines.</summary>
    /// <param name="lines">The lines of a cost, recipe side or cap map.</param>
    /// <returns>The map, sorted by item id.</returns>
    private static SortedDictionary<string, long> ToAmounts(IReadOnlyList<ItemQuantity> lines)
    {
        var amounts = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (ItemQuantity line in lines)
        {
            amounts.Add(line.Item.Value, line.Quantity);
        }

        return amounts;
    }
}
