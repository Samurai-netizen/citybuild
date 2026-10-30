using System.Runtime.Serialization;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// What a building is doing, as the snapshot names it (<c>05</c> Snapshot, <c>04</c> section 4). The wire value is the member's
    /// name; a value this list lacks fails the whole answer instead of being guessed (the client and the server ship together in M0).
    /// </summary>
    public enum BuildingPhaseDto
    {
        /// <summary>The building is being built; it does nothing else until the phase ends.</summary>
        [EnumMember(Value = "Constructing")]
        Constructing,

        /// <summary>The building is being upgraded; it keeps its level and workers and produces nothing until the phase ends.</summary>
        [EnumMember(Value = "Upgrading")]
        Upgrading,

        /// <summary>The building is built and not producing (a house, or a producer that cannot start a cycle).</summary>
        [EnumMember(Value = "Idle")]
        Idle,

        /// <summary>The building has a production cycle running.</summary>
        [EnumMember(Value = "Producing")]
        Producing,
    }
}
