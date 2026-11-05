using System.Runtime.Serialization;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// The kind of a building type in <c>GET /content</c> (<c>05</c> Content). It is the wire form of the engine's building kind
    /// (the content file writes camel case, <c>courierStation</c>; the answer Pascal case, <c>CourierStation</c>).
    /// </summary>
    public enum BuildingKindDto
    {
        /// <summary>Provides workers and has no recipe.</summary>
        [EnumMember(Value = "House")]
        House,

        /// <summary>Has a recipe and takes workers.</summary>
        [EnumMember(Value = "Producer")]
        Producer,

        /// <summary>A store only, holding goods for walkers (CR-0008).</summary>
        [EnumMember(Value = "Warehouse")]
        Warehouse,

        /// <summary>Its workers are couriers walking routes (CR-0008).</summary>
        [EnumMember(Value = "CourierStation")]
        CourierStation,

        /// <summary>The science building, one per city, where research happens (CR-0008).</summary>
        [EnumMember(Value = "Science")]
        Science,
    }
}
