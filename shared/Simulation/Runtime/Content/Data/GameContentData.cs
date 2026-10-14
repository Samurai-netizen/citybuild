#nullable enable
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content.Data
{
    /// <summary>
    /// The content as read from a file or a response, before any check: the root of the JSON document. Every value may be missing
    /// (null); <see cref="GameContentFactory.Create"/> checks it all and builds the immutable <see cref="GameContent"/> from it. The
    /// classes in this namespace are plain settable containers, so any JSON library can fill them (System.Text.Json on the server,
    /// Newtonsoft in Unity) and a test can change one value of valid data.
    /// </summary>
    public sealed class GameContentData
    {
        /// <summary>Required. The version of the content, for example <c>mvp-1</c>; any change to the content needs a new one.</summary>
        public string? ContentVersion { get; set; }

        /// <summary>Required. The global rules.</summary>
        public GameRulesData? Rules { get; set; }

        /// <summary>Required. What a new city starts with.</summary>
        public StartingCityData? StartingCity { get; set; }

        /// <summary>Required, at least one. The eras, numbered 1, 2, … in any order.</summary>
        public List<EraData?>? Eras { get; set; }

        /// <summary>Required. The items, coins included.</summary>
        public List<ItemData?>? Items { get; set; }

        /// <summary>Required. The building types.</summary>
        public List<BuildingData?>? Buildings { get; set; }

        /// <summary>Required. The research nodes of every era.</summary>
        public List<ResearchData?>? Research { get; set; }
    }
}
