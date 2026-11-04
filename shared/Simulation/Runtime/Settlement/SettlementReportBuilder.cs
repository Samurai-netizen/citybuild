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
        /// <summary>The goods walkers handed to projects (used up there), per item.</summary>
        private readonly ItemTotals _delivered = new ItemTotals();
        /// <summary>The goods walkers put into stores, per item.</summary>
        private readonly ItemTotals _moved = new ItemTotals();
        /// <summary>The inputs of the cycles an upgrade ended when its last good arrived during the advance, per item.</summary>
        private readonly ItemTotals _refunded = new ItemTotals();
        /// <summary>The ids of the buildings whose construction or upgrade completed, in time order until <see cref="Build"/> sorts them.</summary>
        private readonly List<int> _completedBuildings = new List<int>();
        /// <summary>The research nodes that completed, in time order until <see cref="Build"/> sorts them.</summary>
        private readonly List<ResearchId> _completedResearch = new List<ResearchId>();
        /// <summary>The couriers whose stamina ran out, in time order until <see cref="Build"/> sorts them by (station, slot).</summary>
        private readonly List<TiredCourier> _tiredCouriers = new List<TiredCourier>();
        /// <summary>The most stamina, in seconds, one courier has spent so far in the advance.</summary>
        private long _courierSeconds;

        /// <summary>Counts the outputs of a completed cycle.</summary>
        public void AddProduced(IReadOnlyList<ItemQuantity> outputs) => Add(_produced, outputs);

        /// <summary>Counts the inputs of a started cycle.</summary>
        public void AddConsumed(IReadOnlyList<ItemQuantity> inputs) => Add(_consumed, inputs);

        /// <summary>Counts goods a walker handed to a project.</summary>
        public void AddDelivered(ItemId item, long quantity) => _delivered.Set(item, checked(_delivered.Get(item) + quantity));

        /// <summary>Counts goods a walker put into a store.</summary>
        public void AddMoved(ItemId item, long quantity) => _moved.Set(item, checked(_moved.Get(item) + quantity));

        /// <summary>Counts the inputs of a cycle that an upgrade started by a delivery ended.</summary>
        public void AddRefunded(IReadOnlyList<ItemQuantity> inputs) => Add(_refunded, inputs);

        /// <summary>Records a completed construction or upgrade.</summary>
        public void AddCompletedBuilding(int buildingId) => _completedBuildings.Add(buildingId);

        /// <summary>Records a completed research node.</summary>
        public void AddCompletedResearch(ResearchId researchId) => _completedResearch.Add(researchId);

        /// <summary>Notes what one courier has spent in the advance so far, in seconds: the report keeps the most (<c>04</c> section 11).</summary>
        public void RecordCourierSeconds(long spentSoFar)
        {
            if (spentSoFar > _courierSeconds)
            {
                _courierSeconds = spentSoFar;
            }
        }

        /// <summary>Notes a courier whose stamina reached 0 at <paramref name="at"/>; a courier tires at most once per advance (only a mutation re-energizes).</summary>
        public void AddTiredCourier(int stationId, int slot, long at) => _tiredCouriers.Add(new TiredCourier(stationId, slot, at));

        /// <summary>
        /// Makes the report. Completions are recorded in time order; the report lists them sorted. A building completes at most one
        /// construction or upgrade per advance, because only a mutation orders one (a delivery only starts what was ordered), so each id appears once.
        /// </summary>
        public SettlementReport Build(long elapsedSeconds, long productionSeconds)
        {
            _completedBuildings.Sort();
            _completedResearch.Sort();
            _tiredCouriers.Sort((left, right) => left.StationId != right.StationId ? left.StationId.CompareTo(right.StationId) : left.Slot.CompareTo(right.Slot));
            return new SettlementReport(
                elapsedSeconds,
                productionSeconds,
                _produced,
                _consumed,
                _delivered,
                _moved,
                _refunded,
                _completedBuildings.AsReadOnly(),
                _completedResearch.AsReadOnly(),
                eraChanged: false,
                _courierSeconds,
                _tiredCouriers.AsReadOnly());
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
