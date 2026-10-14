#nullable enable

namespace CityBuilder.Simulation.Content.Data
{
    /// <summary>An item as read, before any check (see <see cref="ItemDefinition"/>).</summary>
    public sealed class ItemData
    {
        /// <summary>Required. The item's id text, for example <c>logs</c>.</summary>
        public string? Id { get; set; }

        /// <summary>Required. The display name.</summary>
        public string? Name { get; set; }

        /// <summary>Required. The number of the era the item belongs to.</summary>
        public int? Era { get; set; }

        /// <summary>Required. Whether production is limited by a storage cap.</summary>
        public bool? Capped { get; set; }

        /// <summary>Required. Whether the item may be traded on the player market (from Era II).</summary>
        public bool? Tradable { get; set; }
    }
}
