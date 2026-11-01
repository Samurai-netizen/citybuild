#nullable enable

using System.Collections.Generic;
using CityBuilder.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Presentation.Ui
{
    /// <summary>
    /// The UI icons of the Era I art (CR-0005-T4): finds the picture of an item, a building type or a research node by its content id, and the
    /// three fixed ones for workers, builders and research. The pictures are PNG files in <c>Assets/_Project/Resources/UI/Icons</c> named
    /// <c>item_&lt;id&gt;</c>, <c>bld_&lt;typeId&gt;</c> and <c>res_&lt;id&gt;</c>, loaded by name from <c>Resources</c> (decision 024) the first
    /// time and kept after that, so asking again allocates nothing. A content id with no picture gives null and the view keeps its text. It
    /// shows and decides nothing; it only finds files.
    /// </summary>
    public static class UiIcons
    {
        /// <summary>The folder of the icons under <c>Resources</c>.</summary>
        public const string Folder = "UI/Icons/";

        /// <summary>The prefix of an item icon's name.</summary>
        public const string ItemPrefix = "item_";

        /// <summary>The prefix of a building icon's name.</summary>
        public const string BuildingPrefix = "bld_";

        /// <summary>The prefix of a research icon's name.</summary>
        public const string ResearchPrefix = "res_";

        /// <summary>The name of the workers icon.</summary>
        public const string WorkersName = "workers";

        /// <summary>The name of the builders icon.</summary>
        public const string BuildersName = "builders";

        /// <summary>The name of the icon for research as a whole (the HUD's ring).</summary>
        public const string ResearchName = "research";

        /// <summary>The pictures found so far by their name, with null for a name that has none, so a missing file is looked for once.</summary>
        private static readonly Dictionary<string, Texture2D?> _loaded = new Dictionary<string, Texture2D?>();

        /// <summary>The icon of workers, or null when the file is missing.</summary>
        public static Texture2D? Workers => Find(WorkersName);

        /// <summary>The icon of builders, or null when the file is missing.</summary>
        public static Texture2D? Builders => Find(BuildersName);

        /// <summary>The icon of research as a whole, or null when the file is missing.</summary>
        public static Texture2D? Research => Find(ResearchName);

        /// <summary>The icon of an item (<c>coins</c>, <c>logs</c>, <c>planks</c>), or null when it has none.</summary>
        /// <param name="item">The item's content id.</param>
        public static Texture2D? ForItem(ItemId item) => Find(ItemPrefix + item.Value);

        /// <summary>The icon of a building type, or null when it has none.</summary>
        /// <param name="type">The building type's content id.</param>
        public static Texture2D? ForBuilding(BuildingTypeId type) => Find(BuildingPrefix + type.Value);

        /// <summary>The icon of a research node, or null when it has none.</summary>
        /// <param name="research">The research node's content id.</param>
        public static Texture2D? ForResearch(ResearchId research) => Find(ResearchPrefix + research.Value);

        /// <summary>
        /// Puts an icon on an element as its background, or removes the inline background when there is none (the USS one, if any, shows again). Writes the style only when it
        /// differs, so a refresh with nothing new changes nothing.
        /// </summary>
        /// <param name="element">The element that shows the picture.</param>
        /// <param name="icon">The picture, or null for none.</param>
        public static void Show(VisualElement element, Texture2D? icon)
        {
            // The inline style is compared, not the resolved one, which lags until the element has been laid out.
            StyleBackground current = element.style.backgroundImage;
            bool unchanged = icon == null
                ? current.keyword == StyleKeyword.Null
                : current.keyword == StyleKeyword.Undefined && current.value.texture == icon;
            if (unchanged)
            {
                return;
            }

            element.style.backgroundImage = icon == null ? new StyleBackground(StyleKeyword.Null) : new StyleBackground(icon);
        }

        /// <summary>The picture named <paramref name="name"/> in the icons folder: from the cache, or loaded once.</summary>
        /// <param name="name">The file name without the extension.</param>
        private static Texture2D? Find(string name)
        {
            if (!_loaded.TryGetValue(name, out Texture2D? icon))
            {
                icon = Resources.Load<Texture2D>(Folder + name);
                _loaded[name] = icon;
            }

            return icon;
        }
    }
}
