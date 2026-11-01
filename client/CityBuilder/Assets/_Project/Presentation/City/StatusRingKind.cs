#nullable enable

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>Which job a building's progress ring counts (M0-50): none for a building that is not doing a timed job.</summary>
    public enum StatusRingKind
    {
        /// <summary>No ring: the building is idle.</summary>
        None,

        /// <summary>The building is being built.</summary>
        Constructing,

        /// <summary>The building is being upgraded.</summary>
        Upgrading,

        /// <summary>The building is running a production cycle (it stays where it is while the cycle is frozen).</summary>
        Producing,
    }
}
