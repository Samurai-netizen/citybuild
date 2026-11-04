#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Settlement
{
    /// <summary>
    /// The delivery rule (<c>core-instructions/04-simulation-model.md</c>, sections 6 and 16): what a walker that arrives with goods hands to a
    /// building. Used by trip arrivals and (part F) couriers' puts, inside an advance.
    /// </summary>
    internal static class Deliveries
    {
        /// <summary>
        /// Delivers up to <paramref name="quantity"/> of a good to the building at <paramref name="buildingIndex"/> at <paramref name="time"/>:
        /// first to the building's own waiting project (a construction site, an upgrade waiting for goods), then to the research waiting there
        /// when it is the science building, each at most what it still needs and used up at once (<c>delivered</c>); then into its store, at
        /// most its room (<c>moved</c>). A project whose needs become empty starts at <paramref name="time"/>: the construction timer runs for the
        /// build seconds, the upgrade starts (a running cycle ends and its inputs are refunded into the store), the research timer runs. Returns
        /// what is left, which the walker keeps or brings back.
        /// </summary>
        public static long Deliver(
            CityState city, GameContent content, int buildingIndex, ItemId item, long quantity, long time, SettlementReportBuilder report, BuildingTimerQueue timers)
        {
            Building building = city.Buildings[buildingIndex];
            long left = quantity;

            // The building's own project first (BL-20261102-07 decision 37), then the research waiting at the science building.
            long taken = building.FillNeed(item, left);
            if (taken > 0)
            {
                report.AddDelivered(item, taken);
                left = checked(left - taken);
                if (building.Needs.Count == 0)
                {
                    StartBuildingProject(content, building, buildingIndex, time, report, timers);
                }
            }

            ActiveResearch? research = city.ActiveResearch;
            if (left > 0 && research is not null && CityQueries.ResearchNeedAt(city, content, building, item) > 0)
            {
                taken = research.FillNeed(item, left);
                report.AddDelivered(item, taken);
                left = checked(left - taken);
                if (!research.IsWaiting)
                {
                    content.TryGetResearch(research.Id, out ResearchDefinition? node);
                    research.StartTimer(time, checked(time + node!.DurationSeconds));
                }
            }

            long moved = Math.Min(left, CityQueries.Room(city, content, building, item));
            if (moved > 0)
            {
                building.AddToStore(item, moved);
                report.AddMoved(item, moved);
                left = checked(left - moved);
            }

            return left;
        }

        /// <summary>
        /// Starts the project whose last good just arrived (<c>04</c> section 16): a construction site's build timer, or the waiting upgrade
        /// (<c>04</c> section 8, starting an upgrade), and puts its timer into the advance's queue in place of a running cycle's.
        /// </summary>
        private static void StartBuildingProject(
            GameContent content, Building building, int buildingIndex, long time, SettlementReportBuilder report, BuildingTimerQueue timers)
        {
            if (building.Phase == BuildingPhase.Constructing)
            {
                building.StartConstructionTimer(time, checked(time + CityQueries.DefinitionOf(content, building.TypeId).BuildSeconds));
            }
            else
            {
                IReadOnlyList<ItemQuantity>? refunded = ProjectRules.StartUpgrade(content, building, time);
                if (refunded is not null)
                {
                    report.AddRefunded(refunded);
                }
            }

            timers.Set(buildingIndex, building.PhaseEndsAt!.Value);
        }
    }
}
