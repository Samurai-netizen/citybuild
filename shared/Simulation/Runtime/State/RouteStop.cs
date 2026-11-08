#nullable enable
using System;
using System.Collections.Generic;

namespace CityBuilder.Simulation.State
{
    /// <summary>One stop of a route (<c>core-instructions/04-simulation-model.md</c>, section 19): a building and the actions done there, in order. A value.</summary>
    public sealed class RouteStop
    {
        /// <summary>Creates a stop.</summary>
        /// <param name="buildingId">The building, at least 1.</param>
        /// <param name="actions">Its actions in order, at least one; the content's most per stop is <c>SetRoute</c>'s check.</param>
        /// <exception cref="ArgumentNullException">The list or an action is null.</exception>
        /// <exception cref="ArgumentException">The building id is below 1, or there is no action.</exception>
        public RouteStop(int buildingId, IEnumerable<RouteAction> actions)
        {
            if (actions is null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            var list = new List<RouteAction>();
            foreach (RouteAction action in actions)
            {
                list.Add(action ?? throw new ArgumentNullException(nameof(actions), "An action is null."));
            }

            if (buildingId < 1 || list.Count == 0)
            {
                throw new ArgumentException(FormattableString.Invariant($"A stop names a building of at least 1 ({buildingId}) and has at least one action ({list.Count})."));
            }

            BuildingId = buildingId;
            Actions = list.AsReadOnly();
        }

        /// <summary>The building the courier walks to.</summary>
        public int BuildingId { get; }

        /// <summary>What it does there, in order.</summary>
        public IReadOnlyList<RouteAction> Actions { get; }
    }
}
