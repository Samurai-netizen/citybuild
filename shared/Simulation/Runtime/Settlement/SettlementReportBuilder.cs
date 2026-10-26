#nullable enable
using System.Collections.Generic;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Settlement
{
    /// <summary>
    /// Collects what happens during one advance and makes the <see cref="SettlementReport"/>. One per advance; not reused, so the report keeps
    /// its totals.
    /// </summary>
    internal sealed class SettlementReportBuilder
    {
        /// <summary>The outputs of every cycle completed during the advance, per item.</summary>
        private readonly ItemTotals _produced = new ItemTotals();
        /// <summary>The inputs of every cycle started during the advance, per item.</summary>
        private readonly ItemTotals _consumed = new ItemTotals();
        /// <summary>The ids of the buildings whose construction or upgrade completed, in time order until <see cref="Build"/> sorts them.</summary>
        private readonly List<int> _completedBuildings = new List<int>();
        /// <summary>The research nodes that completed, in time order until <see cref="Build"/> sorts them.</summary>
        private readonly List<ResearchId> _completedResearch = new List<ResearchId>();

        /// <summary>Counts the outputs of a completed cycle.</summary>
        public void AddProduced(IReadOnlyList<ItemQuantity> outputs) => Add(_produced, outputs);

        /// <summary>Counts the inputs of a started cycle.</summary>
        public void AddConsumed(IReadOnlyList<ItemQuantity> inputs) => Add(_consumed, inputs);

        /// <summary>Records a completed construction or upgrade.</summary>
        public void AddCompletedBuilding(int buildingId) => _completedBuildings.Add(buildingId);

        /// <summary>Records a completed research node.</summary>
        public void AddCompletedResearch(ResearchId researchId) => _completedResearch.Add(researchId);

        /// <summary>
        /// Makes the report. Completions are recorded in time order; the report lists them sorted. A building completes at most one
        /// construction or upgrade per advance, because only a mutation starts one, so each id appears once.
        /// </summary>
        public SettlementReport Build(long elapsedSeconds, long productionSeconds)
        {
            _completedBuildings.Sort();
            _completedResearch.Sort();
            return new SettlementReport(
                elapsedSeconds,
                productionSeconds,
                _produced,
                _consumed,
                _completedBuildings.AsReadOnly(),
                _completedResearch.AsReadOnly(),
                eraChanged: false);
        }

        /// <summary>Adds each amount to its item's total, <c>checked</c> (<c>04</c> section 9): an overflow throws instead of wrapping around.</summary>
        private static void Add(ItemTotals totals, IReadOnlyList<ItemQuantity> amounts)
        {
            for (int index = 0; index < amounts.Count; index++)
            {
                ItemQuantity amount = amounts[index];
                totals.Set(amount.Item, checked(totals.Get(amount.Item) + amount.Quantity));
            }
        }
    }
}
