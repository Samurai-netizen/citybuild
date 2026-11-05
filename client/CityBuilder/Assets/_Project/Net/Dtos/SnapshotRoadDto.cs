#nullable enable

using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>One road tile of the snapshot's <c>roads</c> list (<c>05</c> Snapshot, <c>04</c> section 17; CR-0008).</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class SnapshotRoadDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public SnapshotRoadDto(int x, int y, int level)
        {
            X = x;
            Y = y;
            Level = level;
        }

        /// <summary>The tile column.</summary>
        public int X { get; }

        /// <summary>The tile row.</summary>
        public int Y { get; }

        /// <summary>The road level: 1 when placed, up to the content's last road level.</summary>
        public int Level { get; }
    }
}
