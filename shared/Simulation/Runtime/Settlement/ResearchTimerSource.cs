#nullable enable
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Settlement
{
    /// <summary>
    /// The research slot's timer (<c>core-instructions/04-simulation-model.md</c>, section 5). On completion the node joins the completed list;
    /// its effects are derived from that list, so they apply from this moment, to cycles that start afterwards.
    /// </summary>
    internal sealed class ResearchTimerSource : IEventSource
    {
        /// <inheritdoc/>
        public long? NextDueAt(CityState city) => city.ActiveResearch?.EndsAt;

        /// <inheritdoc/>
        public void CompleteDue(CityState city, GameContent content, long time, SettlementReportBuilder report, Reservations reservations)
        {
            if (city.ActiveResearch != null && city.ActiveResearch.EndsAt == time)
            {
                report.AddCompletedResearch(city.CompleteResearch());
            }
        }
    }
}
