#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>One stop <c>{ buildingId, actions }</c> of a <see cref="SetRouteRequestDto"/> (<c>core-instructions/05-api-contract.md</c>, CR-0008).</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class RouteStopRequestDto : IEquatable<RouteStopRequestDto>
    {
        /// <summary>Creates the stop.</summary>
        /// <param name="buildingId">The building the courier walks to.</param>
        /// <param name="actions">What the courier does there, in order (1 to 8).</param>
        [JsonConstructor]
        public RouteStopRequestDto(int buildingId, IReadOnlyList<RouteActionRequestDto> actions)
        {
            BuildingId = buildingId;
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        /// <summary>The building.</summary>
        public int BuildingId { get; }

        /// <summary>The actions in order.</summary>
        public IReadOnlyList<RouteActionRequestDto> Actions { get; }

        /// <summary>Value equality by building and actions in order.</summary>
        public bool Equals(RouteStopRequestDto? other)
        {
            if (other == null || BuildingId != other.BuildingId || Actions.Count != other.Actions.Count)
            {
                return false;
            }

            for (int i = 0; i < Actions.Count; i++)
            {
                if (!Actions[i].Equals(other.Actions[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => Equals(obj as RouteStopRequestDto);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hash = BuildingId;
            for (int i = 0; i < Actions.Count; i++)
            {
                hash = HashCode.Combine(hash, Actions[i]);
            }

            return hash;
        }
    }
}
