#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Settlement
{
    /// <summary>
    /// The building timers of one advance in a binary min-heap, earliest end first and ascending id within a second (the order of <c>04</c>
    /// section 6), so the next due time and the buildings due then are found without a scan of every building (the worst case of <c>04</c>
    /// section 12 has 28,800 event times in 8 hours). It holds every building with a <see cref="Building.PhaseEndsAt"/> by its index in
    /// <see cref="CityState.Buildings"/>, which is in id order and doesn't change during an advance. Inside an advance a timer ends only when
    /// it completes, and a new one begins only when a cycle starts; mutations run between advances, and the queue is made new for each one.
    /// </summary>
    internal sealed class BuildingTimerQueue
    {
        private readonly long[] _endsAt;
        private readonly int[] _indexes;
        private int _count;

        private BuildingTimerQueue(int capacity)
        {
            _endsAt = new long[capacity];
            _indexes = new int[capacity];
        }

        /// <summary>The number of timers in the queue.</summary>
        public int Count => _count;

        /// <summary>The queue of every building of the city that has an end (constructing, upgrading, or producing with workers).</summary>
        public static BuildingTimerQueue Of(CityState city)
        {
            IReadOnlyList<Building> buildings = city.Buildings;
            var queue = new BuildingTimerQueue(buildings.Count);
            for (int index = 0; index < buildings.Count; index++)
            {
                long? endsAt = buildings[index].PhaseEndsAt;
                if (endsAt.HasValue)
                {
                    queue.Add(index, endsAt.Value);
                }
            }

            return queue;
        }

        /// <summary>The earliest end in the queue. The queue must not be empty.</summary>
        public long PeekEndsAt() => _count > 0 ? _endsAt[0] : throw new InvalidOperationException("The timer queue is empty.");

        /// <summary>Adds the timer of the building at <paramref name="index"/>, which ends at <paramref name="endsAt"/>.</summary>
        public void Add(int index, long endsAt)
        {
            if (_count == _indexes.Length)
            {
                throw new InvalidOperationException("Every building already has a timer in the queue.");
            }

            int slot = _count++;
            while (slot > 0)
            {
                int parent = (slot - 1) / 2;
                if (!IsEarlier(endsAt, index, _endsAt[parent], _indexes[parent]))
                {
                    break;
                }

                _endsAt[slot] = _endsAt[parent];
                _indexes[slot] = _indexes[parent];
                slot = parent;
            }

            _endsAt[slot] = endsAt;
            _indexes[slot] = index;
        }

        /// <summary>Removes the earliest timer (the lowest index among the earliest ends) and returns its building index.</summary>
        public int Pop()
        {
            if (_count == 0)
            {
                throw new InvalidOperationException("The timer queue is empty.");
            }

            int first = _indexes[0];
            _count--;
            long lastEndsAt = _endsAt[_count];
            int lastIndex = _indexes[_count];
            int slot = 0;
            while (true)
            {
                int child = (2 * slot) + 1;
                if (child >= _count)
                {
                    break;
                }

                if (child + 1 < _count && IsEarlier(_endsAt[child + 1], _indexes[child + 1], _endsAt[child], _indexes[child]))
                {
                    child++;
                }

                if (!IsEarlier(_endsAt[child], _indexes[child], lastEndsAt, lastIndex))
                {
                    break;
                }

                _endsAt[slot] = _endsAt[child];
                _indexes[slot] = _indexes[child];
                slot = child;
            }

            _endsAt[slot] = lastEndsAt;
            _indexes[slot] = lastIndex;
            return first;
        }

        private static bool IsEarlier(long endsAt, int index, long otherEndsAt, int otherIndex) =>
            endsAt < otherEndsAt || (endsAt == otherEndsAt && index < otherIndex);
    }
}
