namespace CityBuilder.Client.Net
{
    /// <summary>The four commands of M0 a player can give (<c>05</c> Commands); one member for each <c>POST /commands/*</c> endpoint.</summary>
    public enum CommandKind
    {
        /// <summary><c>place-building</c>: build a type on a tile.</summary>
        PlaceBuilding,

        /// <summary><c>upgrade-building</c>: raise a building one level.</summary>
        UpgradeBuilding,

        /// <summary><c>assign-workers</c>: set a building's absolute worker count (the worker stepper).</summary>
        AssignWorkers,

        /// <summary><c>start-research</c>: start a research node.</summary>
        StartResearch,
    }
}
