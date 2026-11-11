#nullable enable

using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// One line of a courier's delivery queue (<c>05</c> Snapshot, <c>04</c> section 18; CR-0008 amendment 1): "take at the source, put at the
    /// target". A courier runs its lines in id order; its first is the one it runs now.
    /// </summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class SnapshotDeliveryLineDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public SnapshotDeliveryLineDto(int id, int stationId, int slot, int sourceId, int targetId, string itemId, long quantity)
        {
            Id = id;
            StationId = stationId;
            Slot = slot;
            SourceId = sourceId;
            TargetId = targetId;
            ItemId = itemId;
            Quantity = quantity;
        }

        /// <summary>The line's id in this city; <c>cancel-delivery</c> names it.</summary>
        public int Id { get; }

        /// <summary>The courier station of the courier that runs it.</summary>
        public int StationId { get; }

        /// <summary>That courier's slot.</summary>
        public int Slot { get; }

        /// <summary>The building the goods are taken from.</summary>
        public int SourceId { get; }

        /// <summary>The building they are put into.</summary>
        public int TargetId { get; }

        /// <summary>The good's content id.</summary>
        public string ItemId { get; }

        /// <summary>The most the line moves.</summary>
        public long Quantity { get; }
    }
}
