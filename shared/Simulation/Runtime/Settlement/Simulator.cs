#nullable enable
using System;
using System.Collections.Generic;
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
        /// <summary>
        /// Advances the city by <paramref name="elapsedSeconds"/> (<c>04</c> section 7). At the clock and at every event time up to the target,
        /// in this order: the research that is due completes, then the buildings that are due complete in ascending id order, then the trips
        /// that arrive deliver their goods in ascending trip id order (<c>04</c> sections 16 and 18), then idle producers start cycles in ascending id order, each on its own store, but only while the time is within
        /// <see cref="GameRules.MaxOfflineProductionSeconds"/> of the start (the dormant tail). Timers run for the whole window, and the clock always moves by the full elapsed time. A zero-length
        /// advance only starts the cycles that can start at the clock; the caller ends every city request with one, after its mutation if any.
        /// </summary>
        /// <param name="city">The city; it changes in place.</param>
        /// <param name="content">The game content the city was built with.</param>
        /// <param name="elapsedSeconds">The seconds to advance. 0 is a zero-length advance; a value below 0 counts as 0.</param>
        /// <returns>What happened during the window.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> or <paramref name="content"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// A timer or a trip of the city is due before its clock, or the city names a building type the content doesn't know. Nothing has
        /// changed when something is due before the clock.
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

            // What this advance keeps up to date instead of scanning every building at every event time (04 section 12). Making it changes
            // nothing in the city. The order of the sources is the order at the same second (04 section 6): research completion, then
            // building completions by id, then trip arrivals by trip id; the mailbox deliveries of 04 section 13 will go before the buildings.
            BuildingTimerQueue timers = BuildingTimerQueue.Of(city);
            IEventSource[] sources = { new ResearchTimerSource(), new BuildingTimerSource(timers), new TripArrivalSource(city, timers) };
            long? firstDueAt = NextDueAt(sources, city);
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
                for (int index = 0; index < sources.Length; index++)
                {
                    sources[index].CompleteDue(city, content, time, report);
                }

                if (time <= productionUntil)
                {
                    StartEligible(city, content, time, report, timers);
                }

                long? next = NextDueAt(sources, city);
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
        /// The start rule (<c>04</c> section 6), one pass in ascending id order: an idle producer with at least 1 worker, every input in its own
        /// store and room there for every output that is not the currency (<c>stock + quantity ≤</c> the effective limit; an idle building
        /// reserves nothing) takes its inputs from its store and starts a cycle with the labor of now (research reductions included). A start
        /// touches only its own store, so a start never helps or hinders another building, and one pass starts everything that can start
        /// (CR-0001's repeated passes were for the city-wide stock; decision 34 of CR-0008-T1). Every cycle it starts joins the advance's
        /// timer queue.
        /// </summary>
        private static void StartEligible(CityState city, GameContent content, long time, SettlementReportBuilder report, BuildingTimerQueue timers)
        {
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
                    || CityQueries.TryFindMissingInput(building, recipe, out _)
                    || CityQueries.TryFindFullOutput(city, content, building, recipe, out _))
                {
                    continue;
                }

                // The outputs are reserved by the Producing phase itself: reservations are derived, never stored.
                building.TakeFromStore(recipe.Inputs);
                report.AddConsumed(recipe.Inputs);
                building.StartCycle(time, CityQueries.EffectiveLabor(city, content, building.TypeId, building.Level));
                // StartCycle with at least 1 worker (checked above) always sets an end, so PhaseEndsAt has a value here.
                timers.Add(index, building.PhaseEndsAt!.Value);
            }
        }

        /// <summary>The earliest time any event source has something due, or null when no source has anything left.</summary>
        private static long? NextDueAt(IEventSource[] sources, CityState city)
        {
            long? next = null;
            for (int index = 0; index < sources.Length; index++)
            {
                long? dueAt = sources[index].NextDueAt(city);
                if (dueAt.HasValue && (!next.HasValue || dueAt.Value < next.Value))
                {
                    next = dueAt;
                }
            }

            return next;
        }
    }
}
