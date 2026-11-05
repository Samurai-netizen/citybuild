#nullable enable

namespace CityBuilder.Simulation.State
{
    /// <summary>What a courier is doing (<c>core-instructions/04-simulation-model.md</c>, section 19); the API spells each value as named.</summary>
    public enum CourierState
    {
        /// <summary>At its station: ready to leave for its route's first stop, or without a route. No walk, no times.</summary>
        AtStation,

        /// <summary>Walking to a stop of its route (<see cref="Courier.ToStop"/>), from <see cref="Courier.DepartedAt"/> to <see cref="Courier.ArrivesAt"/>.</summary>
        Walking,

        /// <summary>Walking home to its station after its stamina ran out (CR-0008-T1 part H), with its times.</summary>
        Returning,

        /// <summary>At its station without stamina, keeping its load, until re-energized (part H). No walk, no times.</summary>
        Resting,
    }
}
