#nullable enable

using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>One part of a courier's load (<c>05</c> Snapshot; CR-0008): goods of one kind taken from one building.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class SnapshotCourierLoadDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public SnapshotCourierLoadDto(string itemId, int fromId, long quantity)
        {
            ItemId = itemId;
            FromId = fromId;
            Quantity = quantity;
        }

        /// <summary>The good's content id.</summary>
        public string ItemId { get; }

        /// <summary>The building the goods came from.</summary>
        public int FromId { get; }

        /// <summary>How many, at least 1.</summary>
        public long Quantity { get; }
    }
}
