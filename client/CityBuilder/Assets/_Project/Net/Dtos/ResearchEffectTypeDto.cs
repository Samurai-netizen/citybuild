using System.Runtime.Serialization;

namespace CityBuilder.Client.Net
{
    /// <summary>The kinds of research effect <c>GET /content</c> names (<c>05</c> Content, <c>25</c>); the wire values are camelCase.</summary>
    public enum ResearchEffectTypeDto
    {
        /// <summary>A building type becomes placeable (<c>building</c> is set).</summary>
        [EnumMember(Value = "unlockBuilding")]
        UnlockBuilding,

        /// <summary>The cycles of a building type get shorter by a share in permille (<c>building</c> and <c>permille</c> are set).</summary>
        [EnumMember(Value = "laborReduction")]
        LaborReduction,

        /// <summary>The storage cap of an item rises by an amount (<c>item</c> and <c>amount</c> are set).</summary>
        [EnumMember(Value = "storageCapBonus")]
        StorageCapBonus,
    }
}
