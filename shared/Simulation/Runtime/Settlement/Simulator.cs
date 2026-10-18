#nullable enable
using System;
using System.Collections.ObjectModel;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Settlement
{
    /// <summary>
    /// Computes what happens to a city while time passes (<c>core-instructions/04-simulation-model.md</c>, sections 2, 3, 5, 6, 7 and 11): a
    /// deterministic event simulation from the city clock to the clock plus the elapsed seconds. The server runs it to settle a city on every
    /// request; the client runs the same code for display only.
    /// </summary>
    public static class Simulator
    {
        // The order at the same second (04 section 6): research completion, then building completions by id. The mailbox deliveries of
        // 04 section 13 will go between the two. The sources hold no state, and the read-only collection can't be changed.
        private static readonly ReadOnlyCollection<IEventSource> _eventSources =
            Array.AsReadOnly(new IEventSource[] { new ResearchTimerSource(), new BuildingTimerSource() });

        /// <summary>
        /// Advances the city by <paramref name="elapsedSeconds"/>: moves from event to event in time order (research completion, then building
        /// completions in ascending id order), and sets the clock to the end of the window. Timers run for the whole window. The clock always
        /// moves by the full elapsed time.
        /// </summary>
        /// <param name="city">The city; it changes in place.</param>
        /// <param name="content">The game content the city was built with.</param>
        /// <param name="elapsedSeconds">The seconds to advance. 0 is a zero-length advance; a value below 0 counts as 0.</param>
        /// <returns>What happened during the window.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> or <paramref name="content"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// A timer of the city is due before its clock, or the city names a building type the content doesn't know. Nothing has changed when
        /// a timer is due before the clock.
        /// </exception>
        public static SettlementReport Advance(CityState city, GameContent content, long elapsedSeconds)
        {
            if (city is null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            long start = city.Clock;
            long elapsed = Math.Max(elapsedSeconds, 0);
            long target = checked(start + elapsed);
            long? next = NextDueAt(city);
            if (next < start)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant($"A timer of the city is due at {next}, before its clock {start}: the city is not valid."));
            }

            var report = new SettlementReportBuilder();

            // Every event a source completes is gone afterwards, and every new timer ends at least 1 second later (durations and labor are at
            // least 1, content rule 1), so each pass moves on in time and the loop ends.
            while (next.HasValue && next.Value <= target)
            {
                long time = next.Value;
                for (int index = 0; index < _eventSources.Count; index++)
                {
                    _eventSources[index].CompleteDue(city, content, time, report);
                }

                next = NextDueAt(city);
            }

            city.AdvanceClockTo(target);
            return report.Build(elapsed, Math.Min(elapsed, content.Rules.MaxOfflineProductionSeconds));
        }

        private static long? NextDueAt(CityState city)
        {
            long? next = null;
            for (int index = 0; index < _eventSources.Count; index++)
            {
                long? dueAt = _eventSources[index].NextDueAt(city);
                if (dueAt.HasValue && (!next.HasValue || dueAt.Value < next.Value))
                {
                    next = dueAt;
                }
            }

            return next;
        }
    }
}
