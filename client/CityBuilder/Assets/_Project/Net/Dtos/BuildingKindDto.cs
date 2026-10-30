using System.Runtime.Serialization;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// The kind of a building type in <c>GET /content</c> (<c>05</c> Content). It is the wire form of the engine's building kind
    /// (the content file writes <c>house</c> and <c>producer</c>; the answer writes <c>House</c> and <c>Producer</c>).
    /// </summary>
    public enum BuildingKindDto
    {
        /// <summary>Provides workers and has no recipe.</summary>
        [EnumMember(Value = "House")]
        House,

        /// <summary>Has a recipe and takes workers.</summary>
        [EnumMember(Value = "Producer")]
        Producer,
    }
}
