#nullable enable

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// Which job a building's progress ring counts (M0-50): none for a building that is not doing a timed job; or, instead of the cycle,
    /// how full its store is (CR-0008 D25).
    /// </summary>
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

        /// <summary>
        /// The store gauge (CR-0008 D25): an idle or producing building whose store is more than 75 % full shows how full it is instead of
        /// its cycle, as a segmented ring with the fullest good's icon inside.
        /// </summary>
        Storage,
    }
}
