#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>What the settlement of one request did, from the city's last settled second to now (<c>05</c> Snapshot).</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class SnapshotSettlementDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public SnapshotSettlementDto(long elapsedSeconds, long productionSeconds, Dictionary<string, long> produced, Dictionary<string, long> consumed, List<int> completedBuildings, List<string> completedResearch, bool eraChanged)
        {
            ElapsedSeconds = elapsedSeconds;
            ProductionSeconds = productionSeconds;
            Produced = produced;
            Consumed = consumed;
            CompletedBuildings = completedBuildings;
            CompletedResearch = completedResearch;
            EraChanged = eraChanged;
        }

        /// <summary>Seconds settled by this request.</summary>
        public long ElapsedSeconds { get; }

        /// <summary>Of those, the seconds production counted (the offline cap, <c>04</c> section 6, may count fewer).</summary>
        public long ProductionSeconds { get; }

        /// <summary>Item id to the quantity produced.</summary>
        public Dictionary<string, long> Produced { get; }

        /// <summary>Item id to the quantity consumed.</summary>
        public Dictionary<string, long> Consumed { get; }

        /// <summary>Ids of the buildings that completed.</summary>
        public List<int> CompletedBuildings { get; }

        /// <summary>Ids of the research that completed.</summary>
        public List<string> CompletedResearch { get; }

        /// <summary>Whether the city entered a new era.</summary>
        public bool EraChanged { get; }
    }
}
