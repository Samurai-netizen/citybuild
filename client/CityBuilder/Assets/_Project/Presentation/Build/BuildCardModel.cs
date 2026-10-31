#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Display;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Presentation.Build
{
    /// <summary>
    /// One card of the build menu as plain text and flags: what the player reads about a building type (name, cost, build time, workers, what it
    /// makes), whether the type is locked and by what, and the hint for the city as it is now. Everything comes from the content's definitions
    /// and a city; no game number is written here. It is immutable: the menu makes a new one when the city changes, and two cards that say the
    /// same compare equal, so the view redraws only what changed. The hints are hints only (<c>09</c> UX rules): the server checks again.
    /// </summary>
    public sealed class BuildCardModel : IEquatable<BuildCardModel>
    {
        /// <summary>Milli-units in a whole item: the engine's rates are thousandths of an item per hour.</summary>
        private const long MilliPerItem = 1000;

        /// <summary>Creates a card; use <see cref="Create"/>.</summary>
        private BuildCardModel(
            BuildingTypeId typeId,
            string name,
            string costText,
            string buildTimeText,
            string workersText,
            string recipeText,
            string lockText,
            BuildHint hint,
            string hintText)
        {
            TypeId = typeId;
            Name = name;
            CostText = costText;
            BuildTimeText = buildTimeText;
            WorkersText = workersText;
            RecipeText = recipeText;
            LockText = lockText;
            Hint = hint;
            HintText = hintText;
        }

        /// <summary>The building type the card builds.</summary>
        public BuildingTypeId TypeId { get; }

        /// <summary>The type's name from the content, such as <c>Sawmill</c>.</summary>
        public string Name { get; }

        /// <summary>The price, such as <c>Cost: 150 Coins, 30 Logs</c>.</summary>
        public string CostText { get; }

        /// <summary>How long it takes to build, such as <c>Build time: 2m</c>.</summary>
        public string BuildTimeText { get; }

        /// <summary>The most workers level 1 takes, such as <c>Max workers: 3</c>; empty for a building that takes none (a house).</summary>
        public string WorkersText { get; }

        /// <summary>What it makes, such as <c>2 Logs → 1 Planks · 60/h at 3 workers</c>, or what a house provides.</summary>
        public string RecipeText { get; }

        /// <summary>Why the card is locked, such as <c>Requires: Trade Charter</c>; empty when the type may be placed.</summary>
        public string LockText { get; }

        /// <summary>Whether the city may not place the type yet. A locked card cannot be selected.</summary>
        public bool IsLocked => LockText.Length > 0;

        /// <summary>The warning for the city this card was made for; always <see cref="BuildHint.None"/> on a locked card.</summary>
        public BuildHint Hint { get; }

        /// <summary>The hint in words, such as <c>Not enough Planks</c>; empty when there is none.</summary>
        public string HintText { get; }

        /// <summary>
        /// Makes the card of <paramref name="definition"/> for <paramref name="city"/>: the texts use the content's names, the lock and the hint the
        /// city's era, research, builders and stock, and the rate the city's research (a labor reduction shows in the rate).
        /// </summary>
        /// <param name="content">The game content the names come from.</param>
        /// <param name="city">The city to judge the lock and the hint by: the latest one the client has.</param>
        /// <param name="definition">The building type.</param>
        public static BuildCardModel Create(GameContent content, CityState city, BuildingDefinition definition)
        {
            BuildingLevelDefinition level = definition.GetLevel(1);
            string lockText = LockTextFor(content, city, definition);
            BuildHint hint = lockText.Length > 0 ? BuildHint.None : HintFor(content, city, definition);
            return new BuildCardModel(
                definition.Id,
                definition.Name,
                "Cost: " + Quantities(content, definition.Cost, ", "),
                "Build time: " + DurationText.Format(definition.BuildSeconds),
                level.MaxWorkers > 0 ? "Max workers: " + level.MaxWorkers.ToString(CultureInfo.InvariantCulture) : string.Empty,
                RecipeTextFor(content, city, definition, level),
                lockText,
                hint,
                HintTextFor(content, city, definition, hint));
        }

        /// <summary>True when every text and flag is the same: the view has nothing to redraw.</summary>
        public bool Equals(BuildCardModel? other)
        {
            return other != null
                && TypeId.Equals(other.TypeId)
                && Name == other.Name
                && CostText == other.CostText
                && BuildTimeText == other.BuildTimeText
                && WorkersText == other.WorkersText
                && RecipeText == other.RecipeText
                && LockText == other.LockText
                && Hint == other.Hint
                && HintText == other.HintText;
        }

        /// <summary>See <see cref="Equals(BuildCardModel?)"/>.</summary>
        public override bool Equals(object? obj) => Equals(obj as BuildCardModel);

        /// <summary>A hash of the type and the texts that go with <see cref="Equals(BuildCardModel?)"/>.</summary>
        public override int GetHashCode() => TypeId.GetHashCode() ^ RecipeText.GetHashCode() ^ HintText.GetHashCode();

        /// <summary>
        /// <c>Requires: Trade Charter</c> for a type its research has not unlocked, <c>Requires: &lt;era name&gt;</c> for one of a later era than
        /// the city's; empty when <see cref="CityQueries.IsUnlocked"/> says the city may place it (the server's own rule, shared).
        /// </summary>
        private static string LockTextFor(GameContent content, CityState city, BuildingDefinition definition)
        {
            if (CityQueries.IsUnlocked(city, content, definition.Id))
            {
                return string.Empty;
            }

            if (definition.Era > city.Era)
            {
                string era = content.TryGetEra(definition.Era, out EraDefinition? eraDefinition)
                    ? eraDefinition.Name
                    : "Era " + definition.Era.ToString(CultureInfo.InvariantCulture);
                return "Requires: " + era;
            }

            ResearchId? research = definition.UnlockedByResearch;
            string researchName = research.HasValue && content.TryGetResearch(research.Value, out ResearchDefinition? researchDefinition)
                ? researchDefinition.Name
                : (research.HasValue ? research.Value.Value : "research");
            return "Requires: " + researchName;
        }

        /// <summary>The first warning in the server's order: no free builder, then a cost the stock does not cover.</summary>
        private static BuildHint HintFor(GameContent content, CityState city, BuildingDefinition definition)
        {
            if (CityQueries.BuildersBusy(city) >= content.Rules.BuilderSlots)
            {
                return BuildHint.NoFreeBuilder;
            }

            IReadOnlyList<ItemQuantity> cost = definition.Cost;
            for (int index = 0; index < cost.Count; index++)
            {
                if (city.GetQuantity(cost[index].Item) < cost[index].Quantity)
                {
                    return BuildHint.CannotAfford;
                }
            }

            return BuildHint.None;
        }

        /// <summary>The words of <paramref name="hint"/>; for a missing cost they name every item the stock lacks.</summary>
        private static string HintTextFor(GameContent content, CityState city, BuildingDefinition definition, BuildHint hint)
        {
            switch (hint)
            {
                case BuildHint.NoFreeBuilder:
                    return "All builders are busy";
                case BuildHint.CannotAfford:
                    var missing = new StringBuilder();
                    IReadOnlyList<ItemQuantity> cost = definition.Cost;
                    for (int index = 0; index < cost.Count; index++)
                    {
                        if (city.GetQuantity(cost[index].Item) >= cost[index].Quantity)
                        {
                            continue;
                        }

                        missing.Append(missing.Length == 0 ? "Not enough " : ", ").Append(ItemName(content, cost[index].Item));
                    }

                    return missing.ToString();
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// The recipe line of a producer, <c>2 Logs → 1 Planks · 60/h at 3 workers</c> (the rate of the output at the level's most workers, as the
        /// engine's display helper computes it for this city), or what a building without a recipe gives (a house: its workers).
        /// </summary>
        private static string RecipeTextFor(GameContent content, CityState city, BuildingDefinition definition, BuildingLevelDefinition level)
        {
            RecipeDefinition? recipe = level.Recipe;
            if (recipe == null)
            {
                return level.WorkersProvided > 0
                    ? "Provides " + level.WorkersProvided.ToString(CultureInfo.InvariantCulture) + " workers"
                    : string.Empty;
            }

            string inputs = recipe.Inputs.Count == 0 ? "Nothing" : Quantities(content, recipe.Inputs, " + ");
            string outputs = Quantities(content, recipe.Outputs, " + ");
            return inputs + " → " + outputs + " · " + RatesText(content, city, definition, level.MaxWorkers) + " at "
                + level.MaxWorkers.ToString(CultureInfo.InvariantCulture) + " workers";
        }

        /// <summary>
        /// The per-hour rate of each output at <paramref name="workers"/> workers, such as <c>60/h</c> (with the item's name after it when the
        /// recipe has several outputs). The engine's <see cref="ProductionDisplay.PerHour"/> does the maths, so the card equals the inspector and the
        /// simulator; it needs a building, so a level 1 stand-in with no workers is made for it and thrown away.
        /// </summary>
        private static string RatesText(GameContent content, CityState city, BuildingDefinition definition, int workers)
        {
            var standIn = new Building(1, definition.Id, 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
            IReadOnlyList<ItemRate> rates = ProductionDisplay.PerHour(city, content, standIn, workers).Outputs;
            var text = new StringBuilder();
            for (int index = 0; index < rates.Count; index++)
            {
                text.Append(index == 0 ? string.Empty : ", ").Append(PerHour(rates[index].MilliPerHour));
                if (rates.Count > 1)
                {
                    text.Append(' ').Append(ItemName(content, rates[index].Item));
                }
            }

            return text.ToString();
        }

        /// <summary><c>60/h</c> from thousandths per hour; a part of an item shows up to three decimals without trailing zeros (<c>2.5/h</c>).</summary>
        public static string PerHour(long milliPerHour)
        {
            long whole = milliPerHour / MilliPerItem;
            long part = milliPerHour % MilliPerItem;
            string number = whole.ToString(CultureInfo.InvariantCulture);
            if (part != 0)
            {
                number += "." + part.ToString("D3", CultureInfo.InvariantCulture).TrimEnd('0');
            }

            return number + "/h";
        }

        /// <summary><c>150 Coins, 30 Logs</c>: each amount and the content's name of its item, joined by <paramref name="separator"/>.</summary>
        private static string Quantities(GameContent content, IReadOnlyList<ItemQuantity> lines, string separator)
        {
            var text = new StringBuilder();
            for (int index = 0; index < lines.Count; index++)
            {
                text.Append(index == 0 ? string.Empty : separator)
                    .Append(lines[index].Quantity.ToString(CultureInfo.InvariantCulture))
                    .Append(' ')
                    .Append(ItemName(content, lines[index].Item));
            }

            return text.ToString();
        }

        /// <summary>The item's name from the content; the id when the content has none (it always has).</summary>
        private static string ItemName(GameContent content, ItemId item)
        {
            return content.TryGetItem(item, out ItemDefinition? definition) ? definition.Name : item.Value;
        }
    }
}
