#nullable enable

using System;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.PlayMode
{
    /// <summary>
    /// The PlayMode copy of the EditMode <c>CityTestData.OnOpenGrass</c> (the two test assemblies don't reference each other): the Era I
    /// content answer on an open grass map, for flows laid out on the old 8 × 8 grass grid whose subject is not the map (M1-03).
    /// </summary>
    internal static class OpenGrassContent
    {
        /// <summary>
        /// The content answer <paramref name="contentJson"/> on an open grass map (M1-03): every tile grass and in the start area A, but for the
        /// last tile of the last two rows, which keep the areas B and C so the research that opens them stays valid. For tests laid out on the
        /// old 8 × 8 grass grid whose subject is not the map: their walks and tiles mean what they did before the map.
        /// </summary>
        /// <param name="contentJson">A <c>GET /content</c> answer with a map.</param>
        /// <returns>The same answer with the open grass map.</returns>
        public static string OnOpenGrass(string contentJson)
        {
            string text = ReplaceRows(contentJson, "\"tiles\":[", (row, index, count) => new string('.', row.Length));
            return ReplaceRows(text, "\"areas\":[", (row, index, count) =>
            {
                char last = index == count - 2 ? 'B' : index == count - 1 ? 'C' : 'A';
                return new string('A', row.Length - 1) + last;
            });
        }

        /// <summary>Replaces each quoted row of the JSON array that starts with <paramref name="key"/> by what <paramref name="rowFor"/> makes of it.</summary>
        /// <param name="json">The JSON text.</param>
        /// <param name="key">The array's member name with its opening bracket, as written: <c>"tiles":[</c>.</param>
        /// <param name="rowFor">The new row from the old one, its index and the number of rows.</param>
        /// <returns>The text with the new rows.</returns>
        private static string ReplaceRows(string json, string key, Func<string, int, int, string> rowFor)
        {
            int start = json.IndexOf(key, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "the content fixture has no map: " + key);
            start += key.Length;
            int end = json.IndexOf(']', start);
            string[] rows = json.Substring(start, end - start).Split(',');
            for (int index = 0; index < rows.Length; index++)
            {
                string row = rows[index].Trim('"');
                rows[index] = "\"" + rowFor(row, index, rows.Length) + "\"";
            }

            return json.Substring(0, start) + string.Join(",", rows) + json.Substring(end);
        }
    }
}
