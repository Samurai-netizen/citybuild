using System.Globalization;
using System.Reflection;
using CityBuilder.Infrastructure.Content;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Display;
using CityBuilder.Simulation.State;

namespace CityBuilder.DocTools.Pages;

/// <summary>
/// <c>content.md</c>: every content file in <c>server/content/</c> as the server loads it (<see cref="ContentFileLoader"/>, so only valid
/// content generates): the rules, eras, items, buildings with their levels, workers, recipes and per-hour rates at full staffing, roads,
/// amount presets and the research tree. The M0 design copy and the golden scenario stay in <c>core-instructions/23-mvp-content.md</c>.
/// </summary>
public static class ContentPage
{
    /// <summary>The page's file name.</summary>
    public const string FileName = "content.md";

    /// <summary>The folder of the content files, relative to the repository root.</summary>
    private const string _contentFolder = "server/content";

    /// <summary>Generates the page.</summary>
    /// <param name="root">The repository root.</param>
    /// <returns>The page.</returns>
    public static GeneratedPage Generate(string root)
    {
        var page = new MarkdownPage("Content reference", "the content files in `server/content/`, loaded and validated as the server loads them");
        page.Paragraph("Per-hour rates are at full staffing (the level's max workers) and without research: the engine's display rate " +
                       "(`ProductionDisplay.PerHour`), rounded down to thousandths. A building's labor reductions from research shorten its cycles further.");
        foreach (string file in Directory.GetFiles(Path.Combine(root, _contentFolder), "*.json").Order(StringComparer.Ordinal))
        {
            Write(page, Path.GetFileName(file), ContentFileLoader.Load(file));
        }

        return page.ToPage(FileName);
    }

    /// <summary>Writes one content file's sections.</summary>
    /// <param name="page">The page.</param>
    /// <param name="fileName">The content file's name.</param>
    /// <param name="content">The loaded content.</param>
    private static void Write(MarkdownPage page, string fileName, GameContent content)
    {
        page.Heading(2, fileName)
            .Paragraph($"Content version {MarkdownPage.Code(content.ContentVersion)}. The starting city is in era {PageFormat.Number(content.StartingCity.Era)} " +
                       $"with {PageFormat.Number(content.StartingCity.Purse)} {MarkdownPage.Code(content.Currency.Value)} and no buildings.");

        page.Heading(3, "Rules")
            .Table(["Rule", "Value"], RuleValues(content.Rules).Select(rule => (IReadOnlyList<string>)[MarkdownPage.Code(rule.Key), rule.Value]));

        page.Heading(3, "Eras")
            .Table(["Number", "Id", "Name"], content.Eras.Select(era => (IReadOnlyList<string>)
                [PageFormat.Number(era.Number), MarkdownPage.Code(era.Id.Value), MarkdownPage.Text(era.Name)]));

        page.Heading(3, "Items")
            .Table(["Id", "Name", "Era", "Currency", "Tradable"], content.Items.Select(item => (IReadOnlyList<string>)
                [MarkdownPage.Code(item.Id.Value), MarkdownPage.Text(item.Name), PageFormat.Number(item.Era), YesNo(item.IsCurrency), YesNo(item.IsTradable)]));

        WriteBuildings(page, content);

        page.Heading(3, "Roads")
            .Table(["Level", "Name", "Walk per tile", "Cost per tile"], content.RoadLevels.Select(road => (IReadOnlyList<string>)
                [PageFormat.Number(road.Level), MarkdownPage.Text(road.Name), PageFormat.Number(road.TileMillis) + " ms", PageFormat.Goods(road.Cost)]));

        page.Heading(3, "Amount presets")
            .Table(["Id", "Name", "Percent"], content.AmountPresets.Select(preset => (IReadOnlyList<string>)
                [MarkdownPage.Code(preset.Id.Value), MarkdownPage.Text(preset.Name), PageFormat.Number(preset.Percent) + " %"]));

        WriteResearch(page, content);
    }

