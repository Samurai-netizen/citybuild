#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// All game definitions, validated: the only form in which the engine accepts content. It can't be changed after it is made, and only
    /// <see cref="GameContentFactory.Create"/> makes it. Lists are sorted (items, building types and research by id in ordinal order, eras
    /// by number), so iterating them is deterministic; lookups by id never depend on dictionary order.
    /// </summary>
    public sealed class GameContent
    {
        private readonly Dictionary<int, EraDefinition> _erasByNumber = new Dictionary<int, EraDefinition>();
        private readonly Dictionary<ItemId, ItemDefinition> _itemsById = new Dictionary<ItemId, ItemDefinition>();
        private readonly Dictionary<BuildingTypeId, BuildingDefinition> _buildingsById = new Dictionary<BuildingTypeId, BuildingDefinition>();
        private readonly Dictionary<ResearchId, ResearchDefinition> _researchById = new Dictionary<ResearchId, ResearchDefinition>();

        internal GameContent(
            string contentVersion,
            GameRules rules,
            StartingCityDefinition startingCity,
            IEnumerable<EraDefinition> eras,
            IEnumerable<ItemDefinition> items,
            IEnumerable<BuildingDefinition> buildings,
            IEnumerable<ResearchDefinition> research)
        {
            ContentVersion = contentVersion ?? throw new ArgumentNullException(nameof(contentVersion));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            StartingCity = startingCity ?? throw new ArgumentNullException(nameof(startingCity));
            Eras = ReadOnlyLists.Sorted(eras, (left, right) => left.Number.CompareTo(right.Number));
            Items = ReadOnlyLists.Sorted(items, (left, right) => left.Id.CompareTo(right.Id));
            Buildings = ReadOnlyLists.Sorted(buildings, (left, right) => left.Id.CompareTo(right.Id));
            Research = ReadOnlyLists.Sorted(research, (left, right) => left.Id.CompareTo(right.Id));

            // Add throws on a duplicate key: validation rejects duplicates first, so a throw here is a bug in the factory, not bad data.
            foreach (EraDefinition era in Eras)
            {
                _erasByNumber.Add(era.Number, era);
            }

            foreach (ItemDefinition item in Items)
            {
                _itemsById.Add(item.Id, item);
            }

            foreach (BuildingDefinition building in Buildings)
            {
                _buildingsById.Add(building.Id, building);
            }

            foreach (ResearchDefinition node in Research)
            {
                _researchById.Add(node.Id, node);
            }
        }

        /// <summary>The content's version, for example <c>mvp-1</c>. Snapshots carry it, so a client knows when to fetch the content again.</summary>
        public string ContentVersion { get; }

        /// <summary>The global rules.</summary>
        public GameRules Rules { get; }

        /// <summary>What a new city starts with.</summary>
        public StartingCityDefinition StartingCity { get; }

        /// <summary>The eras, by number: <c>Eras[0]</c> is era 1.</summary>
        public IReadOnlyList<EraDefinition> Eras { get; }

        /// <summary>The items, by id.</summary>
        public IReadOnlyList<ItemDefinition> Items { get; }

        /// <summary>The building types, by id.</summary>
        public IReadOnlyList<BuildingDefinition> Buildings { get; }

        /// <summary>The research nodes of every era, by id.</summary>
        public IReadOnlyList<ResearchDefinition> Research { get; }

        /// <summary>Looks up an era by its number.</summary>
        /// <param name="number">The era number, 1 for the first era.</param>
        /// <param name="era">The era, when it exists.</param>
        /// <returns>True when the content has an era with that number.</returns>
        public bool TryGetEra(int number, [NotNullWhen(true)] out EraDefinition? era) => _erasByNumber.TryGetValue(number, out era);

        /// <summary>Looks up an item by its id.</summary>
        /// <param name="id">The item id.</param>
        /// <param name="item">The item, when it exists.</param>
        /// <returns>True when the content defines the item.</returns>
        public bool TryGetItem(ItemId id, [NotNullWhen(true)] out ItemDefinition? item) => _itemsById.TryGetValue(id, out item);

        /// <summary>Looks up a building type by its id.</summary>
        /// <param name="id">The building type id.</param>
        /// <param name="building">The building type, when it exists.</param>
        /// <returns>True when the content defines the building type.</returns>
        public bool TryGetBuilding(BuildingTypeId id, [NotNullWhen(true)] out BuildingDefinition? building) =>
            _buildingsById.TryGetValue(id, out building);

        /// <summary>Looks up a research node by its id.</summary>
        /// <param name="id">The research id.</param>
        /// <param name="research">The research node, when it exists.</param>
        /// <returns>True when the content defines the research node.</returns>
        public bool TryGetResearch(ResearchId id, [NotNullWhen(true)] out ResearchDefinition? research) =>
            _researchById.TryGetValue(id, out research);
    }
}
