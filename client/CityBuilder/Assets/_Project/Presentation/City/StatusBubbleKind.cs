#nullable enable

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>Which status bubble a building shows (M0-50): the reasons a finished building makes no progress that a player can act on.</summary>
    public enum StatusBubbleKind
    {
        /// <summary>No bubble: the building makes progress, can start its next cycle, or has no recipe (a cottage).</summary>
        None,

        /// <summary>The building has no workers assigned.</summary>
        NoWorkers,

        /// <summary>The building waits for an input item that the city does not have.</summary>
        MissingInput,

        /// <summary>The building waits because the city has no room for an output item.</summary>
        StorageFull,
    }
}
