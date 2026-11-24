using System.Runtime.Serialization;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// The terrain a raw producer needs in <c>GET /content</c> (<c>05</c> Content, M1-03): the wire form of the engine's building site. A type
    /// that may stand on any open land has no site (<c>null</c>).
    /// </summary>
    public enum BuildingSiteDto
    {
        /// <summary>A forest tile on one of the four sides (the woodcutter).</summary>
        [EnumMember(Value = "nearForest")]
        NearForest,

        /// <summary>A rock tile on one of the four sides (the quarry).</summary>
        [EnumMember(Value = "nearRock")]
        NearRock,

        /// <summary>The tile itself is fertile soil (the farm).</summary>
        [EnumMember(Value = "fertileSoil")]
        FertileSoil,
    }
}
