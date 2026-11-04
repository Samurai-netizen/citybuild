using System.Runtime.Serialization;

namespace CityBuilder.Client.Net
{
    /// <summary>What a courier does at a route stop, as the snapshot and <c>set-route</c> name it (<c>05</c>; CR-0008).</summary>
    public enum RouteActionKindDto
    {
        /// <summary>Takes goods from the building's store onto the courier.</summary>
        [EnumMember(Value = "take")]
        Take,

        /// <summary>Puts goods from the courier into the building.</summary>
        [EnumMember(Value = "put")]
        Put,
    }
}
