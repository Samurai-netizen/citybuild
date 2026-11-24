#nullable enable

using System.Collections.Generic;
using CityBuilder.Client.Core;
using UnityEngine;

namespace CityBuilder.Client.Presentation.Ui
{
    /// <summary>
    /// The placeholders made in code for assets that have not arrived (<c>core-instructions/27-asset-pipeline.md</c> → Placeholders, M1-02):
    /// a tint chosen by the asset's id (the colour of a building's placeholder box and of an icon's tile), the id's initials, the flat icon tile
    /// that shows them, and one log line per missing id. A placeholder is never a file. Display only: it decides nothing.
    /// </summary>
    public static class Placeholders
    {
        /// <summary>The width and height, in pixels, of an icon tile.</summary>
        public const int IconTileSize = 32;

        /// <summary>The start of an icon tile's texture name; the id follows.</summary>
        public const string IconTileNamePrefix = "placeholder icon ";

        /// <summary>The colour the initials are drawn in.</summary>
        public static readonly Color InitialsColor = Color.white;

        /// <summary>The width of a glyph of <see cref="Glyphs"/>, in font pixels.</summary>
        private const int GlyphWidth = 3;

        /// <summary>The height of a glyph of <see cref="Glyphs"/>, in font pixels.</summary>
        private const int GlyphHeight = 5;

        /// <summary>How many texture pixels one font pixel covers.</summary>
        private const int GlyphScale = 4;

        /// <summary>The gap between two initials, in texture pixels.</summary>
        private const int GlyphGap = 4;

        /// <summary>The saturations a tint can have, picked by the id's hash.</summary>
        private static readonly float[] Saturations = { 0.35f, 0.5f, 0.65f };

        /// <summary>The brightnesses a tint can have, picked by the id's hash.</summary>
        private static readonly float[] Values = { 0.6f, 0.72f, 0.84f };

        /// <summary>
        /// A tiny pixel font for the initials: each glyph is 5 rows of 3 pixels, top row first, <c>1</c> for ink. Letters and digits only;
        /// any other character leaves a blank.
        /// </summary>
        private static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
        {
            ['A'] = "010101111101101", ['B'] = "110101110101110", ['C'] = "011100100100011", ['D'] = "110101101101110",
            ['E'] = "111100110100111", ['F'] = "111100110100100", ['G'] = "011100101101011", ['H'] = "101101111101101",
            ['I'] = "111010010010111", ['J'] = "001001001101010", ['K'] = "101101110101101", ['L'] = "100100100100111",
            ['M'] = "101111111101101", ['N'] = "110101101101101", ['O'] = "010101101101010", ['P'] = "110101110100100",
            ['Q'] = "010101101110011", ['R'] = "110101110101101", ['S'] = "011100010001110", ['T'] = "111010010010010",
            ['U'] = "101101101101111", ['V'] = "101101101101010", ['W'] = "101101111111101", ['X'] = "101101010101101",
            ['Y'] = "101101010010010", ['Z'] = "111001010100111", ['0'] = "111101101101111", ['1'] = "010110010010111",
            ['2'] = "110001010100111", ['3'] = "110001010001110", ['4'] = "101101111001001", ['5'] = "111100110001110",
            ['6'] = "011100111101111", ['7'] = "111001010010010", ['8'] = "111101111101111", ['9'] = "111101111001110",
        };

        /// <summary>The ids already logged as missing, with their kind, so each is logged once (<c>27</c>); a log cache, never game state.</summary>
        private static readonly HashSet<string> _reported = new HashSet<string>();

        /// <summary>
        /// The tint of an id: the same for the same id on every device and run (an FNV-1a hash of its characters picks the hue, saturation and
        /// brightness), opaque, and exact in 8 bits per channel so a texture keeps it unchanged.
        /// </summary>
        /// <param name="id">The asset's id, such as a building type id.</param>
        /// <returns>The colour.</returns>
        public static Color TintOf(string id)
        {
            uint hash = 2166136261;
            for (int i = 0; i < id.Length; i++)
            {
                hash = unchecked((hash ^ id[i]) * 16777619);
            }

            float hue = hash % 360 / 360f;
            Color color = Color.HSVToRGB(hue, Saturations[(hash >> 9) % 3], Values[(hash >> 17) % 3]);
            return (Color32)color;
        }

