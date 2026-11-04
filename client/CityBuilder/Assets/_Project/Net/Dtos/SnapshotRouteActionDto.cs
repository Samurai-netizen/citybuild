#nullable enable

using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>One action at a route stop (<c>05</c> Snapshot; CR-0008), as <c>set-route</c> sends it.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class SnapshotRouteActionDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public SnapshotRouteActionDto(RouteActionKindDto kind, string itemId, string amount)
        {
            Kind = kind;
            ItemId = itemId;
            Amount = amount;
        }

        /// <summary>Take or put.</summary>
        public RouteActionKindDto Kind { get; }

        /// <summary>The good's content id.</summary>
        public string ItemId { get; }

        /// <summary>How much, in the API's form: a preset id, a percentage or a number.</summary>
        public string Amount { get; }
    }
}
