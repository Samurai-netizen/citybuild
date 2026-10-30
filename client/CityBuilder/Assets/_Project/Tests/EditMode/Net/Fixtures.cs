using System.IO;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>
    /// Reads the files of <c>Tests/Fixtures</c>: answers copied byte for byte from a real development server (M0-37, see the folder's
    /// <c>README.md</c>). A DTO test that passes on them proves the DTOs read what the server really writes, not what a test author imagined.
    /// </summary>
    public static class Fixtures
    {
        /// <summary>The fixture folder, under the project's <c>Assets</c> folder.</summary>
        private const string Folder = "_Project/Tests/Fixtures";

        /// <summary>The text of the fixture file <paramref name="name"/>, for example <c>content.json</c>.</summary>
        public static string Read(string name)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, Folder, name));
        }

        /// <summary>The names of the fixture files that start with <paramref name="prefix"/> and end in <c>.json</c>, sorted.</summary>
        public static string[] Names(string prefix)
        {
            string[] paths = Directory.GetFiles(Path.Combine(Application.dataPath, Folder), prefix + "*.json");
            string[] names = new string[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                names[i] = Path.GetFileName(paths[i]);
            }

            System.Array.Sort(names, System.StringComparer.Ordinal);
            return names;
        }
    }
}
