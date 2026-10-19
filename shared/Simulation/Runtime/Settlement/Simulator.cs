#nullable enable
using System;
using System.Collections.Generic;
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
        /// Advances the city by <paramref name="elapsedSeconds"/> (<c>04</c> section 7). At the clock and at every event time up to the target,
        /// in this order: the research that is due completes, then the buildings that are due complete in ascending id order, then idle
        /// producers start cycles in ascending id order, pass after pass until one starts nothing, but only while the time is within
        /// <see cref="GameRules.MaxOfflineProductionSeconds"/> of the start (the dormant tail). Timers run for the whole window, and the clock always moves by the full elapsed time. A zero-length
        /// advance only starts the cycles that can start at the clock; every mutation is followed by one.
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
            long productionUntil = checked(start + content.Rules.MaxOfflineProductionSeconds);
            long? firstDueAt = NextDueAt(city);
            if (firstDueAt < start)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant($"A timer of the city is due at {firstDueAt}, before its clock {start}: the city is not valid."));
            }

            var report = new SettlementReportBuilder();
            long time = start;

            // Each pass completes what is due, then starts, so the order of 04 section 6 holds at the clock too: a canonical city has nothing
            // due there, but a restored one may. Every event a source completes is gone afterwards, and every new timer ends at least 1 second
            // later (durations and labor are at least 1, content rule 1), so each pass moves on in time and the loop ends.
            while (true)
            {
                for (int index = 0; index < _eventSources.Count; index++)
                {
                    _eventSources[index].CompleteDue(city, content, time, report);
                }

                if (time <= productionUntil)
                {
                    StartEligible(city, content, time, report);
                }

                long? next = NextDueAt(city);
                if (!next.HasValue || next.Value > target)
                {
                    break;
                }

                time = next.Value;
            }

            city.AdvanceClockTo(target);
            return report.Build(elapsed, Math.Min(elapsed, content.Rules.MaxOfflineProductionSeconds));
        }

        /// <summary>
        /// The start rule (<c>04</c> section 6): passes in ascending id order until a pass starts nothing. A start takes inputs, which can make
        /// room under a cap for a lower id that the pass has already skipped (CR-0001); the next pass starts it in the same second. Each pass
        /// starts at least one building or ends the loop, and a building starts at most once (it is then producing), so the loop ends.
        /// </summary>
        private static void StartEligible(CityState city, GameContent content, long time, SettlementReportBuilder report)
        {
            while (StartPass(city, content, time, report))
            {
            }
        }

        /// <summary>
        /// One pass of the start rule in ascending id order, each start fully applied before the next, so a lower id gets scarce inputs and
        /// output space first: an idle producer with at least 1 worker, every input in stock and room for every capped output
        /// (stock + reserved + quantity ≤ cap) takes its inputs and starts a cycle with the labor of now (research reductions included).
        /// Returns whether any building started.
        /// </summary>
        private static bool StartPass(CityState city, GameContent content, long time, SettlementReportBuilder report)
        {
            bool started = false;
            IReadOnlyList<Building> buildings = city.Buildings;
            for (int index = 0; index < buildings.Count; index++)
            {
                Building building = buildings[index];
                if (building.Phase != BuildingPhase.Idle || building.AssignedWorkers < 1)
                {
                    continue;
                }

                RecipeDefinition? recipe = CityQueries.DefinitionOf(content, building.TypeId).GetLevel(building.Level).Recipe;
                if (recipe is null
                    || CityQueries.TryFindMissingInput(city, recipe, out _)
                    || CityQueries.TryFindFullOutput(city, content, recipe, out _))
                {
                    continue;
                }

                // The outputs are reserved by the Producing phase itself: reservations are derived, never stored.
                city.RemoveItems(recipe.Inputs);
                report.AddConsumed(recipe.Inputs);
                building.StartCycle(time, CityQueries.EffectiveLabor(city, content, building.TypeId, building.Level));
                started = true;
            }

            return started;
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
