#nullable enable

namespace CityBuilder.Simulation.Content
{
    /// <summary>The global rules of the game content (<c>core-instructions/23-mvp-content.md</c>, global rules).</summary>
    public sealed class GameRules
    {
        /// <summary>Creates the rules from values the shape phase has read (<see cref="ContentReader"/>); each is described on its property.</summary>
        internal GameRules(
            int gridWidth,
            int gridHeight,
            int builderSlots,
            int researchSlots,
            long minCycleSeconds,
            int maxLaborReductionPermille,
            long groundTileMillis,
            int crossingSpeedPermille,
            long minWalkSeconds,
            int maxWalkBonusPermille,
            long tripCarryLimit,
            int maxRouteStops,
            int maxStopActions)
        {
            GridWidth = gridWidth;
            GridHeight = gridHeight;
            BuilderSlots = builderSlots;
            ResearchSlots = researchSlots;
            MinCycleSeconds = minCycleSeconds;
            MaxLaborReductionPermille = maxLaborReductionPermille;
            GroundTileMillis = groundTileMillis;
            CrossingSpeedPermille = crossingSpeedPermille;
            MinWalkSeconds = minWalkSeconds;
            MaxWalkBonusPermille = maxWalkBonusPermille;
            TripCarryLimit = tripCarryLimit;
            MaxRouteStops = maxRouteStops;
            MaxStopActions = maxStopActions;
        }

        /// <summary>The grid's width in tiles: x runs from 0 to <c>GridWidth − 1</c>.</summary>
        public int GridWidth { get; }

        /// <summary>The grid's height in tiles: y runs from 0 to <c>GridHeight − 1</c>.</summary>
        public int GridHeight { get; }

        /// <summary>How many constructions and upgrades may run at once.</summary>
        public int BuilderSlots { get; }

        /// <summary>How many research nodes may run at once: exactly 1 in M0.</summary>
        public int ResearchSlots { get; }

        /// <summary>The shortest cycle content may allow, in seconds: with every labor reduction and all max workers, no cycle is shorter.</summary>
        public long MinCycleSeconds { get; }

        /// <summary>The largest total labor reduction of one building type, in permille (500 = half the labor).</summary>
        public int MaxLaborReductionPermille { get; }

        /// <summary>
        /// How long a walker takes to cross one tile of open ground, in milliseconds (5000 in M0). A walk's own start and end buildings count
        /// as ground too (<c>core-instructions/04-simulation-model.md</c>, section 17).
        /// </summary>
        public long GroundTileMillis { get; }

        /// <summary>
        /// How fast a walker crosses another building's tile, in permille of the ground speed (200 = a fifth: 25 s instead of 5 s). A walk
        /// crosses a building only when every way does (the path rule, <c>04</c> section 17).
        /// </summary>
        public int CrossingSpeedPermille { get; }

        /// <summary>The shortest walk content may allow, in seconds: one step between two adjacent buildings with every walk bonus applied.</summary>
        public long MinWalkSeconds { get; }

        /// <summary>The largest total walk bonus of all research together, in permille (500 = walks take half the time).</summary>
        public int MaxWalkBonusPermille { get; }

        /// <summary>The most goods one manual trip carries, all goods together, at least 1 (<c>04</c> section 18).</summary>
        public long TripCarryLimit { get; }

        /// <summary>The most stops a courier's route may have, 2 to 16 (<c>04</c> section 19).</summary>
        public int MaxRouteStops { get; }

        /// <summary>The most actions one stop of a route may have, 1 to 8 (<c>04</c> section 19).</summary>
        public int MaxStopActions { get; }
    }
}
