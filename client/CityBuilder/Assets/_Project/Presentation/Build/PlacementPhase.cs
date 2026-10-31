namespace CityBuilder.Client.Presentation.Build
{
    /// <summary>Where the player is in placing a building (<see cref="PlacementFlowModel"/>).</summary>
    public enum PlacementPhase
    {
        /// <summary>No card is picked: nothing is highlighted and the bar is hidden.</summary>
        Idle,

        /// <summary>A card is picked and no tile: the ground shows which tiles are free and which are taken.</summary>
        PickingTile,

        /// <summary>A card and a tile are picked: the bar asks the player to confirm.</summary>
        Confirming,

        /// <summary>The player confirmed and the command is on its way; nothing can be changed until it ends.</summary>
        Sending,
    }
}