    /// <summary>The buildings: one overview table, then one table of levels per building type.</summary>
    /// <param name="page">The page.</param>
    /// <param name="content">The content.</param>
    private static void WriteBuildings(MarkdownPage page, GameContent content)
    {
        page.Heading(3, "Buildings")
            .Table(["Id", "Name", "Kind", "Era", "Available", "Max per city", "Cost", "Build time", "Levels"],
                content.Buildings.Select(building => (IReadOnlyList<string>)
                [
                    MarkdownPage.Code(building.Id.Value), MarkdownPage.Text(building.Name), MarkdownPage.Code(building.Kind.ToString()),
                    PageFormat.Number(building.Era),
                    building.IsAvailableAtStart ? "at start"
                        : building.UnlockedByResearch is { } research ? "research " + MarkdownPage.Code(research.Value) : "—",
                    building.MaxPerCity is { } max ? PageFormat.Number(max) : "—",
                    PageFormat.Goods(building.Cost), PageFormat.Duration(building.BuildSeconds), PageFormat.Number(building.Levels.Count),
                ]));

        CityState city = CityFactory.CreateStarterCity(content);
        foreach (BuildingDefinition building in content.Buildings)
        {
            page.Heading(4, $"{building.Name} ({building.Id.Value})")
                .Table(["Level", "Max workers", "Recipe per cycle", "Labor", "Per hour at full staffing", "Storage", "Other", "Upgrade cost", "Upgrade time"],
                    building.Levels.Select(level => (IReadOnlyList<string>)
                    [
                        PageFormat.Number(level.Level), PageFormat.Number(level.MaxWorkers), Recipe(level.Recipe),
                        level.Recipe is null ? "—" : PageFormat.Number(level.Recipe.Labor) + " worker-s",
                        PerHour(city, content, building, level), PageFormat.Goods(level.Storage), Other(level),
                        level.Level == 1 ? "—" : PageFormat.Goods(level.UpgradeCost),
                        level.Level == 1 ? "—" : PageFormat.Duration(level.UpgradeSeconds),
                    ]));
        }
    }

    /// <summary>A recipe as <c>inputs → outputs</c>, or a dash for a level without one.</summary>
    /// <param name="recipe">The recipe, or null.</param>
    /// <returns>The cell.</returns>
    private static string Recipe(RecipeDefinition? recipe) =>
        recipe is null ? "—" : (recipe.Inputs.Count == 0 ? "nothing" : PageFormat.Goods(recipe.Inputs)) + " → " + PageFormat.Goods(recipe.Outputs);

