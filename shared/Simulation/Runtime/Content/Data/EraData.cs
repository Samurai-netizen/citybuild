#nullable enable

namespace CityBuilder.Simulation.Content.Data
{
    /// <summary>An era as read, before any check (see <see cref="EraDefinition"/>).</summary>
    public sealed class EraData
    {
        /// <summary>Required. The era's number: 1 for the first era.</summary>
        public int? Number { get; set; }

        /// <summary>Required. The era's id text, for example <c>early_medieval</c>.</summary>
        public string? Id { get; set; }

        /// <summary>Required. The display name, for example <c>Early Middle Ages</c>.</summary>
        public string? Name { get; set; }
    }
}
