#nullable enable
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// Content that has the right shape but has not passed the rules yet. The lists keep the order of the data, so rule errors come in file
    /// order; the lookups hold the first definition of each id, so a duplicate (reported by its rule) can't break another check.
    /// </summary>
    internal sealed class ContentDraft
    {
        /// <summary>
        /// Takes the read definitions as they are and indexes them by id or number; the first definition of an id wins the lookup, so a
        /// duplicate stays in the list (for its rule to report) without hiding the original from the other rules.
        /// </summary>
        public ContentDraft(
            string contentVersion,
            GameRules rules,
            StartingCityDefinition startingCity,
            List<EraDefinition> eras,
            List<ItemDefinition> items,
            List<BuildingDefinition> buildings,
            List<ResearchDefinition> research)
        {
            ContentVersion = contentVersion;
            Rules = rules;
            StartingCity = startingCity;
            Eras = eras;
            Items = items;
            Buildings = buildings;
            Research = research;

            foreach (EraDefinition era in eras)
            {
                ErasByNumber.TryAdd(era.Number, era);
            }

            foreach (ItemDefinition item in items)
            {
                ItemsById.TryAdd(item.Id, item);
            }

            foreach (BuildingDefinition building in buildings)
            {
                BuildingsById.TryAdd(building.Id, building);
            }

            foreach (ResearchDefinition node in research)
            {
                ResearchById.TryAdd(node.Id, node);
            }
        }

        /// <summary>The content's version label, such as <c>mvp-1</c>; not checked beyond being present.</summary>
        public string ContentVersion { get; }

        /// <summary>The global rules (grid, slots, caps, limits) as read; <see cref="ValueRules"/> checks their values.</summary>
        public GameRules Rules { get; }

        /// <summary>The era and stock of a new city as read.</summary>
        public StartingCityDefinition StartingCity { get; }

        /// <summary>Every era in file order, duplicates included.</summary>
        public List<EraDefinition> Eras { get; }

        /// <summary>Every item in file order, duplicates included.</summary>
        public List<ItemDefinition> Items { get; }

        /// <summary>Every building type in file order, duplicates included.</summary>
        public List<BuildingDefinition> Buildings { get; }

        /// <summary>Every research node in file order, duplicates included.</summary>
        public List<ResearchDefinition> Research { get; }

        /// <summary>The first era with each number; for references from items, buildings, research and the starting city.</summary>
        public Dictionary<int, EraDefinition> ErasByNumber { get; } = new Dictionary<int, EraDefinition>();

        /// <summary>The first item with each id; for references from costs, recipes, caps, bonuses and the starting stock.</summary>
        public Dictionary<ItemId, ItemDefinition> ItemsById { get; } = new Dictionary<ItemId, ItemDefinition>();

        /// <summary>The first building type with each id; for the targets of research effects.</summary>
        public Dictionary<BuildingTypeId, BuildingDefinition> BuildingsById { get; } = new Dictionary<BuildingTypeId, BuildingDefinition>();

        /// <summary>The first research node with each id; for prerequisites and the research graph.</summary>
        public Dictionary<ResearchId, ResearchDefinition> ResearchById { get; } = new Dictionary<ResearchId, ResearchDefinition>();

        /// <summary>Whether this is the first item with its id (a later one is a duplicate, reported once and left out of other checks).</summary>
        public bool IsFirst(ItemDefinition item) => ReferenceEquals(ItemsById[item.Id], item);

        /// <summary>Whether this is the first building type with its id.</summary>
        public bool IsFirst(BuildingDefinition building) => ReferenceEquals(BuildingsById[building.Id], building);

        /// <summary>Whether this is the first research node with its id.</summary>
        public bool IsFirst(ResearchDefinition node) => ReferenceEquals(ResearchById[node.Id], node);

        /// <summary>The validated content. Call it only when no rule reported an error.</summary>
        public GameContent ToContent() => new GameContent(ContentVersion, Rules, StartingCity, Eras, Items, Buildings, Research);
    }
}
