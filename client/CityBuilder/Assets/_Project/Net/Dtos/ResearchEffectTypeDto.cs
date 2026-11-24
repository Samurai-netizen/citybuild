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

        /// <summary>The limit of a good in every store that holds it rises by an amount (<c>item</c> and <c>amount</c> are set).</summary>
        [EnumMember(Value = "storageCapBonus")]
        StorageCapBonus,

        /// <summary>An amount mode becomes usable (<c>mode</c> is set; CR-0008).</summary>
        [EnumMember(Value = "unlockAmountMode")]
        UnlockAmountMode,

        /// <summary>Every walk gets shorter by a share in permille (<c>permille</c> is set; CR-0008).</summary>
        [EnumMember(Value = "walkSpeedBonus")]
        WalkSpeedBonus,

        /// <summary>The highest level of a building type rises by an amount (<c>building</c> and <c>amount</c> are set; M1-02).</summary>
        [EnumMember(Value = "maxLevelBonus")]
        MaxLevelBonus,

        /// <summary>The city gets more builder slots (<c>amount</c> is set; M1-02).</summary>
        [EnumMember(Value = "builderSlotBonus")]
        BuilderSlotBonus,

        /// <summary>The city is chartered: Era I is complete (no member is set; M1-02).</summary>
        [EnumMember(Value = "grantCharter")]
        GrantCharter,
    }
}
