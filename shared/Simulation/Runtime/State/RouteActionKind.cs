#nullable enable

namespace CityBuilder.Simulation.State
{
    /// <summary>What a courier does with a good at a stop (<c>core-instructions/04-simulation-model.md</c>, section 19); the API writes <c>take</c> and <c>put</c>.</summary>
    public enum RouteActionKind
    {
        /// <summary>Take the amount of the building's stock of the good, within the courier's free carry room.</summary>
        Take,

        /// <summary>Put the amount of what the courier carries of the good, delivered by <c>04</c> section 16 (a waiting project first).</summary>
        Put,
    }
}
