#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Settlement
{
    /// <summary>
    /// The building timers of one advance in an indexed binary min-heap, earliest end first and ascending id within a second (the order of
    /// <c>04</c> section 6), so the next due time and the buildings due then are found without a scan of every building (the worst case of
    /// <c>04</c> section 12 has thousands of event times). It holds every building with a <see cref="Building.PhaseEndsAt"/> by its index in
    /// <see cref="CityState.Buildings"/>, which is in id order and doesn't change during an advance, at most once each. Inside an advance a
    /// timer ends when it completes, a cycle's start adds one, and a delivery that starts a project sets one, which replaces a running cycle's
    /// (<c>04</c> section 16); mutations run between advances, and the queue is made new for each one.
    /// </summary>
    internal sealed class BuildingTimerQueue
    {
        /// <summary>The heap's end times: <c>_endsAt[slot]</c> belongs to <c>_indexes[slot]</c>; slot 0 holds the earliest timer.</summary>
        private readonly long[] _endsAt;
        /// <summary>The heap's building indexes in <see cref="CityState.Buildings"/>, slot by slot beside <see cref="_endsAt"/>.</summary>
        private readonly int[] _indexes;
        /// <summary>Each building's slot in the heap by its index in <see cref="CityState.Buildings"/>, or -1 when it has no timer in the queue.</summary>
        private readonly int[] _slotOf;
        /// <summary>How many slots are in use; slots from here on are free.</summary>
        private int _count;

        /// <summary>Creates an empty queue with room for one timer per building, so it never grows during the advance.</summary>
        private BuildingTimerQueue(int capacity)
        {
            _endsAt = new long[capacity];
            _indexes = new int[capacity];
            _slotOf = new int[capacity];
            for (int index = 0; index < capacity; index++)
            {
                _slotOf[index] = -1;
            }
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

        /// <summary>Adds the timer of the building at <paramref name="index"/>, which ends at <paramref name="endsAt"/>; it has none in the queue yet.</summary>
        public void Add(int index, long endsAt)
        {
            if (_slotOf[index] >= 0)
            {
                throw new InvalidOperationException(FormattableString.Invariant($"The building at index {index} already has a timer in the queue."));
            }

            int slot = _count++;
            SiftUp(slot, endsAt, index);
        }

        /// <summary>
        /// Sets the timer of the building at <paramref name="index"/> to <paramref name="endsAt"/>: adds it, or moves the one it has (an upgrade a
        /// delivery started replaces the running cycle's end).
        /// </summary>
        public void Set(int index, long endsAt)
        {
            int slot = _slotOf[index];
            if (slot < 0)
            {
                Add(index, endsAt);
            }
            else if (endsAt < _endsAt[slot])
            {
                SiftUp(slot, endsAt, index);
            }
            else
            {
                SiftDown(slot, endsAt, index);
            }
        }

        /// <summary>Removes the earliest timer (the lowest index among the earliest ends) and returns its building index.</summary>
        public int Pop()
        {
            if (_count == 0)
            {
                throw new InvalidOperationException("The timer queue is empty.");
            }

            int first = _indexes[0];
            _slotOf[first] = -1;
            _count--;
            if (_count > 0)
            {
                // The last timer refills the root's place and sinks to where it belongs.
                SiftDown(0, _endsAt[_count], _indexes[_count]);
            }

            return first;
        }

        /// <summary>
        /// Puts a timer into <paramref name="slot"/> or above it: every parent that comes later (a later end, or a higher index at the same end)
        /// moves down one level until the timer's place is found. A heap of n timers needs at most log2(n) moves.
        /// </summary>
        private void SiftUp(int slot, long endsAt, int index)
        {
            while (slot > 0)
            {
                int parent = (slot - 1) / 2;
                if (!IsEarlier(endsAt, index, _endsAt[parent], _indexes[parent]))
                {
                    break;
                }

                Move(parent, slot);
                slot = parent;
            }

            Place(slot, endsAt, index);
        }

        /// <summary>
        /// Puts a timer into <paramref name="slot"/> or below it: the earlier of the two children moves up one level until the timer fits, which
        /// keeps every parent earlier than its children.
        /// </summary>
        private void SiftDown(int slot, long endsAt, int index)
        {
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

                if (!IsEarlier(_endsAt[child], _indexes[child], endsAt, index))
                {
                    break;
                }

                Move(child, slot);
                slot = child;
            }

            Place(slot, endsAt, index);
        }

        /// <summary>Moves the timer in slot <paramref name="from"/> to slot <paramref name="to"/>, keeping its building's slot in step.</summary>
        private void Move(int from, int to)
        {
            Place(to, _endsAt[from], _indexes[from]);
        }

        /// <summary>Writes a timer into a slot and records the slot for its building.</summary>
        private void Place(int slot, long endsAt, int index)
        {
            _endsAt[slot] = endsAt;
            _indexes[slot] = index;
            _slotOf[index] = slot;
        }

        /// <summary>The heap's order: an earlier end first, and at the same end the lower building index (which is the lower id) first.</summary>
        private static bool IsEarlier(long endsAt, int index, long otherEndsAt, int otherIndex) =>
            endsAt < otherEndsAt || (endsAt == otherEndsAt && index < otherIndex);
    }
}
