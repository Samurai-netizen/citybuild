#nullable enable
using System.Collections.Generic;
using CityBuilder.Simulation.Content.Data;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// The shape phase of the map, the bridge and a building's site (M1-03; <see cref="ContentReader"/> does the rest): reads the values,
    /// reports missing ones and unknown letters, and checks that the rows match the grid. The references between them and the research
    /// (an area an effect opens, the bridge's unlock) are <see cref="MapRules"/>'.
    /// </summary>
    internal sealed class MapReader
    {
        /// <summary>The JSON value of a <c>site</c> that needs forest beside the building.</summary>
        private const string _siteNearForest = "nearForest";

        /// <summary>The JSON value of a <c>site</c> that needs rock beside the building.</summary>
        private const string _siteNearRock = "nearRock";

        /// <summary>The JSON value of a <c>site</c> on fertile soil.</summary>
        private const string _siteFertileSoil = "fertileSoil";

        /// <summary>Where every error goes.</summary>
        private readonly ContentErrorList _errors;

        /// <summary>The shared reader of required values.</summary>
        private readonly FieldReader _fields;

        /// <summary>Creates a reader that reports into <paramref name="errors"/> through <paramref name="fields"/>.</summary>
        public MapReader(ContentErrorList errors, FieldReader fields)
        {
            _errors = errors;
            _fields = fields;
        }

        /// <summary>
        /// Reads the map, or null without one or after reporting why it can't be made. Rows are checked against the grid's size when the rules
        /// were read (<paramref name="rules"/>); a list reports its first row of another length only, so a wrong grid size is one error.
        /// </summary>
        public MapDefinition? ReadMap(MapData? data, GameRules? rules)
        {
            if (data is null)
            {
                return null;
            }

            const string path = "map";
            string id = _fields.RequireText(data.Id, path + ".id");
            long roughTileMillis = _fields.RequireLong(data.RoughTileMillis, path + ".roughTileMillis");
            char? startArea = ReadLetter(data.StartArea, path + ".startArea");
            int errorsBefore = _errors.Count;
            Terrain[]? terrain = ReadRows<Terrain>(data.Tiles, path + ".tiles", rules, ReadTerrain, "a terrain letter: '.', 'F', 'R', 'S' or 'W'");
            char[]? areas = ReadRows<char>(data.Areas, path + ".areas", rules, ReadArea, "a capital letter, the tile's area");
            if (terrain is null || areas is null || !startArea.HasValue || rules is null || _errors.Count > errorsBefore)
            {
                return null;
            }

            return new MapDefinition(id, startArea.Value, roughTileMillis, rules.GridWidth, rules.GridHeight, terrain, areas);
        }

        /// <summary>Reads the bridge, or null without one.</summary>
        public BridgeDefinition? ReadBridge(BridgeData? data)
        {
            if (data is null)
            {
                return null;
            }

            const string path = "bridge";
            return new BridgeDefinition(
                _fields.RequireText(data.Name, path + ".name"),
                _fields.RequireLong(data.TileMillis, path + ".tileMillis"),
                _fields.ReadItemAmounts(data.Cost, path + ".cost", true));
        }

        /// <summary>Reads a building type's site: <see cref="BuildingSite.Any"/> without one, after reporting an unknown one.</summary>
        public BuildingSite ReadSite(string? text, string path)
        {
            switch (text)
            {
                case null:
                    return BuildingSite.Any;
                case _siteNearForest:
                    return BuildingSite.NearForest;
                case _siteNearRock:
                    return BuildingSite.NearRock;
                case _siteFertileSoil:
                    return BuildingSite.FertileSoil;
                default:
                    _fields.InvalidValue(path, $"'{text}' is not a site: use '{_siteNearForest}', '{_siteNearRock}' or '{_siteFertileSoil}'.");
                    return BuildingSite.Any;
            }
        }

        /// <summary>Reads an area letter (one capital letter), or null after reporting a missing or malformed one.</summary>
        public char? ReadLetter(string? text, string path)
        {
            if (text is null)
            {
                _fields.Missing(path);
                return null;
            }

            if (text.Length != 1 || text[0] < 'A' || text[0] > 'Z')
            {
                _fields.InvalidValue(path, $"'{text}' is not an area: an area is one capital letter.");
                return null;
            }

            return text[0];
        }

        /// <summary>
        /// Reads a list of rows into one array of cells (index y × width + x): the number of rows must be the grid's height and each row as
        /// long as its width. Null after reporting a missing list, a wrong shape or an unknown letter.
        /// </summary>
        private T[]? ReadRows<T>(List<string?>? rows, string path, GameRules? rules, ReadCell<T> read, string expected)
        {
            if (rows is null)
            {
                _fields.Missing(path);
                return null;
            }

            if (rules is null)
            {
                return null;
            }

            if (rows.Count != rules.GridHeight)
            {
                _errors.Add(ContentErrorCode.MapMismatch, path, $"The map has {rows.Count} rows, but the grid is {rules.GridHeight} high.");
                return null;
            }

            var cells = new T[rules.GridWidth * rules.GridHeight];
            for (int y = 0; y < rows.Count; y++)
            {
                string rowPath = path + "[#" + y.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
                string? row = rows[y];
                if (row is null || row.Length != rules.GridWidth)
                {
                    _errors.Add(ContentErrorCode.MapMismatch, rowPath, $"Each row has one letter per column of the {rules.GridWidth} wide grid.");
                    return null;
                }

                for (int x = 0; x < row.Length; x++)
                {
                    if (!read(row[x], out T cell))
                    {
                        _fields.InvalidValue(rowPath, $"'{row[x]}' at x = {x} is not {expected}.");
                        return null;
                    }

                    cells[(y * rules.GridWidth) + x] = cell;
                }
            }

            return cells;
        }

        /// <summary>Reads one cell of a row; false for a letter the list doesn't allow.</summary>
        private delegate bool ReadCell<T>(char letter, out T cell);

        /// <summary>The terrain of a letter of <c>map.tiles</c>.</summary>
        private static bool ReadTerrain(char letter, out Terrain terrain)
        {
            switch (letter)
            {
                case '.': terrain = Terrain.Grass; return true;
                case 'F': terrain = Terrain.Forest; return true;
                case 'R': terrain = Terrain.Rock; return true;
                case 'S': terrain = Terrain.Soil; return true;
                case 'W': terrain = Terrain.Water; return true;
                default: terrain = Terrain.Grass; return false;
            }
        }

        /// <summary>An area letter of <c>map.areas</c>: a capital letter.</summary>
        private static bool ReadArea(char letter, out char area)
        {
            area = letter;
            return letter >= 'A' && letter <= 'Z';
        }
    }
}
