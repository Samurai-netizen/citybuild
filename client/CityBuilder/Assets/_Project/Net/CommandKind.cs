namespace CityBuilder.Client.Net
{
    /// <summary>The commands of M0 a player can give (<c>05</c> Commands); one member for each <c>POST /commands/*</c> endpoint.</summary>
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

        /// <summary><c>start-trip</c>: send a free worker from a source store to a target building or project (CR-0008).</summary>
        StartTrip,

        /// <summary><c>place-roads</c>: place level-1 roads on one or more tiles (CR-0008).</summary>
        PlaceRoads,

        /// <summary><c>upgrade-roads</c>: raise one or more road tiles one level (CR-0008).</summary>
        UpgradeRoads,

        /// <summary><c>remove-roads</c>: remove one or more road tiles (CR-0008).</summary>
        RemoveRoads,

        /// <summary><c>set-route</c>: set or clear a courier slot's route (CR-0008).</summary>
        SetRoute,

        /// <summary><c>reenergize-couriers</c>: give every courier its full stamina again (CR-0008).</summary>
        ReenergizeCouriers,

        /// <summary><c>cancel-delivery</c>: cancel one delivery line of a courier, or all of its lines and its route (CR-0008 amendment 1).</summary>
        CancelDelivery,
    }
}
