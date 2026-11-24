#nullable enable
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content.Data
{
    /// <summary>The map as read, before any check (see <see cref="MapDefinition"/> for its meaning; <c>29</c> → Map, <c>04</c> section 17).</summary>
    public sealed class MapData
    {
        /// <summary>Required. The map's id, for example <c>crossing</c>.</summary>
        public string? Id { get; set; }

        /// <summary>Required. The letter of the area every city starts with open, for example <c>A</c>.</summary>
        public string? StartArea { get; set; }

        /// <summary>Required. How long a walker takes to cross one forest or rock tile, in milliseconds.</summary>
        public long? RoughTileMillis { get; set; }

        /// <summary>Required. The terrain, one row per y from 0 down, one letter per x: <c>.</c> grass, <c>F</c> forest, <c>R</c> rock, <c>S</c> fertile soil, <c>W</c> water.</summary>
        public List<string?>? Tiles { get; set; }

        /// <summary>Required. The area of each tile, in the shape of <see cref="Tiles"/>: one capital letter per tile.</summary>
        public List<string?>? Areas { get; set; }
    }
}
