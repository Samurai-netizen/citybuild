#nullable enable

namespace CityBuilder.Client.Presentation.Roads
{
    /// <summary>Where the player is in road mode (CR-0008).</summary>
    public enum RoadPhase
    {
        /// <summary>Road mode is closed.</summary>
        Idle,

        /// <summary>Road mode is open and waiting for the player to pick or drag tiles on the map.</summary>
        PickingTiles,

        /// <summary>One or more tiles are picked (either for placing level-1 roads or for upgrading/removing existing roads).</summary>
        Confirming,

        /// <summary>A road command (<c>place-roads</c>, <c>upgrade-roads</c> or <c>remove-roads</c>) is in flight.</summary>
        Sending,
    }
}
