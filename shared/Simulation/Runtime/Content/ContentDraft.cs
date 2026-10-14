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

        public string ContentVersion { get; }

        public GameRules Rules { get; }

        public StartingCityDefinition StartingCity { get; }

        public List<EraDefinition> Eras { get; }

        public List<ItemDefinition> Items { get; }

        public List<BuildingDefinition> Buildings { get; }

        public List<ResearchDefinition> Research { get; }

        public Dictionary<int, EraDefinition> ErasByNumber { get; } = new Dictionary<int, EraDefinition>();

        public Dictionary<ItemId, ItemDefinition> ItemsById { get; } = new Dictionary<ItemId, ItemDefinition>();

        public Dictionary<BuildingTypeId, BuildingDefinition> BuildingsById { get; } = new Dictionary<BuildingTypeId, BuildingDefinition>();

        public Dictionary<ResearchId, ResearchDefinition> ResearchById { get; } = new Dictionary<ResearchId, ResearchDefinition>();

        /// <summary>The validated content. Call it only when no rule reported an error.</summary>
        public GameContent ToContent() => new GameContent(ContentVersion, Rules, StartingCity, Eras, Items, Buildings, Research);
    }
}
