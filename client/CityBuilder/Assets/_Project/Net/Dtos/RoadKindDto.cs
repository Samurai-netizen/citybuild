using System.Runtime.Serialization;

namespace CityBuilder.Client.Net
{
    /// <summary>The kind of a road tile in the snapshot (<c>05</c> Snapshot, M1-03): a road on land, or a bridge over water.</summary>
    public enum RoadKindDto
    {
        /// <summary>A road on a land tile.</summary>
        [EnumMember(Value = "road")]
        Road,

        /// <summary>A bridge on a water tile: the only way walkers cross water.</summary>
        [EnumMember(Value = "bridge")]
        Bridge,
    }
}
