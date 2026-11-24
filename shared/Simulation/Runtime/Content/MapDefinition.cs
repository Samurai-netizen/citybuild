#nullable enable
using System;
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// The one hand-made map every city of the content stands on (<c>core-instructions/29-era1-chronicle.md</c> → Map, <c>04</c> section 17): the
    /// terrain of each tile and the area it belongs to. A city starts with the start area open; <see cref="OpenLandEffect"/> research opens
    /// the others. Its size is the grid's (<see cref="GameRules.GridWidth"/> × <see cref="GameRules.GridHeight"/>).
    /// </summary>
    public sealed class MapDefinition
    {
        /// <summary>The terrain of each tile, row by row (index y × width + x).</summary>
        private readonly Terrain[] _terrain;

        /// <summary>The area letter of each tile, in the order of <see cref="_terrain"/>.</summary>
        private readonly char[] _areas;

        /// <summary>Creates the map from rows the content rules have checked: the same size, known letters.</summary>
        internal MapDefinition(string id, char startArea, long roughTileMillis, int width, int height, Terrain[] terrain, char[] areas)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            StartArea = startArea;
            RoughTileMillis = roughTileMillis;
            Width = width;
            Height = height;
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            _areas = areas ?? throw new ArgumentNullException(nameof(areas));
            var letters = new SortedSet<char>(areas);
            AreaLetters = ReadOnlyLists.From(new List<char>(letters));
        }

        /// <summary>The map's id, for example <c>crossing</c>.</summary>
        public string Id { get; }

        /// <summary>The area every city starts with open.</summary>
        public char StartArea { get; }

        /// <summary>How long a walker takes to cross one forest or rock tile, in milliseconds.</summary>
        public long RoughTileMillis { get; }

        /// <summary>The number of columns.</summary>
        public int Width { get; }

        /// <summary>The number of rows.</summary>
        public int Height { get; }

        /// <summary>Every area letter of the map, in order.</summary>
        public IReadOnlyList<char> AreaLetters { get; }

        /// <summary>The terrain of the tile (<paramref name="x"/>, <paramref name="y"/>), which must be on the map.</summary>
        public Terrain TerrainAt(int x, int y) => _terrain[IndexOf(x, y)];

        /// <summary>The area letter of the tile (<paramref name="x"/>, <paramref name="y"/>), which must be on the map.</summary>
        public char AreaAt(int x, int y) => _areas[IndexOf(x, y)];

        /// <summary>The index of a tile in the arrays; throws for a tile off the map, which is a caller's bug.</summary>
        private int IndexOf(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                throw new ArgumentOutOfRangeException(nameof(x), FormattableString.Invariant($"The tile ({x}, {y}) is off the {Width} × {Height} map."));
            }

            return (y * Width) + x;
        }
    }
}
