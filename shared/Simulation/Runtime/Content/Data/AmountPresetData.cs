#nullable enable

namespace CityBuilder.Simulation.Content.Data
{
    /// <summary>An amount preset as read, before any check (see <see cref="AmountPresetDefinition"/>).</summary>
    public sealed class AmountPresetData
    {
        /// <summary>Required. The preset's id text, for example <c>half</c>; the API's amount string names it.</summary>
        public string? Id { get; set; }

        /// <summary>Required. The display name, for example <c>Half</c>.</summary>
        public string? Name { get; set; }

        /// <summary>Required. The share of the base it gives, 1 to 100 percent (<c>04</c> section 19).</summary>
        public int? Percent { get; set; }
    }
}
