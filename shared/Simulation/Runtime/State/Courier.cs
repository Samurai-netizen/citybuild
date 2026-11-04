#nullable enable
using System;
using System.Collections.Generic;

namespace CityBuilder.Simulation.State
{
    /// <summary>
    /// One courier of a courier station (<c>core-instructions/04-simulation-model.md</c>, section 19): the worker in slot <see cref="Slot"/> of
    /// station <see cref="StationId"/>, which walks the slot's route in a loop. It keeps its state, the walk it is on (the stop it walks to,
    /// its departure and arrival), its stamina and its load, each part with the building it came from. A station has exactly one courier per
    /// assigned worker, in slots 1 to the count (invariant 9). Only the engine changes a courier, through the internal transitions below.
    /// </summary>
    public sealed class Courier
    {
        /// <summary>The order of <see cref="_loads"/> (<see cref="CourierLoad.Compare"/>), made once: it holds no state.</summary>
        private static readonly IComparer<CourierLoad> _loadOrder = Comparer<CourierLoad>.Create(CourierLoad.Compare);

        /// <summary>The load, sorted by good, then the building it came from; each pair once.</summary>
        private readonly List<CourierLoad> _loads;

        /// <summary>Restores a courier with every value, for example from the database.</summary>
        /// <param name="stationId">The courier station's building id, at least 1.</param>
        /// <param name="slot">The slot in the station, at least 1.</param>
        /// <param name="state">What it is doing.</param>
        /// <param name="toStop">The stop (1-based) it walks to while <see cref="CourierState.Walking"/>; 0 in every other state.</param>
        /// <param name="departedAt">When its walk started: set exactly while walking or returning, at least 0.</param>
        /// <param name="arrivesAt">When its walk ends: set exactly while walking or returning, after <paramref name="departedAt"/>.</param>
        /// <param name="stamina">Its stamina in seconds, at least 0.</param>
        /// <param name="loads">What it carries, in any order: each (good, building) once; null for nothing.</param>
        /// <exception cref="ArgumentException">The values don't form a courier; the message says which rule they break.</exception>
        public Courier(int stationId, int slot, CourierState state, int toStop, long? departedAt, long? arrivesAt, long stamina, IEnumerable<CourierLoad>? loads = null)
        {
            if (stationId < 1 || slot < 1 || stamina < 0)
            {
                throw new ArgumentException(FormattableString.Invariant($"Courier {stationId}/{slot}: the station and the slot are at least 1 and the stamina not below 0."));
            }

            bool walks = state == CourierState.Walking || state == CourierState.Returning;
            bool timesFit = walks
                ? departedAt >= 0 && arrivesAt > departedAt
                : !departedAt.HasValue && !arrivesAt.HasValue;
            bool stopFits = state == CourierState.Walking ? toStop >= 1 : toStop == 0;
            if (!timesFit || !stopFits)
            {
                throw new ArgumentException(FormattableString.Invariant(
                    $"Courier {stationId}/{slot} is {state}: a walk has a departure not below 0 and a later arrival, only a walk to a stop names it, and a courier that doesn't walk has neither."));
            }

            StationId = stationId;
            Slot = slot;
            State = state;
            ToStop = toStop;
            DepartedAt = departedAt;
            ArrivesAt = arrivesAt;
            Stamina = stamina;
            _loads = SortedLoads(loads, stationId, slot);
            Loads = _loads.AsReadOnly();
        }

        /// <summary>The courier station's building id.</summary>
        public int StationId { get; }

        /// <summary>The slot in its station, from 1; its route is the slot's.</summary>
        public int Slot { get; }

        /// <summary>What it is doing.</summary>
        public CourierState State { get; private set; }

        /// <summary>The stop (1-based) of its route it walks to while <see cref="CourierState.Walking"/>; 0 otherwise.</summary>
        public int ToStop { get; private set; }

        /// <summary>When its walk started, in city-clock seconds; null when it doesn't walk.</summary>
        public long? DepartedAt { get; private set; }

        /// <summary>When its walk ends and it arrives, in city-clock seconds; null when it doesn't walk.</summary>
        public long? ArrivesAt { get; private set; }

        /// <summary>Its stamina in seconds: walks after the absence start spend it (<c>04</c> section 2, part H).</summary>
        public long Stamina { get; private set; }

        /// <summary>What it carries, by good (ordinal), then the building each part came from.</summary>
        public IReadOnlyList<CourierLoad> Loads { get; }

        /// <summary>How many goods it carries, all goods together: what its station level's carry limit bounds.</summary>
        public long Carried
        {
            get
            {
                long carried = 0;
                for (int index = 0; index < _loads.Count; index++)
                {
                    carried = checked(carried + _loads[index].Quantity);
                }

                return carried;
            }
        }

