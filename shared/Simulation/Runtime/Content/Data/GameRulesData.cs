#nullable enable

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

        /// <summary>Required. The shortest cycle that content may allow, in seconds.</summary>
        public long? MinCycleSeconds { get; set; }

        /// <summary>Required. The largest total labor reduction of one building type, in permille.</summary>
        public int? MaxLaborReductionPermille { get; set; }

        /// <summary>Required. How long a walker takes to cross one tile of open ground, in milliseconds.</summary>
        public long? GroundTileMillis { get; set; }

        /// <summary>Required. How fast a walker crosses another building's tile, in permille of the ground speed.</summary>
        public int? CrossingSpeedPermille { get; set; }

        /// <summary>Required. The shortest walk that content may allow, in seconds.</summary>
        public long? MinWalkSeconds { get; set; }

        /// <summary>Required. The largest total walk bonus of every research together, in permille.</summary>
        public int? MaxWalkBonusPermille { get; set; }

        /// <summary>Required. The most goods one manual trip carries, all goods together (<c>04</c> section 18).</summary>
        public long? TripCarryLimit { get; set; }

        /// <summary>Required. The most stops a courier's route may have, 2 to 16 (<c>04</c> section 19).</summary>
        public int? MaxRouteStops { get; set; }

        /// <summary>Required. The most actions one stop of a route may have, 1 to 8 (<c>04</c> section 19).</summary>
        public int? MaxStopActions { get; set; }
    }
}
