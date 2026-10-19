#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Simulation.State
{
    /// <summary>
    /// Everything that is stored about one city (<c>core-instructions/04-simulation-model.md</c>, sections 3 to 5): its clock, era, stock,
    /// buildings and research. Values derived from it and the content (workers, builders, reservations, caps, labor, unlocks) are never stored;
    /// <see cref="CityQueries"/> computes them. Only the engine changes a city, through <see cref="Rules.CityRules"/> and
    /// <see cref="Settlement.Simulator"/>. Every list is sorted, so iterating it is deterministic.
    /// </summary>
    public sealed class CityState
    {
        private readonly ItemTotals _inventory = new ItemTotals();
        private readonly List<Building> _buildings;
        private readonly List<ResearchId> _completedResearch;

        /// <summary>
        /// Restores a city with every value, for example from the database. The city takes the building objects over: the caller must not keep
        /// using them. The values must form a valid city; the checks below are those that need no content.
        /// </summary>
        /// <param name="clock">The city clock in seconds, at least 0.</param>
        /// <param name="era">The era number, at least 1.</param>
        /// <param name="nextBuildingId">The id the next placed building gets, above every building's id, at least 1.</param>
        /// <param name="inventory">The stock: each item at most once, no quantity below 0. Items with 0 are left out.</param>
        /// <param name="buildings">The buildings, in any order: unique ids, one building per tile.</param>
        /// <param name="completedResearch">The completed research nodes, in any order, each at most once.</param>
        /// <param name="activeResearch">The running research, or null; never a completed node.</param>
        /// <exception cref="ArgumentNullException">A list or a building is null.</exception>
        /// <exception cref="ArgumentException">The values don't form a valid city; the message says which rule they break.</exception>
        public CityState(
            long clock,
            int era,
            int nextBuildingId,
            IEnumerable<KeyValuePair<ItemId, long>> inventory,
            IEnumerable<Building> buildings,
            IEnumerable<ResearchId> completedResearch,
            ActiveResearch? activeResearch)
        {
            if (inventory is null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (clock < 0 || era < 1 || nextBuildingId < 1)
            {
                throw new ArgumentException("The clock must not be negative, and the era and the next building id must be at least 1.");
            }

            Clock = clock;
            Era = era;
            NextBuildingId = nextBuildingId;
            ActiveResearch = activeResearch;

            foreach (KeyValuePair<ItemId, long> stock in inventory)
            {
                if (stock.Value < 0 || _inventory.ContainsKey(stock.Key))
                {
                    throw new ArgumentException(FormattableString.Invariant($"Item {stock.Key}: listed twice or below 0."), nameof(inventory));
                }

                _inventory.Set(stock.Key, stock.Value);
            }

            _buildings = SortedBuildings(buildings, nextBuildingId);
            _completedResearch = SortedResearch(completedResearch, activeResearch);
            Inventory = _inventory;
            Buildings = _buildings.AsReadOnly();
            CompletedResearch = _completedResearch.AsReadOnly();
        }

        /// <summary>The city clock in seconds: how much game time the city has lived. It never decreases.</summary>
        public long Clock { get; private set; }

        /// <summary>The number of the era the city is in.</summary>
        public int Era { get; }

        /// <summary>The id the next placed building gets. Ids are never reused.</summary>
        public int NextBuildingId { get; private set; }

        /// <summary>The stock, in ordinal item order. It holds only items with a quantity above 0; <see cref="GetQuantity"/> gives 0 for the others.</summary>
        public IReadOnlyDictionary<ItemId, long> Inventory { get; }

        /// <summary>The buildings, in ascending id order (the order they were placed in).</summary>
        public IReadOnlyList<Building> Buildings { get; }

        /// <summary>The completed research nodes, in ordinal id order.</summary>
        public IReadOnlyList<ResearchId> CompletedResearch { get; }

        /// <summary>The research in the city's one research slot, or null when the slot is free.</summary>
        public ActiveResearch? ActiveResearch { get; private set; }

        /// <summary>How many of an item the city has.</summary>
        /// <param name="item">The item.</param>
        /// <returns>The quantity, 0 when the city has none.</returns>
        public long GetQuantity(ItemId item) => _inventory.Get(item);

        /// <summary>Whether the city has completed a research node.</summary>
        /// <param name="id">The research node.</param>
        /// <returns>True when the node is in <see cref="CompletedResearch"/>.</returns>
        public bool HasCompletedResearch(ResearchId id) => _completedResearch.BinarySearch(id) >= 0;

        /// <summary>Looks up a building by its id.</summary>
        /// <param name="id">The building id.</param>
        /// <param name="building">The building, when the city has one with that id.</param>
        /// <returns>True when the city has the building.</returns>
        public bool TryGetBuilding(int id, [NotNullWhen(true)] out Building? building)
        {
            int low = 0;
            int high = _buildings.Count - 1;
            while (low <= high)
            {
                // Overflow-free midpoint; the list is sorted by id.
                int middle = low + ((high - low) / 2);
                int middleId = _buildings[middle].Id;
                if (middleId == id)
                {
                    building = _buildings[middle];
                    return true;
                }

                if (middleId < id)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            building = null;
            return false;
        }

        /// <summary>Whether the city has at least every amount in the list.</summary>
        internal bool HasItems(IReadOnlyList<ItemQuantity> amounts)
        {
            for (int index = 0; index < amounts.Count; index++)
            {
                ItemQuantity amount = amounts[index];
                if (GetQuantity(amount.Item) < amount.Quantity)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Adds every amount in the list. Storage caps don't apply: they limit production only (<c>04</c> section 8).</summary>
        internal void AddItems(IReadOnlyList<ItemQuantity> amounts)
        {
            for (int index = 0; index < amounts.Count; index++)
            {
                ItemQuantity amount = amounts[index];
                _inventory.Set(amount.Item, checked(GetQuantity(amount.Item) + amount.Quantity));
            }
        }

        /// <summary>Takes every amount in the list. The caller checks <see cref="HasItems"/> first; stock never drops below 0.</summary>
        internal void RemoveItems(IReadOnlyList<ItemQuantity> amounts)
        {
            if (!HasItems(amounts))
            {
                throw new InvalidOperationException("The city can't pay these amounts; the caller must check them first.");
            }

            for (int index = 0; index < amounts.Count; index++)
            {
                ItemQuantity amount = amounts[index];
                _inventory.Set(amount.Item, checked(GetQuantity(amount.Item) - amount.Quantity));
            }
        }

        /// <summary>Places a new building with the next id; it is constructing from now until <paramref name="endsAt"/>.</summary>
        internal Building AddBuilding(BuildingTypeId typeId, int x, int y, long endsAt)
        {
            Building building = Building.StartConstruction(NextBuildingId, typeId, x, y, Clock, endsAt);
            NextBuildingId = checked(NextBuildingId + 1);

            // Ids only grow, so appending keeps the list sorted.
            _buildings.Add(building);
            return building;
        }

        /// <summary>Fills the free research slot. The caller checks that it is free.</summary>
        internal void StartResearch(ResearchId id, long endsAt)
        {
            if (ActiveResearch != null)
            {
                throw new InvalidOperationException("The research slot is busy; the caller must check it first.");
            }

            ActiveResearch = new ActiveResearch(id, Clock, endsAt);
        }

        /// <summary>
        /// Completes the running research: the node joins <see cref="CompletedResearch"/> and the slot is free. Its effects are derived from
        /// the completed list (<see cref="CityQueries"/>), so they apply from this moment on.
        /// </summary>
        internal ResearchId CompleteResearch()
        {
            ActiveResearch running = ActiveResearch ?? throw new InvalidOperationException("No research is running.");
            int index = _completedResearch.BinarySearch(running.Id);

            // The constructor rejects a running node that is also completed, and CityRules.StartResearch never starts a completed one, so the
            // search misses and gives the insert point.
            _completedResearch.Insert(~index, running.Id);
            ActiveResearch = null;
            return running.Id;
        }

        /// <summary>Moves the clock forward to <paramref name="clock"/>. The clock never decreases (<c>04</c> invariant 7).</summary>
        internal void AdvanceClockTo(long clock)
        {
            if (clock < Clock)
            {
                throw new InvalidOperationException(FormattableString.Invariant($"The clock can't move back from {Clock} to {clock}."));
            }

            Clock = clock;
        }

        private static List<Building> SortedBuildings(IEnumerable<Building> buildings, int nextBuildingId)
        {
            if (buildings is null)
            {
                throw new ArgumentNullException(nameof(buildings));
            }

            var sorted = new List<Building>();
            var tiles = new HashSet<(int X, int Y)>();
            foreach (Building building in buildings)
            {
                if (building is null)
                {
                    throw new ArgumentNullException(nameof(buildings), "A building is null.");
                }

                if (building.Id >= nextBuildingId || !tiles.Add((building.X, building.Y)))
                {
                    throw new ArgumentException(
                        FormattableString.Invariant($"Building {building.Id}: its id is not below the next building id, or its tile is taken."),
                        nameof(buildings));
                }

                sorted.Add(building);
            }

            sorted.Sort((left, right) => left.Id.CompareTo(right.Id));
            for (int index = 1; index < sorted.Count; index++)
            {
                if (sorted[index].Id == sorted[index - 1].Id)
                {
                    throw new ArgumentException(FormattableString.Invariant($"Building {sorted[index].Id} is listed twice."), nameof(buildings));
                }
            }

            return sorted;
        }

        private static List<ResearchId> SortedResearch(IEnumerable<ResearchId> completedResearch, ActiveResearch? activeResearch)
        {
            if (completedResearch is null)
            {
                throw new ArgumentNullException(nameof(completedResearch));
            }

            var sorted = new List<ResearchId>(completedResearch);
            sorted.Sort();
            for (int index = 0; index < sorted.Count; index++)
            {
                bool isRepeated = index > 0 && sorted[index] == sorted[index - 1];
                bool isActive = activeResearch != null && sorted[index] == activeResearch.Id;
                if (isRepeated || isActive)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant($"Research {sorted[index]}: completed twice, or both completed and running."),
                        nameof(completedResearch));
                }
            }

            return sorted;
        }
    }
}
