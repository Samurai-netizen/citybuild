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
    /// from that advance's <see cref="BuildingTimerQueue"/>, to which the start rule adds every cycle it starts. A completed cycle's outputs
    /// go into the building's own store, the currency into the purse (<c>04</c> section 16).
    /// </summary>
    internal sealed class BuildingTimerSource : IEventSource
    {
        /// <summary>The advance's building timers; the start rule adds every cycle it starts to the same queue.</summary>
        private readonly BuildingTimerQueue _timers;

        /// <summary>Creates the source for one advance over that advance's timer queue.</summary>
        public BuildingTimerSource(BuildingTimerQueue timers)
        {
            _timers = timers;
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
                        FormattableString.Invariant($"Building {building.Id} ends at {building.PhaseEndsAt}, but the timer queue had it due at {time}."));
                }

                Complete(city, content, building, report);
            }
        }

        /// <summary>
        /// Completes one building's phase: a construction staffs the building with <c>min(free, MaxWorkers)</c> (decision 018), an upgrade
        /// raises its level, and a cycle moves its outputs from reserved to stock, even above a cap (decision 013).
        /// </summary>
        private static void Complete(CityState city, GameContent content, Building building, SettlementReportBuilder report)
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
                    // The reserved outputs move into the building's store (the start checked the room), the currency into the purse. An
                    // upgrade ends a running cycle, so the level is the start's.
                    IReadOnlyList<ItemQuantity> outputs = CityQueries.RecipeOf(content, building.TypeId, building.Level).Outputs;
                    for (int index = 0; index < outputs.Count; index++)
                    {
                        if (outputs[index].Item == content.Currency)
                        {
                            city.AddToPurse(outputs[index].Quantity);
                        }
                        else
                        {
                            building.AddToStore(outputs[index].Item, outputs[index].Quantity);
                        }
                    }

                    report.AddProduced(outputs);
                    building.CompleteCycle();
                    break;
                default:
                    throw new InvalidOperationException(FormattableString.Invariant($"Building {building.Id} is {building.Phase} but has an end."));
            }
        }
    }
}
