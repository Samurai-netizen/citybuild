using System.Runtime.Serialization;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// What a courier is doing, as the snapshot names it (<c>05</c> Snapshot, <c>04</c> section 19; CR-0008). The wire value is the member's
    /// name; a value this list lacks fails the whole answer instead of being guessed.
    /// </summary>
    public enum CourierStateDto
    {
        /// <summary>At its station, ready to leave when its slot has a route.</summary>
        [EnumMember(Value = "AtStation")]
        AtStation,

        /// <summary>On its way to a stop of its route.</summary>
        [EnumMember(Value = "Walking")]
        Walking,

        /// <summary>On its way home to its station.</summary>
        [EnumMember(Value = "Returning")]
        Returning,

        /// <summary>At its station without stamina, until it is re-energized (part H).</summary>
        [EnumMember(Value = "Resting")]
        Resting,
    }
}