        /// <summary>
        /// The initials of an id, in capitals: the first letters of its first two words (split at <c>_</c>), or the first two letters of a one-word
        /// id (<c>cut_stone</c> → <c>CS</c>, <c>stone</c> → <c>ST</c>).
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>At most two characters; empty for an empty id.</returns>
        public static string InitialsOf(string id)
        {
            string[] words = id.Split(new[] { '_' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return string.Empty;
            }

            string initials = words.Length == 1
                ? words[0].Substring(0, Mathf.Min(2, words[0].Length))
                : words[0].Substring(0, 1) + words[1].Substring(0, 1);
            return initials.ToUpperInvariant();
        }

        /// <summary>
        /// Makes the icon tile of an id: a square of its tint with its initials in <see cref="InitialsColor"/>, named
        /// <see cref="IconTileNamePrefix"/> and the id. The caller keeps it (it is made once per id) and may destroy it.
        /// </summary>
        /// <param name="id">The id the icon stands for (without the icon's prefix).</param>
        /// <returns>A new readable texture.</returns>
        public static Texture2D CreateIconTile(string id)
        {
            var tile = new Texture2D(IconTileSize, IconTileSize, TextureFormat.RGBA32, false)
            {
                name = IconTileNamePrefix + id,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[IconTileSize * IconTileSize];
            Color32 tint = TintOf(id);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = tint;
            }

            string initials = InitialsOf(id);
            int glyphPixels = GlyphWidth * GlyphScale;
            int width = initials.Length * glyphPixels + (initials.Length > 1 ? GlyphGap : 0);
            int left = (IconTileSize - width) / 2;
            int bottom = (IconTileSize - GlyphHeight * GlyphScale) / 2;
            for (int c = 0; c < initials.Length; c++)
            {
                if (Glyphs.TryGetValue(initials[c], out string? glyph))
                {
                    DrawGlyph(pixels, glyph, left + c * (glyphPixels + GlyphGap), bottom);
                }
            }

            tile.SetPixels32(pixels);
            tile.Apply(false, false);
            return tile;
        }

        /// <summary>
        /// Logs that an asset is missing and its placeholder shows, the first time an id of a kind is reported (<c>27</c>: once per id, never an
        /// error for the player).
        /// </summary>
        /// <param name="kind">What is missing: <c>model</c>, <c>icon</c>, <c>carry model</c>.</param>
        /// <param name="id">The asset's id or path.</param>
        /// <returns>True when this call logged it; false when it was logged before.</returns>
        public static bool ReportMissing(string kind, string id)
        {
            if (!_reported.Add(kind + " " + id))
            {
                return false;
            }

            ClientLog.Info("Placeholder: no " + kind + " '" + id + "' yet, drawn in code (27 → Placeholders).");
            return true;
        }

        /// <summary>Draws one glyph into the pixels, scaled, with its bottom-left corner at (<paramref name="x"/>, <paramref name="y"/>).</summary>
        /// <param name="pixels">The tile's pixels, bottom row first.</param>
        /// <param name="glyph">The glyph's 15 pixels, top row first.</param>
        /// <param name="x">The left edge in texture pixels.</param>
        /// <param name="y">The bottom edge in texture pixels.</param>
        private static void DrawGlyph(Color32[] pixels, string glyph, int x, int y)
        {
            Color32 ink = InitialsColor;
            for (int row = 0; row < GlyphHeight; row++)
            {
                for (int column = 0; column < GlyphWidth; column++)
                {
                    if (glyph[row * GlyphWidth + column] != '1')
                    {
                        continue;
                    }

                    int top = y + (GlyphHeight - 1 - row) * GlyphScale;
                    int start = x + column * GlyphScale;
                    for (int dy = 0; dy < GlyphScale; dy++)
                    {
                        for (int dx = 0; dx < GlyphScale; dx++)
                        {
                            pixels[(top + dy) * IconTileSize + start + dx] = ink;
                        }
                    }
                }
            }
        }
    }
}
