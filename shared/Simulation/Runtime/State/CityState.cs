#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Simulation.State
{
    /// <summary>
    /// Everything that is stored about one city (<c>core-instructions/04-simulation-model.md</c>, sections 3 to 5, 16 and 17): its clock, era,
    /// purse, buildings (with their stores and projects), research, roads and trips (section 18). Values derived from it and the content (workers, builders,
    /// reservations, limits, labor, unlocks, city totals) are never stored;
    /// <see cref="CityQueries"/> computes them. Only the engine changes a city, through <see cref="Rules.CityRules"/> and
    /// <see cref="Settlement.Simulator"/>. Every list is sorted, so iterating it is deterministic.
    /// </summary>
    public sealed class CityState
    {
        /// <summary>The buildings, sorted by id; ids and tiles are unique.</summary>
        private readonly List<Building> _buildings;
        /// <summary>The completed research nodes, sorted by id, each once.</summary>
        private readonly List<ResearchId> _completedResearch;
        /// <summary>The roads, sorted by tile (column, then row), one per tile, never on a building's tile.</summary>
        private readonly List<RoadTile> _roads;
        /// <summary>The trips under way, sorted by id; each between two buildings of the city.</summary>
        private readonly List<Trip> _trips;

        /// <summary>
        /// Restores a city with every value, for example from the database. The city takes the building objects over: the caller must not keep
        /// using them. The values must form a valid city; the checks below are those that need no content.
        /// </summary>
        /// <param name="clock">The city clock in seconds, at least 0.</param>
        /// <param name="era">The era number, at least 1.</param>
        /// <param name="nextBuildingId">The id the next placed building gets, above every building's id, at least 1.</param>
        /// <param name="purse">The coins in the purse (the currency, <c>04</c> section 16), at least 0.</param>
        /// <param name="buildings">The buildings, in any order: unique ids, one building per tile.</param>
        /// <param name="completedResearch">The completed research nodes, in any order, each at most once.</param>
        /// <param name="activeResearch">The running research, or null; never a completed node.</param>
        /// <param name="roads">The roads, in any order: one per tile, none on a building's tile; null for none.</param>
        /// <param name="trips">The trips under way, in any order: unique ids below <paramref name="nextTripId"/>, between buildings of the city; null for none.</param>
        /// <param name="nextTripId">The id the next trip gets, above every trip's id, at least 1.</param>
        /// <exception cref="ArgumentNullException">A list or a building is null.</exception>
        /// <exception cref="ArgumentException">The values don't form a valid city; the message says which rule they break.</exception>
        public CityState(
            long clock,
            int era,
            int nextBuildingId,
            long purse,
            IEnumerable<Building> buildings,
            IEnumerable<ResearchId> completedResearch,
            ActiveResearch? activeResearch,
            IEnumerable<RoadTile>? roads = null,
            IEnumerable<Trip>? trips = null,
            int nextTripId = 1)
        {
            if (clock < 0 || era < 1 || nextBuildingId < 1 || purse < 0 || nextTripId < 1)
            {
                throw new ArgumentException("The clock and the purse must not be negative, and the era and the next building and trip ids must be at least 1.");
            }

            Clock = clock;
            Era = era;
            NextBuildingId = nextBuildingId;
            ActiveResearch = activeResearch;
            Purse = purse;

            _buildings = SortedBuildings(buildings, nextBuildingId);
            _completedResearch = SortedResearch(completedResearch, activeResearch);
            _roads = SortedRoads(roads ?? Array.Empty<RoadTile>(), _buildings);
            NextTripId = nextTripId;
            _trips = SortedTrips(trips ?? Array.Empty<Trip>(), nextTripId);
            foreach (Trip trip in _trips)
            {
                if (IndexOfBuilding(trip.SourceId) < 0 || IndexOfBuilding(trip.TargetId) < 0)
                {
                    throw new ArgumentException(FormattableString.Invariant($"Trip {trip.Id} names a building the city doesn't have."), nameof(trips));
                }
            }

            Buildings = _buildings.AsReadOnly();
            CompletedResearch = _completedResearch.AsReadOnly();
            Roads = _roads.AsReadOnly();
            Trips = _trips.AsReadOnly();
        }

        /// <summary>The city clock in seconds: how much game time the city has lived. It never decreases.</summary>
        public long Clock { get; private set; }

        /// <summary>The number of the era the city is in.</summary>
        public int Era { get; }

        /// <summary>The id the next placed building gets. Ids are never reused.</summary>
        public int NextBuildingId { get; private set; }

        /// <summary>
        /// The coins in the city's purse: the currency, in no building and uncapped; every coin cost is paid from it at once and the coins a
        /// cycle makes go into it (<c>04</c> section 16).
        /// </summary>
        public long Purse { get; private set; }

        /// <summary>The buildings, in ascending id order (the order they were placed in).</summary>
        public IReadOnlyList<Building> Buildings { get; }

        /// <summary>The completed research nodes, in ordinal id order.</summary>
        public IReadOnlyList<ResearchId> CompletedResearch { get; }

        /// <summary>The research in the city's one research slot, or null when the slot is free.</summary>
        public ActiveResearch? ActiveResearch { get; private set; }

        /// <summary>The roads, by tile: column, then row (<c>04</c> section 17).</summary>
        public IReadOnlyList<RoadTile> Roads { get; }

        /// <summary>The trips under way, by id (<c>04</c> section 18); each keeps a worker busy until it arrives.</summary>
        public IReadOnlyList<Trip> Trips { get; }

        /// <summary>The id the next trip gets. Ids are never reused.</summary>
        public int NextTripId { get; private set; }

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
            int index = IndexOfBuilding(id);
            building = index >= 0 ? _buildings[index] : null;
            return building is not null;
        }

        /// <summary>The index in <see cref="Buildings"/> of the building with the id, or -1 when the city has none: a binary search, the list being sorted by id.</summary>
        internal int IndexOfBuilding(int id)
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
                    return middle;
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

            return -1;
        }

        /// <summary>Looks up the road on a tile.</summary>
        /// <param name="x">The tile column.</param>
        /// <param name="y">The tile row.</param>
        /// <param name="road">The road, when the tile has one.</param>
        /// <returns>True when a road lies on the tile.</returns>
        public bool TryGetRoad(int x, int y, out RoadTile road)
        {
            int index = FindRoad(x, y);
            road = index >= 0 ? _roads[index] : default;
            return index >= 0;
        }

        /// <summary>Looks up the building standing on a tile, in any phase.</summary>
        /// <param name="x">The tile column.</param>
        /// <param name="y">The tile row.</param>
        /// <param name="building">The building, when the tile has one.</param>
        /// <returns>True when a building stands on the tile.</returns>
        public bool TryGetBuildingAt(int x, int y, [NotNullWhen(true)] out Building? building)
        {
            for (int index = 0; index < _buildings.Count; index++)
            {
                if (_buildings[index].X == x && _buildings[index].Y == y)
                {
                    building = _buildings[index];
                    return true;
                }
            }

            building = null;
            return false;
        }

        /// <summary>Takes coins out of the purse. The caller checks the purse first; it never drops below 0.</summary>
        internal void PayFromPurse(long coins)
        {
            if (coins < 0 || coins > Purse)
            {
                throw new InvalidOperationException(FormattableString.Invariant($"The purse can't pay {coins} with {Purse}; the caller must check it first."));
            }

            Purse = checked(Purse - coins);
        }

        /// <summary>Puts coins into the purse (a cycle's coin output); the purse has no limit.</summary>
        internal void AddToPurse(long coins)
        {
            if (coins < 0)
            {
                throw new InvalidOperationException(FormattableString.Invariant($"Coins put into the purse are at least 0, not {coins}."));
            }

            Purse = checked(Purse + coins);
        }

        /// <summary>Places a new building with the next id; it is constructing from now until <paramref name="endsAt"/>.</summary>
        internal Building AddBuilding(BuildingTypeId typeId, int x, int y, long endsAt) =>
            Append(Building.StartConstruction(NextBuildingId, typeId, x, y, Clock, endsAt));

        /// <summary>Places a construction site with the next id: constructing without a timer until <paramref name="needs"/> are delivered.</summary>
        internal Building AddConstructionSite(BuildingTypeId typeId, int x, int y, IReadOnlyList<ItemQuantity> needs) =>
            Append(Building.StartConstructionSite(NextBuildingId, typeId, x, y, needs));

        /// <summary>Starts a trip with the next trip id; ids only grow, so appending keeps the list sorted.</summary>
        internal Trip AddTrip(int sourceId, int targetId, ItemId item, long quantity, long arrivesAt)
        {
            var trip = new Trip(NextTripId, sourceId, targetId, item, quantity, Clock, arrivesAt);
            NextTripId = checked(NextTripId + 1);
            _trips.Add(trip);
            return trip;
        }

        /// <summary>Ends the trip at <paramref name="index"/> in <see cref="Trips"/>: it arrived, and the caller has delivered its goods.</summary>
        internal void RemoveTripAt(int index) => _trips.RemoveAt(index);

        /// <summary>Puts a level-1 road on an empty tile. The caller checks that the tile has neither a road nor a building.</summary>
        internal void PlaceRoad(int x, int y)
        {
            int index = FindRoad(x, y);
            if (index >= 0 || TryGetBuildingAt(x, y, out _))
            {
                throw new InvalidOperationException(FormattableString.Invariant($"The tile ({x}, {y}) is not empty; the caller must check it first."));
            }

            _roads.Insert(~index, new RoadTile(x, y, 1));
        }

        /// <summary>Sets the level of the road on a tile. The caller checks that the tile has a road and that the content has the level.</summary>
        internal void SetRoadLevel(int x, int y, int level) => _roads[RequireRoad(x, y)] = new RoadTile(x, y, level);

        /// <summary>Takes the road off a tile. The caller checks that the tile has one.</summary>
        internal void RemoveRoad(int x, int y) => _roads.RemoveAt(RequireRoad(x, y));

        /// <summary>Fills the free research slot with a running timer from now. The caller checks that it is free.</summary>
        internal void StartResearch(ResearchId id, long endsAt) => FillResearchSlot(new ActiveResearch(id, Clock, endsAt));

        /// <summary>Fills the free research slot with a research waiting for <paramref name="needs"/> at the science building.</summary>
        internal void StartResearchWaiting(ResearchId id, IReadOnlyList<ItemQuantity> needs) => FillResearchSlot(ActiveResearch.Waiting(id, needs));

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

        /// <summary>Puts a research into the free slot. The caller checks that it is free.</summary>
        private void FillResearchSlot(ActiveResearch research)
        {
            if (ActiveResearch != null)
            {
                throw new InvalidOperationException("The research slot is busy; the caller must check it first.");
            }

            ActiveResearch = research;
        }

        /// <summary>Gives the next building id to a new building and adds it; ids only grow, so appending keeps the list sorted.</summary>
        private Building Append(Building building)
        {
            NextBuildingId = checked(NextBuildingId + 1);
            _buildings.Add(building);
            return building;
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

        /// <summary>
        /// The restoring constructor's buildings, checked and sorted by id: none null, every id below the next building id and unique, every tile
        /// used once.
        /// </summary>
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

        /// <summary>The index of the road on a tile in <see cref="_roads"/>, or the bitwise complement of where it would go (binary search by tile).</summary>
        private int FindRoad(int x, int y)
        {
            int low = 0;
            int high = _roads.Count - 1;
            while (low <= high)
            {
                // Overflow-free midpoint; the list is sorted by tile.
                int middle = low + ((high - low) / 2);
                RoadTile road = _roads[middle];
                int order = road.X != x ? road.X.CompareTo(x) : road.Y.CompareTo(y);
                if (order == 0)
                {
                    return middle;
                }

                if (order < 0)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return ~low;
        }

        /// <summary>The index of the road on a tile; throws when the tile has none (the caller's check is missing).</summary>
        private int RequireRoad(int x, int y)
        {
            int index = FindRoad(x, y);
            return index >= 0
                ? index
                : throw new InvalidOperationException(FormattableString.Invariant($"The tile ({x}, {y}) has no road; the caller must check it first."));
        }

        /// <summary>The restoring constructor's roads, checked and sorted by tile: one per tile, none on a building's tile.</summary>
        private static List<RoadTile> SortedRoads(IEnumerable<RoadTile> roads, List<Building> buildings)
        {
            var sorted = new List<RoadTile>(roads);
            sorted.Sort(RoadTile.CompareTiles);
            var buildingTiles = new HashSet<(int X, int Y)>();
            foreach (Building building in buildings)
            {
                buildingTiles.Add((building.X, building.Y));
            }

            for (int index = 0; index < sorted.Count; index++)
            {
                RoadTile road = sorted[index];
                bool isRepeated = index > 0 && RoadTile.CompareTiles(road, sorted[index - 1]) == 0;
                if (isRepeated || buildingTiles.Contains((road.X, road.Y)))
                {
                    throw new ArgumentException(
                        FormattableString.Invariant($"The road at ({road.X}, {road.Y}) shares its tile with another road or a building."), nameof(roads));
                }
            }

            return sorted;
        }

        /// <summary>The restoring constructor's trips, checked and sorted by id: none null, every id unique and below the next trip id.</summary>
        private static List<Trip> SortedTrips(IEnumerable<Trip> trips, int nextTripId)
        {
            var sorted = new List<Trip>();
            foreach (Trip trip in trips)
            {
                sorted.Add(trip ?? throw new ArgumentNullException(nameof(trips), "A trip in the list is null."));
            }

            sorted.Sort((left, right) => left.Id.CompareTo(right.Id));
            for (int index = 0; index < sorted.Count; index++)
            {
                if (sorted[index].Id >= nextTripId || (index > 0 && sorted[index].Id == sorted[index - 1].Id))
                {
                    throw new ArgumentException(FormattableString.Invariant($"Trip {sorted[index].Id}: listed twice or not below the next trip id {nextTripId}."), nameof(trips));
                }
            }

            return sorted;
        }

        /// <summary>The restoring constructor's completed research, checked and sorted: each node once, and not also the running one.</summary>
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
