#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// The map every city stands on (<c>05</c> Content → <c>map</c>, M1-03): one row of terrain letters and one row of area letters per tile
    /// row, from the north edge (y = 0). Terrain: <c>.</c> grass, <c>F</c> forest, <c>R</c> rock, <c>S</c> fertile soil, <c>W</c> water.
    /// </summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class ContentMapDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public ContentMapDto(string id, string startArea, long roughTileMillis, List<string> tiles, List<string> areas)
        {
            Id = id;
            StartArea = startArea;
            RoughTileMillis = roughTileMillis;
            Tiles = tiles;
            Areas = areas;
        }

        /// <summary>The map's id.</summary>
        public string Id { get; }

        /// <summary>The letter of the area that is open from the start.</summary>
        public string StartArea { get; }

        /// <summary>How many milliseconds a walker takes to cross one forest or rock tile.</summary>
        public long RoughTileMillis { get; }

        /// <summary>The terrain letters, one string per row.</summary>
        public List<string> Tiles { get; }

        /// <summary>The area letters, one string per row; research opens an area by its letter.</summary>
        public List<string> Areas { get; }
    }
}