        /// <summary>A new courier at its station with full stamina and nothing carried (a worker added to the station, <c>04</c> section 19).</summary>
        internal static Courier AtStationWith(int stationId, int slot, long stamina) =>
            new Courier(stationId, slot, CourierState.AtStation, 0, null, null, stamina);

        /// <summary>How many of the good it carries, from every building together.</summary>
        public long CarriedOf(ItemId item)
        {
            long carried = 0;
            for (int index = 0; index < _loads.Count; index++)
            {
                if (_loads[index].Item == item)
                {
                    carried = checked(carried + _loads[index].Quantity);
                }
            }

            return carried;
        }

        /// <summary>Starts a walk to stop <paramref name="toStop"/> of its route, from <paramref name="departedAt"/> to <paramref name="arrivesAt"/>.</summary>
        internal void WalkTo(int toStop, long departedAt, long arrivesAt)
        {
            if (toStop < 1 || departedAt < 0 || arrivesAt <= departedAt)
            {
                throw new InvalidOperationException(FormattableString.Invariant($"Courier {StationId}/{Slot}: a walk to stop {toStop} from {departedAt} to {arrivesAt} is not a walk."));
            }

            State = CourierState.Walking;
            ToStop = toStop;
            DepartedAt = departedAt;
            ArrivesAt = arrivesAt;
        }

        /// <summary>Ends whatever it does and puts it at its station, ready to leave (its load is the caller's business).</summary>
        internal void StandAtStation()
        {
            State = CourierState.AtStation;
            ToStop = 0;
            DepartedAt = null;
            ArrivesAt = null;
        }

        /// <summary>Ends its walk home without stamina: it rests at its station with its load until re-energized (<c>04</c> section 2, part H).</summary>
        internal void Rest()
        {
            State = CourierState.Resting;
            ToStop = 0;
            DepartedAt = null;
            ArrivesAt = null;
        }

        /// <summary>Adds goods taken from building <paramref name="fromId"/> to its load; the caller keeps the carry limit.</summary>
        internal void Load(ItemId item, int fromId, long quantity)
        {
            if (quantity < 1)
            {
                throw new InvalidOperationException(FormattableString.Invariant($"Courier {StationId}/{Slot}: a load of {quantity} is not at least 1."));
            }

            var added = new CourierLoad(item, fromId, quantity);
            int index = _loads.BinarySearch(added, _loadOrder);
            if (index >= 0)
            {
                _loads[index] = new CourierLoad(item, fromId, checked(_loads[index].Quantity + quantity));
            }
            else
            {
                _loads.Insert(~index, added);
            }
        }

        /// <summary>
        /// Takes <paramref name="quantity"/> of the good off its load, from the parts in ascending building id (CR-0008-T1 decision 47), and
        /// adds what it took to <paramref name="taken"/> in that order; what stays keeps its origin. The caller checks that it carries that much.
        /// </summary>
        internal void Unload(ItemId item, long quantity, List<CourierLoad> taken)
        {
            long left = quantity;
            int index = 0;
            while (left > 0 && index < _loads.Count)
            {
                CourierLoad part = _loads[index];
                if (part.Item != item)
                {
                    index++;
                    continue;
                }

                long share = Math.Min(left, part.Quantity);
                left -= share;
                taken.Add(share == part.Quantity ? part : new CourierLoad(item, part.FromId, share));
                if (share == part.Quantity)
                {
                    _loads.RemoveAt(index);
                }
                else
                {
                    _loads[index] = new CourierLoad(item, part.FromId, part.Quantity - share);
                    index++;
                }
            }

            if (left > 0)
            {
                throw new InvalidOperationException(FormattableString.Invariant($"Courier {StationId}/{Slot} carries less than {quantity} {item}; the caller must check it first."));
            }
        }

        /// <summary>Empties its load and returns what it carried, by good, then origin: the caller gives each part back (a returned load).</summary>
        internal CourierLoad[] TakeAllLoads()
        {
            CourierLoad[] all = _loads.ToArray();
            _loads.Clear();
            return all;
        }

        /// <summary>The restoring constructor's load, checked and sorted: no null part and each (good, building) once.</summary>
        private static List<CourierLoad> SortedLoads(IEnumerable<CourierLoad>? loads, int stationId, int slot)
        {
            var sorted = new List<CourierLoad>();
            foreach (CourierLoad load in loads ?? Array.Empty<CourierLoad>())
            {
                sorted.Add(load ?? throw new ArgumentNullException(nameof(loads), "A load part is null."));
            }

            sorted.Sort(CourierLoad.Compare);
            for (int index = 1; index < sorted.Count; index++)
            {
                if (CourierLoad.Compare(sorted[index], sorted[index - 1]) == 0)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant($"Courier {stationId}/{slot} lists {sorted[index].Item} from building {sorted[index].FromId} twice."), nameof(loads));
                }
            }

            return sorted;
        }
    }
}
