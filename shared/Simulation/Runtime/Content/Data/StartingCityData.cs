#nullable enable

namespace CityBuilder.Simulation.Content.Data
{
    /// <summary>What a new city starts with, as read, before any check (see <see cref="StartingCityDefinition"/>).</summary>
    public sealed class StartingCityData
    {
        /// <summary>Required. The number of the era the city starts in.</summary>
        public int? Era { get; set; }

        /// <summary>Required. The coins a new city starts with in its purse; a new city has no goods (CR-0008 D2).</summary>
        public long? Purse { get; set; }
    }
}
