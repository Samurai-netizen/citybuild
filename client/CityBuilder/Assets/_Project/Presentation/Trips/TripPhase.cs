namespace CityBuilder.Client.Presentation.Trips
{
    /// <summary>Where the player is in the manual trip flow (CR-0008).</summary>
    public enum TripPhase
    {
        /// <summary>Nothing is being set up: the trip bar and path are hidden.</summary>
        Idle,

        /// <summary>A source building is picked; waiting for the player to tap a target building on the map.</summary>
        PickingTarget,

        /// <summary>Both source and target are picked; the player can pick item and amount and press Send.</summary>
        Confirming,

        /// <summary><c>start-trip</c> is on its way to the server; buttons are off until it ends.</summary>
        Sending,
    }
}
