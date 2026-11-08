#nullable enable

using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>One manual trip under way (<c>05</c> Snapshot, <c>04</c> section 18; CR-0008): a worker carrying goods to another building.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class SnapshotTripDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public SnapshotTripDto(int id, int sourceId, int targetId, string itemId, long quantity, long departedAt, long arrivesAt)
        {
            Id = id;
            SourceId = sourceId;
            TargetId = targetId;
            ItemId = itemId;
            Quantity = quantity;
            DepartedAt = departedAt;
            ArrivesAt = arrivesAt;
        }

        /// <summary>The trip's id in this city, counted from 1.</summary>
        public int Id { get; }

        /// <summary>The building the goods came from; it takes back what doesn't fit.</summary>
        public int SourceId { get; }

        /// <summary>The building the goods go to.</summary>
        public int TargetId { get; }

        /// <summary>The good's content id.</summary>
        public string ItemId { get; }

        /// <summary>The load.</summary>
        public long Quantity { get; }

        /// <summary>The city-clock second the worker left.</summary>
        public long DepartedAt { get; }

        /// <summary>The city-clock second the worker arrives and delivers.</summary>
        public long ArrivesAt { get; }
    }
}
