#nullable enable
using System;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Display
{
    /// <summary>
    /// Progress bars and countdowns for the player's screens. <b>Display only, never authority.</b> They read the timer fields a settled
    /// city holds and take the clock to show as an argument, so a client can run ahead of the last server snapshot; the next snapshot
    /// replaces whatever they showed. Everything is whole seconds or permille (0 to 1000), rounded down, with no floating point.
    /// </summary>
    public static class ProgressDisplay
    {
        /// <summary>
        /// How far a building's current job has come at <paramref name="clock"/>, in permille. A running cycle counts the worker-seconds done:
        /// the labor still open at the last worker change, <see cref="Building.LaborRemaining"/>, less the seconds since
        /// <see cref="Building.SegmentStartedAt"/> times the assigned workers, out of <see cref="Building.CycleLabor"/>; so a frozen cycle
        /// (no workers) stays where it is, and more workers make the bar move faster. Construction and upgrade count the seconds of the
        /// phase out of its length.
        /// </summary>
        /// <param name="building">The building.</param>
        /// <param name="clock">The city-clock second to show; a value before the job started shows 0.</param>
        /// <returns>0 to 1000; 0 for an idle building; 1000 once the job's end has passed.</returns>
        /// <exception cref="ArgumentNullException">The building is null.</exception>
        public static int ProgressPermille(Building building, long clock)
        {
            if (building is null)
            {
                throw new ArgumentNullException(nameof(building));
            }

            switch (building.Phase)
            {
                case BuildingPhase.Constructing:
                case BuildingPhase.Upgrading:
                    return TimePermille(building.PhaseStartedAt, building.PhaseEndsAt, clock);
                case BuildingPhase.Producing:
                    return CyclePermille(building, clock);
                default:
                    return 0;
            }
        }

        /// <summary>
        /// The seconds until the building's job ends at <paramref name="clock"/>: the end of the cycle, construction or upgrade.
        /// </summary>
        /// <param name="building">The building.</param>
        /// <param name="clock">The city-clock second to count from.</param>
        /// <returns>
        /// The seconds, at least 0 (0 when the end has passed and the next snapshot hasn't arrived yet); null when nothing is scheduled: an
        /// idle building or a frozen cycle (no workers).
        /// </returns>
        /// <exception cref="ArgumentNullException">The building is null.</exception>
        public static long? SecondsUntilNextEvent(Building building, long clock)
        {
            if (building is null)
            {
                throw new ArgumentNullException(nameof(building));
            }

            return building.PhaseEndsAt.HasValue ? Math.Max(0, building.PhaseEndsAt.Value - clock) : null;
        }

        /// <summary>How far the running research has come at <paramref name="clock"/>, in permille of its duration.</summary>
        /// <param name="city">The city.</param>
        /// <param name="clock">The city-clock second to show; a value before the start shows 0.</param>
        /// <returns>0 to 1000; 0 when no research runs; 1000 once its end has passed.</returns>
        /// <exception cref="ArgumentNullException">The city is null.</exception>
        public static int ResearchProgressPermille(CityState city, long clock)
        {
            if (city is null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            ActiveResearch? research = city.ActiveResearch;
            return research is null ? 0 : TimePermille(research.StartedAt, research.EndsAt, clock);
        }

        /// <summary>
        /// How far a running cycle is, in permille of its labor: the labor done before the current segment plus what its workers did since, out of
        /// <see cref="Building.CycleLabor"/>. A frozen cycle keeps its value because 0 workers add nothing.
        /// </summary>
        private static int CyclePermille(Building building, long clock)
        {
            // Past the end the bar is full. Checking it first also keeps the product below from growing with a far-away clock.
            if (building.PhaseEndsAt.HasValue && clock >= building.PhaseEndsAt.Value)
            {
                return IntegerMath.PermilleScale;
            }

            long worked = checked(Math.Max(0, clock - building.SegmentStartedAt) * building.AssignedWorkers);
            long remaining = Math.Max(0, building.LaborRemaining - worked);
            return PermilleOf(building.CycleLabor - remaining, building.CycleLabor);
        }

        /// <summary>How far a timed job (construction, upgrade, research) is, in permille of its seconds; a job with no length or past its end is full.</summary>
        private static int TimePermille(long startedAt, long? endsAt, long clock)
        {
            long end = endsAt ?? startedAt;
            // A job with no length (a restored city may have one) is done; the division below needs a length above 0.
            if (end <= startedAt || clock >= end)
            {
                return IntegerMath.PermilleScale;
            }

            return PermilleOf(Math.Max(0, clock - startedAt), end - startedAt);
        }

        /// <summary>The part out of the whole in permille, rounded down; the part is between 0 and the whole.</summary>
        private static int PermilleOf(long part, long whole) =>
            (int)IntegerMath.FloorDiv(checked(part * IntegerMath.PermilleScale), whole);
    }
}
