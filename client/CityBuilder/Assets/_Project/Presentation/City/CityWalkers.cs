#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The couriers and trip workers on the map (CR-0008-T2 part D, <c>04</c> sections 17 to 19). <see cref="Show"/> makes the walkers match a
    /// city: one per courier (standing at its station while it is at the station or resting) and one per trip; a walk's path is planned when
    /// the walk is first seen and kept until it ends (a walk keeps the path it started with). <see cref="Tick"/> places each walker for the
    /// display time between the walk's departure and arrival, from the snapshot's times. Views are pooled and nothing is allocated on a frame
    /// where no walk starts. Display only: nothing here is ever sent to the server.
    /// </summary>
    public sealed class CityWalkers
    {
        /// <summary>The folder, under <c>Resources</c>, of the common models.</summary>
        public const string ModelFolder = "Models/common/";

        /// <summary>The model of a courier.</summary>
        public const string CourierModelName = "courier";

        /// <summary>The model of a trip worker.</summary>
        public const string WorkerModelName = "worker";

        /// <summary>The start of the model name of a load: <c>carry_logs</c>.</summary>
        public const string CarryPrefix = "carry_";

        /// <summary>The sideways step, in tiles, between couriers standing at one station.</summary>
        private const float StandingSpacing = 0.22f;

        /// <summary>What a walker is showing and where it goes.</summary>
        private sealed class Entry
        {
            /// <summary>The walker's key in <see cref="_entries"/>.</summary>
            public long Key;

            /// <summary>True for a courier, false for a trip worker.</summary>
            public bool IsCourier;

            /// <summary>The view.</summary>
            public WalkerView View = null!;

            /// <summary>Whether the walker is on a walk now.</summary>
            public bool Walking;

            /// <summary>The walk's departure and arrival, city seconds; 0 when standing.</summary>
            public long DepartedAt;

            /// <summary>The walk's arrival, city seconds.</summary>
            public long ArrivesAt;

            /// <summary>The courier's state, to see a new walk begin.</summary>
            public CourierState State;

            /// <summary>The stop a courier walks to, to see a new walk begin.</summary>
            public int ToStop;

            /// <summary>The path of the walk, or the one tile of a standing walker.</summary>
            public WalkPath Path = null!;

            /// <summary>The sideways offset of a standing walker, in tiles.</summary>
            public float StandOffset;

            /// <summary>Whether the last <see cref="Show"/> saw this walker; the unseen are recycled.</summary>
            public bool Seen;

            /// <summary>Whether the walker has a path yet.</summary>
            public bool Planned;
        }

        /// <summary>The transform the walkers hang under.</summary>
        private readonly Transform _parent;

        /// <summary>Plans walks.</summary>
        private readonly WalkPlanner _planner;

        /// <summary>The walkers by key: couriers by station and slot, trips by trip id.</summary>
        private readonly Dictionary<long, Entry> _entries = new Dictionary<long, Entry>();

        /// <summary>The same walkers in a list, to visit them every frame without an enumerator.</summary>
        private readonly List<Entry> _list = new List<Entry>();

        /// <summary>Views no walker uses now, ready for the next one.</summary>
        private readonly Stack<WalkerView> _poolCouriers = new Stack<WalkerView>();

        /// <summary>Views of trip workers no walker uses now.</summary>
        private readonly Stack<WalkerView> _poolWorkers = new Stack<WalkerView>();

        /// <summary>The carry models found so far by item id; null when the art has none for the item.</summary>
        private readonly Dictionary<string, GameObject?> _loadModels = new Dictionary<string, GameObject?>();

        /// <summary>The courier model, or null (capsules are shown then).</summary>
        private readonly GameObject? _courierModel;

        /// <summary>The worker model, or null.</summary>
        private readonly GameObject? _workerModel;

        /// <summary>The material of a load that has no model.</summary>
        private readonly Material _fallbackMaterial;

        /// <summary>Entries to recycle after a <see cref="Show"/>, reused.</summary>
        private readonly List<Entry> _unseen = new List<Entry>();

        /// <summary>Whether the planner read the map for the city now being shown.</summary>
        private bool _planning;

        /// <summary>Whether <see cref="Destroy"/> ran.</summary>
        private bool _destroyed;

        /// <summary>Creates the walkers' manager.</summary>
        /// <param name="parent">The transform the walkers hang under.</param>
        /// <param name="content">The game content, which the paths need.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public CityWalkers(Transform parent, GameContent content)
        {
            _parent = parent != null ? parent : throw new ArgumentNullException(nameof(parent));
            _planner = new WalkPlanner(content ?? throw new ArgumentNullException(nameof(content)));
            _courierModel = Resources.Load<GameObject>(ModelFolder + CourierModelName);
            _workerModel = Resources.Load<GameObject>(ModelFolder + WorkerModelName);
            _fallbackMaterial = PlaceholderArt.CreateMaterial(new Color(0.55f, 0.38f, 0.2f));
        }

        /// <summary>The number of walkers shown.</summary>
        public int Count => _list.Count;

        /// <summary>Whether a courier's walker is on a walk, for tests; false when there is none.</summary>
        /// <param name="stationId">The station.</param>
        /// <param name="slot">The courier's slot.</param>
        /// <returns>True while it walks.</returns>
        public bool IsCourierWalking(int stationId, int slot)
        {
            return _entries.TryGetValue(CourierKey(stationId, slot), out Entry? entry) && entry.Walking;
        }

        /// <summary>The view of a courier, or null, for tests.</summary>
        /// <param name="stationId">The station.</param>
        /// <param name="slot">The courier's slot.</param>
        /// <returns>The view.</returns>
        public WalkerView? CourierView(int stationId, int slot)
        {
            return _entries.TryGetValue(CourierKey(stationId, slot), out Entry? entry) ? entry.View : null;
        }

        /// <summary>The view of a trip's worker, or null, for tests.</summary>
        /// <param name="tripId">The trip.</param>
        /// <returns>The view.</returns>
        public WalkerView? TripView(int tripId)
        {
            return _entries.TryGetValue(TripKey(tripId), out Entry? entry) ? entry.View : null;
        }

        /// <summary>
        /// Makes the walkers match a city. A walker whose walk is the same as before keeps its path; a new walk gets a new path (the map is read
        /// once for all of them); a walker that is gone goes back to the pool.
        /// </summary>
        /// <param name="city">The displayed city.</param>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> is null.</exception>
        public void Show(CityState city)
        {
            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (_destroyed)
            {
                return;
            }

            _planning = false;
            for (int i = 0; i < _list.Count; i++)
            {
                _list[i].Seen = false;
            }

            IReadOnlyList<Courier> couriers = city.Couriers;
            for (int i = 0; i < couriers.Count; i++)
            {
                ShowCourier(city, couriers[i]);
            }

            IReadOnlyList<Trip> trips = city.Trips;
            for (int i = 0; i < trips.Count; i++)
            {
                ShowTrip(city, trips[i]);
            }

            _unseen.Clear();
            for (int i = 0; i < _list.Count; i++)
            {
                if (!_list[i].Seen)
                {
                    _unseen.Add(_list[i]);
                }
            }

            for (int i = 0; i < _unseen.Count; i++)
            {
                Recycle(_unseen[i]);
            }
        }

        /// <summary>
        /// Places every walker for the display time and moves the walkers' legs on. Allocates nothing.
        /// </summary>
        /// <param name="deltaSeconds">The frame time in real seconds.</param>
        /// <param name="nowSeconds">The display city clock in seconds, with its fraction (<c>DisplayClock.PreciseNow</c>).</param>
        public void Tick(float deltaSeconds, double nowSeconds)
        {
            for (int i = 0; i < _list.Count; i++)
            {
                Entry entry = _list[i];
                if (!entry.Walking)
                {
                    entry.Path.Sample(0f, out float sx, out float sz, out _, out _);
                    entry.View.Place(sx + entry.StandOffset, sz, 0, 0);
                    entry.View.Stand();
                    continue;
                }

                double span = Math.Max(1L, entry.ArrivesAt - entry.DepartedAt);
                float fraction = (float)((nowSeconds - entry.DepartedAt) / span);
                entry.Path.Sample(fraction, out float x, out float z, out int dx, out int dz);
                entry.View.Place(x, z, dx, dz);
                entry.View.Walk(deltaSeconds);
            }
        }

        /// <summary>Destroys every view and the material. Safe to call twice.</summary>
        public void Destroy()
        {
            if (_destroyed)
            {
                return;
            }

            _destroyed = true;
            for (int i = 0; i < _list.Count; i++)
            {
                _list[i].View.Destroy();
            }

            foreach (WalkerView view in _poolCouriers)
            {
                view.Destroy();
            }

            foreach (WalkerView view in _poolWorkers)
            {
                view.Destroy();
            }

            _list.Clear();
            _entries.Clear();
            _poolCouriers.Clear();
            _poolWorkers.Clear();
            PlaceholderArt.Destroy(_fallbackMaterial);
        }

        /// <summary>The key of a courier.</summary>
        private static long CourierKey(int stationId, int slot) => ((long)stationId << 16) | (uint)slot;

        /// <summary>The key of a trip (negative, so it never meets a courier's).</summary>
        private static long TripKey(int tripId) => -1L - tripId;

        /// <summary>Shows one courier: standing at its station, or on its walk.</summary>
        private void ShowCourier(CityState city, Courier courier)
        {
            if (!city.TryGetBuilding(courier.StationId, out Building? station))
            {
                return;
            }

            Entry entry = EntryFor(CourierKey(courier.StationId, courier.Slot), true);
            entry.Seen = true;
            bool walking = (courier.State == CourierState.Walking || courier.State == CourierState.Returning)
                && courier.DepartedAt.HasValue && courier.ArrivesAt.HasValue;
            bool same = entry.Planned && entry.State == courier.State && entry.ToStop == courier.ToStop
                && (walking
                    ? entry.Walking && entry.DepartedAt == courier.DepartedAt!.Value && entry.ArrivesAt == courier.ArrivesAt!.Value
                    : !entry.Walking);
            if (!same)
            {
                entry.Planned = true;
                entry.State = courier.State;
                entry.ToStop = courier.ToStop;
                entry.Walking = false;
                entry.StandOffset = (courier.Slot - 1) * StandingSpacing;
                WalkPath? path = walking ? PlanCourierWalk(city, courier, station) : null;
                if (path != null)
                {
                    entry.Path = path;
                    entry.Walking = true;
                    entry.DepartedAt = courier.DepartedAt!.Value;
                    entry.ArrivesAt = courier.ArrivesAt!.Value;
                }
                else
                {
                    entry.Path = WalkPath.Standing(station.X, station.Y);
                }
            }

            ShowLoad(entry, courier.Loads.Count > 0 ? courier.Loads[0].Item.Value : null);
        }

        /// <summary>Shows one trip's worker on its walk from the source to the target.</summary>
        private void ShowTrip(CityState city, Trip trip)
        {
            if (!city.TryGetBuilding(trip.SourceId, out Building? source) || !city.TryGetBuilding(trip.TargetId, out Building? target))
            {
                return;
            }

            Entry entry = EntryFor(TripKey(trip.Id), false);
            entry.Seen = true;
            if (!(entry.Planned && entry.DepartedAt == trip.DepartedAt && entry.ArrivesAt == trip.ArrivesAt))
            {
                entry.Planned = true;
                EnsurePlanner(city);
                entry.Path = _planner.Path(source, target);
                entry.Walking = true;
                entry.DepartedAt = trip.DepartedAt;
                entry.ArrivesAt = trip.ArrivesAt;
            }

            ShowLoad(entry, trip.Item.Value);
        }

        /// <summary>
        /// The path of a courier's walk. Its start is not in the snapshot, so it is worked out from the route: the stop before the one it walks
        /// to; for the first stop, the station or the last stop, whichever gives the walk's length; for a walk home, the stop whose walk to the
        /// station has the walk's length (the last when none does). Null when the route is gone.
        /// </summary>
        private WalkPath? PlanCourierWalk(CityState city, Courier courier, Building station)
        {
            if (!city.TryGetRoute(courier.StationId, courier.Slot, out Route? route))
            {
                return null;
            }

            EnsurePlanner(city);
            long length = courier.ArrivesAt!.Value - courier.DepartedAt!.Value;
            IReadOnlyList<RouteStop> stops = route.Stops;
            if (courier.State == CourierState.Returning)
            {
                Building? from = null;
                for (int i = 0; i < stops.Count; i++)
                {
                    if (city.TryGetBuilding(stops[i].BuildingId, out Building? candidate))
                    {
                        if (_planner.Seconds(candidate, station) == length)
                        {
                            return _planner.Path(candidate, station);
                        }

                        from = candidate;
                    }
                }

                return from == null ? null : _planner.Path(from, station);
            }

            if (courier.ToStop < 1 || courier.ToStop > stops.Count || !city.TryGetBuilding(stops[courier.ToStop - 1].BuildingId, out Building? target))
            {
                return null;
            }

            Building? origin;
            if (courier.ToStop >= 2)
            {
                city.TryGetBuilding(stops[courier.ToStop - 2].BuildingId, out origin);
            }
            else
            {
                city.TryGetBuilding(stops[stops.Count - 1].BuildingId, out Building? last);
                origin = station;
                if (last != null && _planner.Seconds(station, target) != length && _planner.Seconds(last, target) == length)
                {
                    origin = last;
                }
            }

            return origin == null ? null : _planner.Path(origin, target);
        }

        /// <summary>Reads the map into the planner once per <see cref="Show"/>, the first time a path is needed.</summary>
        private void EnsurePlanner(CityState city)
        {
            if (!_planning)
            {
                _planner.Load(city);
                _planning = true;
            }
        }

        /// <summary>Makes the view show a load of an item, or none.</summary>
        private void ShowLoad(Entry entry, string? item)
        {
            if (string.Equals(entry.View.LoadItem, item, StringComparison.Ordinal))
            {
                return;
            }

            GameObject? model = null;
            if (item != null && !_loadModels.TryGetValue(item, out model))
            {
                model = Resources.Load<GameObject>(ModelFolder + CarryPrefix + item);
                _loadModels.Add(item, model);
            }

            entry.View.SetLoad(item, model, _fallbackMaterial);
        }

        /// <summary>The walker with a key: the one shown already, or a new one from the pool.</summary>
        private Entry EntryFor(long key, bool courier)
        {
            if (_entries.TryGetValue(key, out Entry? existing))
            {
                return existing;
            }

            Stack<WalkerView> pool = courier ? _poolCouriers : _poolWorkers;
            WalkerView view;
            if (pool.Count > 0)
            {
                view = pool.Pop();
            }
            else
            {
                view = new WalkerView(courier ? "Courier" : "Worker", courier ? _courierModel : _workerModel, _parent);
            }

            view.SetActive(true);
            var entry = new Entry { Key = key, IsCourier = courier, View = view, Path = WalkPath.Standing(0, 0) };
            _entries.Add(key, entry);
            _list.Add(entry);
            return entry;
        }

        /// <summary>Takes a walker that has gone off the map and puts its view back in the pool.</summary>
        private void Recycle(Entry entry)
        {
            _entries.Remove(entry.Key);
            _list.Remove(entry);
            entry.View.SetLoad(null, null, null);
            entry.View.Stand();
            entry.View.SetActive(false);
            (entry.IsCourier ? _poolCouriers : _poolWorkers).Push(entry.View);
        }
    }
}