    /// <summary>
    /// What a level takes in and gives out per hour with all its workers and no research, from the engine's display rate on a building of that
    /// type and level in a starter city; a dash for a level without a recipe or without workers.
    /// </summary>
    /// <param name="city">A starter city: no research, so no labor reductions.</param>
    /// <param name="content">The content.</param>
    /// <param name="building">The building type.</param>
    /// <param name="level">The level.</param>
    /// <returns>The cell, for example <c>60 logs → 30 planks</c>.</returns>
    private static string PerHour(CityState city, GameContent content, BuildingDefinition building, BuildingLevelDefinition level)
    {
        if (level.Recipe is null || level.MaxWorkers == 0)
        {
            return "—";
        }

        var preview = new Building(1, building.Id, 0, 0, level.Level, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
        ProductionRates rates = ProductionDisplay.PerHour(city, content, preview, level.MaxWorkers);
        string inputs = rates.Inputs.Count == 0 ? "nothing" : Rates(rates.Inputs);
        return inputs + " → " + Rates(rates.Outputs);
    }

    /// <summary>Rates as <c>7.5 logs</c>, in recipe order.</summary>
    /// <param name="rates">The rates.</param>
    /// <returns>The text.</returns>
    private static string Rates(IReadOnlyList<ItemRate> rates) =>
        string.Join(", ", rates.Select(rate => PageFormat.Milli(rate.MilliPerHour) + " " + MarkdownPage.Code(rate.Item.Value)));

    /// <summary>The level's values that only some kinds have: the workers a house provides, a courier station's carry limit and stamina.</summary>
    /// <param name="level">The level.</param>
    /// <returns>The cell, or a dash.</returns>
    private static string Other(BuildingLevelDefinition level)
    {
        var parts = new List<string>();
        if (level.WorkersProvided > 0)
        {
            parts.Add("provides " + PageFormat.Number(level.WorkersProvided) + " workers");
        }

        if (level.CarryLimit > 0)
        {
            parts.Add("carries " + PageFormat.Number(level.CarryLimit));
        }

        if (level.StaminaSeconds > 0)
        {
            parts.Add("stamina " + PageFormat.Duration(level.StaminaSeconds));
        }

        return parts.Count == 0 ? "—" : string.Join(", ", parts);
    }

    /// <summary>The research nodes: a Mermaid diagram of the prerequisites, then one table.</summary>
    /// <param name="page">The page.</param>
    /// <param name="content">The content.</param>
    private static void WriteResearch(MarkdownPage page, GameContent content)
    {
        page.Heading(3, "Research");
        var diagram = new List<string> { "flowchart LR" };
        diagram.AddRange(content.Research.Select(node => $"  {NodeId(node.Id.Value)}[{MarkdownPage.MermaidLabel(node.Name)}]"));
        foreach (ResearchDefinition node in content.Research)
        {
            diagram.AddRange(node.Prerequisites.Select(prerequisite => $"  {NodeId(prerequisite.Value)} --> {NodeId(node.Id.Value)}"));
        }

        page.Paragraph("An arrow points from a prerequisite to the research it opens.")
            .Mermaid(diagram)
            .Table(["Id", "Name", "Era", "Cost", "Duration", "Prerequisites", "Effects"], content.Research.Select(node => (IReadOnlyList<string>)
            [
                MarkdownPage.Code(node.Id.Value), MarkdownPage.Text(node.Name), PageFormat.Number(node.Era), PageFormat.Goods(node.Cost),
                PageFormat.Duration(node.DurationSeconds), PageFormat.Codes(node.Prerequisites.Select(prerequisite => prerequisite.Value)),
                node.Effects.Count == 0 ? "—" : string.Join("; ", node.Effects.Select(Effect)),
            ]));
    }

    /// <summary>One research effect in words. A new effect type needs a line here (recipe "New engine rule or effect type" in <c>19</c>).</summary>
    /// <param name="effect">The effect.</param>
    /// <returns>The text.</returns>
    /// <exception cref="InvalidOperationException">An effect type this page doesn't know.</exception>
    private static string Effect(ResearchEffect effect) => effect switch
    {
        UnlockBuildingEffect unlock => "unlocks " + MarkdownPage.Code(unlock.Building.Value),
        LaborReductionEffect reduction => $"{Permille(reduction.Permille)} less labor for {MarkdownPage.Code(reduction.Building.Value)}",
        StorageCapBonusEffect bonus => $"+{PageFormat.Number(bonus.Amount)} storage limit for {MarkdownPage.Code(bonus.Item.Value)}",
        UnlockAmountModeEffect mode => "unlocks the amount mode " + MarkdownPage.Code(mode.Mode.ToString()),
        WalkSpeedBonusEffect walk => $"{Permille(walk.Permille)} faster walking",
        _ => throw new InvalidOperationException(
            $"DocTools has no text for the research effect {effect.GetType().Name}: add it to ContentPage.Effect (tools/DocTools)."),
    };

    /// <summary>Permille as a percentage, for example 250 → <c>25 %</c>, 125 → <c>12.5 %</c>.</summary>
    /// <param name="permille">The permille.</param>
    /// <returns>The text.</returns>
    private static string Permille(int permille) => PageFormat.Milli(permille * 100L) + " %";

    /// <summary>
    /// Every public property of the rules with its value, in declaration order, named as in the content file (camelCase). Reflection keeps a
    /// new rule from being left off the page.
    /// </summary>
    /// <param name="rules">The rules.</param>
    /// <returns>The key and the formatted value of each rule.</returns>
    private static IEnumerable<KeyValuePair<string, string>> RuleValues(GameRules rules) =>
        typeof(GameRules).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(property => property.MetadataToken)
            .Select(property => KeyValuePair.Create(
                char.ToLowerInvariant(property.Name[0]) + property.Name[1..],
                property.GetValue(rules) is { } value ? MarkdownPage.Code(Convert.ToString(value, CultureInfo.InvariantCulture)) : "—"));

    /// <summary>"yes" or "no".</summary>
    /// <param name="value">The flag.</param>
    /// <returns>The word.</returns>
    private static string YesNo(bool value) => value ? "yes" : "no";

    /// <summary>A Mermaid node id for a research id: letters, digits and underscores only.</summary>
    /// <param name="id">The research id.</param>
    /// <returns>The node id.</returns>
    private static string NodeId(string id) => "r_" + new string([.. id.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_')]);
}
