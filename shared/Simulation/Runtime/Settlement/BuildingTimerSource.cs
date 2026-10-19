#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Settlement
{
    /// <summary>
    /// The buildings' phase timers (<c>core-instructions/04-simulation-model.md</c>, section 6, completion rule). Buildings due at the same time
    /// complete in ascending id order, each fully before the next, so a house that completes first provides workers to the auto-staffing of a
    /// producer with a higher id. A frozen cycle has no end and never completes. The source lives for one advance: it takes the due buildings
    /// from that advance's <see cref="BuildingTimerQueue"/>, to which the start rule adds every cycle it starts, and a completed cycle's
    /// outputs leave that advance's <see cref="Reservations"/>.
    /// </summary>
    internal sealed class BuildingTimerSource : IEventSource
    {
        private readonly BuildingTimerQueue _timers;
        private readonly Reservations _reservations;

        public BuildingTimerSource(BuildingTimerQueue timers, Reservations reservations)
        {
            _timers = timers;
            _reservations = reservations;
        }

        /// <inheritdoc/>
        public long? NextDueAt(CityState city) => _timers.Count > 0 ? _timers.PeekEndsAt() : (long?)null;

        /// <inheritdoc/>
        public void CompleteDue(CityState city, GameContent content, long time, SettlementReportBuilder report)
        {
            IReadOnlyList<Building> buildings = city.Buildings;
            while (_timers.Count > 0 && _timers.PeekEndsAt() == time)
            {
                Building building = buildings[_timers.Pop()];
                if (building.PhaseEndsAt != time)
                {
                    throw new InvalidOperationException(
                        FormattableString.Invariant($"Building {building.Id} ends at {building.PhaseEndsAt}, but its timer in the queue at {time}."));
                }

                Complete(city, content, building, report, _reservations);
            }
        }

        private static void Complete(CityState city, GameContent content, Building building, SettlementReportBuilder report, Reservations reservations)
        {
            switch (building.Phase)
            {
                case BuildingPhase.Constructing:
                    // Auto-staffing: min(free, MaxWorkers). A house's level takes no workers (content rule 3), so it gets 0.
                    int maxWorkers = CityQueries.DefinitionOf(content, building.TypeId).GetLevel(building.Level).MaxWorkers;
                    building.CompleteConstruction((int)Math.Min(CityQueries.FreeWorkers(city, content), maxWorkers));
                    report.AddCompletedBuilding(building.Id);
                    break;
                case BuildingPhase.Upgrading:
                    building.CompleteUpgrade();
                    report.AddCompletedBuilding(building.Id);
                    break;
                case BuildingPhase.Producing:
                    // The reserved outputs move to stock; caps limit only starts. An upgrade ends a running cycle, so the level is the start's.
                    IReadOnlyList<ItemQuantity> outputs = CityQueries.RecipeOf(content, building.TypeId, building.Level).Outputs;
                    city.AddItems(outputs);
                    reservations.Remove(outputs);
                    report.AddProduced(outputs);
                    building.CompleteCycle();
                    break;
                default:
                    throw new InvalidOperationException(FormattableString.Invariant($"Building {building.Id} is {building.Phase} but has an end."));
            }
        }
    }
}
