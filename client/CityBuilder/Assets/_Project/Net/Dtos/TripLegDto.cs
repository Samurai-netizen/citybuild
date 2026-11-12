#nullable enable

using System.Runtime.Serialization;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// Which way a worker's delivery walks, as the snapshot names it (<c>05</c> Snapshot, <c>04</c> section 18; CR-0008 D21). The wire value
    /// is the member's name; a value this list lacks fails the whole answer instead of being guessed.
    /// </summary>
    public enum TripLegDto
    {
        /// <summary>From the source to the target, carrying a load.</summary>
        [EnumMember(Value = "ToTarget")]
        ToTarget,

        /// <summary>Back to the source for the next load, carrying nothing.</summary>
        [EnumMember(Value = "ToSource")]
        ToSource,
    }
}
