#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>The game rules of <c>GET /content</c> (<c>05</c> Content, <c>04</c> section 1).</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class ContentRulesDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public ContentRulesDto(int gridWidth, int gridHeight, int builderSlots, int researchSlots, long maxOfflineProductionSeconds, long minCycleSeconds, int maxLaborReductionPermille, long groundTileMillis, int crossingSpeedPermille, long minWalkSeconds, int maxWalkBonusPermille, long tripCarryLimit, int maxRouteStops, int maxStopActions, int timeScale)
        {
            GridWidth = gridWidth;
            GridHeight = gridHeight;
            BuilderSlots = builderSlots;
            ResearchSlots = researchSlots;
            MaxOfflineProductionSeconds = maxOfflineProductionSeconds;
            MinCycleSeconds = minCycleSeconds;
            MaxLaborReductionPermille = maxLaborReductionPermille;
            GroundTileMillis = groundTileMillis;
            CrossingSpeedPermille = crossingSpeedPermille;
            MinWalkSeconds = minWalkSeconds;
            MaxWalkBonusPermille = maxWalkBonusPermille;
            TripCarryLimit = tripCarryLimit;
            MaxRouteStops = maxRouteStops;
            MaxStopActions = maxStopActions;
            TimeScale = timeScale;
        }

        /// <summary>Tiles per row.</summary>
        public int GridWidth { get; }

        /// <summary>Tiles per column.</summary>
        public int GridHeight { get; }

        /// <summary>Builder slots every city has.</summary>
        public int BuilderSlots { get; }

        /// <summary>Research slots every city has.</summary>
        public int ResearchSlots { get; }

        /// <summary>Most seconds of production one settlement counts.</summary>
        public long MaxOfflineProductionSeconds { get; }

        /// <summary>Shortest production cycle in seconds, after labor reductions.</summary>
        public long MinCycleSeconds { get; }

        /// <summary>Largest labor reduction in permille.</summary>
        public int MaxLaborReductionPermille { get; }

        /// <summary>Milliseconds a walker takes to cross one tile of open ground.</summary>
        public long GroundTileMillis { get; }

        /// <summary>A walker's speed across another building's tile, in permille of the ground speed.</summary>
        public int CrossingSpeedPermille { get; }

        /// <summary>Shortest walk the content allows, in seconds.</summary>
        public long MinWalkSeconds { get; }

        /// <summary>Largest walk bonus of all research together, in permille.</summary>
        public int MaxWalkBonusPermille { get; }

        /// <summary>Most goods one manual trip carries (CR-0008).</summary>
        public long TripCarryLimit { get; }
        /// <summary>Most stops a courier's route has (CR-0008).</summary>
        public int MaxRouteStops { get; }
        /// <summary>Most actions one stop of a route has (CR-0008).</summary>
        public int MaxStopActions { get; }
        /// <summary>Server seconds per real second: 1 outside Development.</summary>
        public int TimeScale { get; }
    }
}
