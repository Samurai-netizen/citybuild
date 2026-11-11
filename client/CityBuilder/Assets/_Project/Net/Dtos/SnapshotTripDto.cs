#nullable enable

using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// One worker's delivery under way (<c>05</c> Snapshot, <c>04</c> section 18; CR-0008 D21): a worker shuttling goods to another building,
    /// one load at a time, walking its current leg.
    /// </summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class SnapshotTripDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public SnapshotTripDto(int id, int sourceId, int targetId, string itemId, long quantity, long departedAt, long arrivesAt, long remaining, TripLegDto leg)
        {
            Id = id;
            SourceId = sourceId;
            TargetId = targetId;
            ItemId = itemId;
            Quantity = quantity;
            DepartedAt = departedAt;
            ArrivesAt = arrivesAt;
            Remaining = remaining;
            Leg = leg;
        }

        /// <summary>The trip's id in this city, counted from 1.</summary>
        public int Id { get; }

        /// <summary>The building the goods came from; it takes back what doesn't fit.</summary>
        public int SourceId { get; }

        /// <summary>The building the goods go to.</summary>
        public int TargetId { get; }

        /// <summary>The good's content id.</summary>
        public string ItemId { get; }

        /// <summary>The load: above 0 on the way to the target, 0 on the way back.</summary>
        public long Quantity { get; }

        /// <summary>The city-clock second the worker began the current leg.</summary>
        public long DepartedAt { get; }

        /// <summary>The city-clock second the current leg ends: a delivery at the target, or a pickup at the source.</summary>
        public long ArrivesAt { get; }

        /// <summary>How many are still in the source, to be fetched by later loads.</summary>
        public long Remaining { get; }

        /// <summary>Which way the worker walks now.</summary>
        public TripLegDto Leg { get; }
    }
}
