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
    /// producer with a higher id. A frozen cycle has no end and never completes.
    /// </summary>
    internal sealed class BuildingTimerSource : IEventSource
    {
        /// <inheritdoc/>
        public long? NextDueAt(CityState city)
        {
            long? next = null;
            IReadOnlyList<Building> buildings = city.Buildings;
            for (int index = 0; index < buildings.Count; index++)
            {
                long? endsAt = buildings[index].PhaseEndsAt;
                if (endsAt.HasValue && (!next.HasValue || endsAt.Value < next.Value))
                {
                    next = endsAt;
                }
            }

            return next;
        }

        /// <inheritdoc/>
        public void CompleteDue(CityState city, GameContent content, long time, SettlementReportBuilder report, Reservations reservations)
        {
            IReadOnlyList<Building> buildings = city.Buildings;
            for (int index = 0; index < buildings.Count; index++)
            {
                Building building = buildings[index];
                if (building.PhaseEndsAt == time)
                {
                    Complete(city, content, building, report, reservations);
                }
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
