#nullable enable
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content.Data
{
    /// <summary>The global rules as read, before any check (see <see cref="GameRules"/> for their meaning).</summary>
    public sealed class GameRulesData
    {
        /// <summary>Required. The grid's width in tiles.</summary>
        public int? GridWidth { get; set; }

        /// <summary>Required. The grid's height in tiles.</summary>
        public int? GridHeight { get; set; }

        /// <summary>Required. How many constructions and upgrades may run at once.</summary>
        public int? BuilderSlots { get; set; }

        /// <summary>Required. How many research nodes may run at once; exactly 1 in M0.</summary>
        public int? ResearchSlots { get; set; }

        /// <summary>Required. The base storage cap of every capped item, keyed by item id.</summary>
        public Dictionary<string, long?>? BaseStorageCaps { get; set; }

        /// <summary>Required. How many seconds of a settlement may start production cycles.</summary>
        public long? MaxOfflineProductionSeconds { get; set; }

        /// <summary>Required. The shortest cycle that content may allow, in seconds.</summary>
        public long? MinCycleSeconds { get; set; }

        /// <summary>Required. The largest total labor reduction of one building type, in permille.</summary>
        public int? MaxLaborReductionPermille { get; set; }
    }
}
