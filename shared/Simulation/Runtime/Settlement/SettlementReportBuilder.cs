#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Simulation.Settlement
{
    /// <summary>Collects what happens during one advance and makes the <see cref="SettlementReport"/>. One per advance; not reused.</summary>
    internal sealed class SettlementReportBuilder
    {
        private readonly SortedDictionary<ItemId, long> _produced = new SortedDictionary<ItemId, long>();
        private readonly SortedDictionary<ItemId, long> _consumed = new SortedDictionary<ItemId, long>();
        private readonly List<int> _completedBuildings = new List<int>();
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
                new ReadOnlyDictionary<ItemId, long>(_produced),
                new ReadOnlyDictionary<ItemId, long>(_consumed),
                _completedBuildings.AsReadOnly(),
                _completedResearch.AsReadOnly(),
                eraChanged: false);
        }

        private static void Add(SortedDictionary<ItemId, long> totals, IReadOnlyList<ItemQuantity> amounts)
        {
            for (int index = 0; index < amounts.Count; index++)
            {
                ItemQuantity amount = amounts[index];
                totals.TryGetValue(amount.Item, out long total);
                totals[amount.Item] = checked(total + amount.Quantity);
            }
        }
    }
}
