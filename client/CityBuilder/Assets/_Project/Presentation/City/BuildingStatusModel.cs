#nullable enable

using System;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Display;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// Works out what a building's status shows (M0-50) from the displayed city and the content, with the engine's own helpers: the ring from
    /// the building's phase and <see cref="ProgressDisplay.ProgressPermille"/> at the displayed city's clock (the same clock as the
    /// <c>DisplayClock</c> gives, since <c>DisplayCity</c> advanced the city to it), the bubble and the stalled look from
    /// <see cref="StallReason"/>, the pips from the assigned workers. Pure code with no Unity objects; it decides nothing and sends nothing.
    /// </summary>
    public static class BuildingStatusModel
    {
        /// <summary>
        /// The status of one building of <paramref name="city"/>. A building whose type or level the content does not know has its ring and its
        /// pips but no bubble, never an exception: the display city already shows such a city as it is, and this runs every second.
        /// </summary>
        /// <param name="city">The displayed city; its clock is the clock to show.</param>
        /// <param name="content">The game content.</param>
        /// <param name="building">A building of <paramref name="city"/>.</param>
        /// <returns>The status.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static BuildingStatus Describe(CityState city, GameContent content, Building building)
        {
            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (building == null)
            {
                throw new ArgumentNullException(nameof(building));
            }

            StatusRingKind ring = RingOf(building.Phase);
            int progress = ring == StatusRingKind.None ? 0 : ProgressDisplay.ProgressPermille(building, city.Clock);

            StatusBubbleKind bubble = StatusBubbleKind.None;
            ItemId item = default;
            if (content.TryGetBuilding(building.TypeId, out BuildingDefinition? definition) && building.Level <= definition.MaxLevel)
            {
                StallReason? reason = StallReason.Of(city, content, building);
                if (reason != null)
                {
                    bubble = BubbleOf(reason.Kind);
                    item = reason.Item;
                }
            }

            return new BuildingStatus(ring, progress, bubble, item, building.AssignedWorkers);
        }

        /// <summary>The ring a phase gets: only a timed job has one.</summary>
        private static StatusRingKind RingOf(BuildingPhase phase)
        {
            switch (phase)
            {
                case BuildingPhase.Constructing:
                    return StatusRingKind.Constructing;
                case BuildingPhase.Upgrading:
                    return StatusRingKind.Upgrading;
                case BuildingPhase.Producing:
                    return StatusRingKind.Producing;
                default:
                    return StatusRingKind.None;
            }
        }

        /// <summary>The bubble a stall kind gets; a building with no recipe (a cottage) is not stalled in the player's eyes.</summary>
        private static StatusBubbleKind BubbleOf(StallKind kind)
        {
            switch (kind)
            {
                case StallKind.NoWorkers:
                    return StatusBubbleKind.NoWorkers;
                case StallKind.MissingInput:
                    return StatusBubbleKind.MissingInput;
                case StallKind.StorageFull:
                    return StatusBubbleKind.StorageFull;
                default:
                    return StatusBubbleKind.None;
            }
        }
    }
}
