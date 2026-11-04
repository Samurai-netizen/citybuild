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
        public SnapshotSettlementDto(long elapsedSeconds, Dictionary<string, long> produced, Dictionary<string, long> consumed, Dictionary<string, long> delivered, Dictionary<string, long> moved, Dictionary<string, long> refunded, long courierSeconds, List<SnapshotTiredCourierDto> tiredCouriers, List<int> completedBuildings, List<string> completedResearch, bool eraChanged)
        {
            ElapsedSeconds = elapsedSeconds;
            Produced = produced;
            Consumed = consumed;
            Delivered = delivered;
            Moved = moved;
            Refunded = refunded;
            CourierSeconds = courierSeconds;
            TiredCouriers = tiredCouriers;
            CompletedBuildings = completedBuildings;
            CompletedResearch = completedResearch;
            EraChanged = eraChanged;
        }

        /// <summary>Seconds settled by this request.</summary>
        public long ElapsedSeconds { get; }

        /// <summary>Item id to the quantity produced.</summary>
        public Dictionary<string, long> Produced { get; }

        /// <summary>Item id to the quantity consumed.</summary>
        public Dictionary<string, long> Consumed { get; }

        /// <summary>Item id to what walkers handed to projects, used up there (CR-0008).</summary>
        public Dictionary<string, long> Delivered { get; }

        /// <summary>Item id to what walkers put into stores (CR-0008).</summary>
        public Dictionary<string, long> Moved { get; }

        /// <summary>Item id to the inputs given back when an upgrade started at its last delivery (CR-0008).</summary>
        public Dictionary<string, long> Refunded { get; }

        /// <summary>The most stamina one courier spent, in seconds; 0 while the player was present (CR-0008).</summary>
        public long CourierSeconds { get; }

        /// <summary>The couriers whose stamina ran out, by station, then slot (CR-0008).</summary>
        public List<SnapshotTiredCourierDto> TiredCouriers { get; }

        /// <summary>Ids of the buildings that completed.</summary>
        public List<int> CompletedBuildings { get; }

        /// <summary>Ids of the research that completed.</summary>
        public List<string> CompletedResearch { get; }

        /// <summary>Whether the city entered a new era.</summary>
        public bool EraChanged { get; }
    }
}
