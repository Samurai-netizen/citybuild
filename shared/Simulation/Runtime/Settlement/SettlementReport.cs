#nullable enable
using System.Collections.Generic;

namespace CityBuilder.Simulation.Settlement
{
    /// <summary>
    /// What happened during one <see cref="Simulator.Advance"/> (<c>core-instructions/04-simulation-model.md</c>, section 11). It comes with
    /// every settled snapshot and is persisted as aggregated ledger entries. Every list is sorted, so two equal settlements give equal reports.
    /// </summary>
    public sealed class SettlementReport
    {
        /// <summary>Creates the report; only <see cref="SettlementReportBuilder.Build"/> does, with sorted lists. Each value is described on its property.</summary>
        internal SettlementReport(
            long elapsedSeconds,
            long productionSeconds,
            IReadOnlyDictionary<ItemId, long> produced,
            IReadOnlyDictionary<ItemId, long> consumed,
            IReadOnlyDictionary<ItemId, long> delivered,
            IReadOnlyDictionary<ItemId, long> moved,
            IReadOnlyDictionary<ItemId, long> refunded,
            IReadOnlyList<int> completedBuildings,
            IReadOnlyList<ResearchId> completedResearch,
            bool eraChanged)
        {
            ElapsedSeconds = elapsedSeconds;
            ProductionSeconds = productionSeconds;
            Produced = produced;
            Consumed = consumed;
            Delivered = delivered;
            Moved = moved;
            Refunded = refunded;
            CompletedBuildings = completedBuildings;
            CompletedResearch = completedResearch;
            EraChanged = eraChanged;
        }

        /// <summary>The seconds the city clock moved: the elapsed time asked for, or 0 when that was below 0.</summary>
        public long ElapsedSeconds { get; }

        /// <summary>
        /// The seconds of the window in which cycles could start: <c>min(ElapsedSeconds, MaxOfflineProductionSeconds)</c>. After them the city
        /// is dormant (<c>04</c> section 2).
        /// </summary>
        public long ProductionSeconds { get; }

        /// <summary>The outputs of the cycles that completed, per item in ordinal order; only items above 0.</summary>
        public IReadOnlyDictionary<ItemId, long> Produced { get; }

        /// <summary>The inputs of the cycles that started, per item in ordinal order; only items above 0.</summary>
        public IReadOnlyDictionary<ItemId, long> Consumed { get; }

        /// <summary>The goods walkers handed to projects, used up there (<c>04</c> section 16), per item in ordinal order; only items above 0.</summary>
        public IReadOnlyDictionary<ItemId, long> Delivered { get; }

        /// <summary>The goods walkers put into stores, per item in ordinal order; only items above 0. A returned load is not counted: it only goes back.</summary>
        public IReadOnlyDictionary<ItemId, long> Moved { get; }

        /// <summary>
        /// The inputs of the cycles that an upgrade ended when its last good arrived during the advance (<c>04</c> section 8), per item in ordinal
        /// order; only items above 0. An upgrade started by a mutation names its refund in the mutation's result instead.
        /// </summary>
        public IReadOnlyDictionary<ItemId, long> Refunded { get; }

        /// <summary>The buildings whose construction or upgrade completed, by ascending id, each once.</summary>
        public IReadOnlyList<int> CompletedBuildings { get; }

        /// <summary>The research nodes that completed, in ordinal id order.</summary>
        public IReadOnlyList<ResearchId> CompletedResearch { get; }

        /// <summary>Whether the city entered a new era. Always false until the era transition exists (M2, <c>25</c>).</summary>
        public bool EraChanged { get; }
    }
}
