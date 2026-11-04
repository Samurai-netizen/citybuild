#nullable enable
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
    internal sealed class BuildingTimerQueue : TimerHeap
    {
        /// <summary>Creates an empty queue with room for one timer per building, so it never grows during the advance.</summary>
        private BuildingTimerQueue(int capacity)
            : base(capacity)
        {
        }

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
    }
}
