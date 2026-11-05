#nullable enable
using System;

namespace CityBuilder.Simulation.State
{
    /// <summary>One action at a stop of a route (<c>core-instructions/04-simulation-model.md</c>, section 19): take or put a good, with an amount. A value.</summary>
    public sealed class RouteAction
    {
        /// <summary>Creates an action.</summary>
        /// <param name="kind">Take or put.</param>
        /// <param name="item">The good (never the currency; <c>SetRoute</c> checks that).</param>
        /// <param name="amount">How much (a preset, a percentage or an exact number; <c>SetRoute</c> checks that its mode is unlocked).</param>
        /// <exception cref="ArgumentNullException"><paramref name="amount"/> is null.</exception>
        public RouteAction(RouteActionKind kind, ItemId item, Amount amount)
        {
            Kind = kind;
            Item = item;
            Amount = amount ?? throw new ArgumentNullException(nameof(amount));
        }

        /// <summary>Take or put.</summary>
        public RouteActionKind Kind { get; }

        /// <summary>The good.</summary>
        public ItemId Item { get; }

        /// <summary>How much, of the building's stock (take) or of what the courier carries (put).</summary>
        public Amount Amount { get; }
    }
}
