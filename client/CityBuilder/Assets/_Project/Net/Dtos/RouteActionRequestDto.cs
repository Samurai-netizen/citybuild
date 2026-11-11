#nullable enable

using System;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>One action <c>{ kind, itemId, amount }</c> of a stop in a <see cref="SetRouteRequestDto"/> (<c>core-instructions/05-api-contract.md</c>, CR-0008).</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class RouteActionRequestDto : IEquatable<RouteActionRequestDto>
    {
        /// <summary>Creates the action.</summary>
        /// <param name="kind">Take or put.</param>
        /// <param name="itemId">The good's content id.</param>
        /// <param name="amount">The amount in the API's form: a preset id, a percentage or an exact number.</param>
        [JsonConstructor]
        public RouteActionRequestDto(RouteActionKindDto kind, string itemId, string amount)
        {
            Kind = kind;
            ItemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
            Amount = amount ?? throw new ArgumentNullException(nameof(amount));
        }

        /// <summary>Take or put.</summary>
        public RouteActionKindDto Kind { get; }

        /// <summary>The good's content id.</summary>
        public string ItemId { get; }

        /// <summary>How much, in the API's form.</summary>
        public string Amount { get; }

        /// <summary>Value equality by kind, item and amount.</summary>
        public bool Equals(RouteActionRequestDto? other) => other != null && Kind == other.Kind && ItemId == other.ItemId && Amount == other.Amount;

        /// <inheritdoc />
        public override bool Equals(object? obj) => Equals(obj as RouteActionRequestDto);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Kind, ItemId, Amount);
    }
}
