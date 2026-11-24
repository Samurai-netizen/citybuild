#nullable enable

using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>The city's builder slots (<c>05</c> Snapshot): one is busy while a building is constructed or upgraded.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class SnapshotBuildersDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public SnapshotBuildersDto(int slots, int busy, int queued = 0, int queueLength = 0)
        {
            Slots = slots;
            Busy = busy;
            Queued = queued;
            QueueLength = queueLength;
        }

        /// <summary>How many buildings may be under construction or upgrade at once.</summary>
        public int Slots { get; }

        /// <summary>How many slots are in use now.</summary>
        public int Busy { get; }

        /// <summary>
        /// How many orders wait in the builder queue (CR-0013); 0 when the answer has none. Written when the city has a queue (the server sends
        /// it then, 0 included), left out otherwise.
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public int Queued { get; }

        /// <summary>How many orders the builder queue holds (CR-0013); 0 means no queue, and when the answer has none; left out when 0.</summary>
        [JsonProperty(Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int QueueLength { get; }

        /// <summary>Newtonsoft writes <see cref="Queued"/> when there is a queue or an order waits in it.</summary>
        /// <returns>Whether to write the member.</returns>
        public bool ShouldSerializeQueued() => QueueLength > 0 || Queued > 0;
    }
}
