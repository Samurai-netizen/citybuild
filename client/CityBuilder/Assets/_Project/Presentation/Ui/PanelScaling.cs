#nullable enable

using UnityEngine;

namespace CityBuilder.Client.Presentation.Ui
{
    /// <summary>
    /// How the UI panel is scaled for a screen shape (M0-59). A landscape screen is matched on its height at 880 by 400 units; a portrait
    /// screen is matched on its width at 400 by 880 units. Either way one panel unit is about one dp, so a 48 unit button is a 48 dp touch
    /// target and the layouts have about 400 units on the short side. Pure maths on numbers, so a test checks it without a device.
    /// </summary>
    public readonly struct PanelScaling
    {
        /// <summary>The short side of the reference resolution, in panel units.</summary>
        public const int ShortSide = 400;

        /// <summary>The long side of the reference resolution, in panel units.</summary>
        public const int LongSide = 880;

        /// <summary>Creates a scaling.</summary>
        private PanelScaling(Vector2Int referenceResolution, float match)
        {
            ReferenceResolution = referenceResolution;
            Match = match;
        }

        /// <summary>The reference resolution for the panel settings.</summary>
        public Vector2Int ReferenceResolution { get; }

        /// <summary>The match value for the panel settings: 0 matches the width, 1 the height.</summary>
        public float Match { get; }

        /// <summary>Whether this is the portrait scaling.</summary>
        public bool IsPortrait => Match < 0.5f;

        /// <summary>
        /// Chooses the scaling for a screen size in pixels. A square or an unknown (zero) size counts as landscape, the game's main shape.
        /// </summary>
        /// <param name="screenWidth">The screen's width in pixels.</param>
        /// <param name="screenHeight">The screen's height in pixels.</param>
        public static PanelScaling For(int screenWidth, int screenHeight)
        {
            return screenHeight > screenWidth
                ? new PanelScaling(new Vector2Int(ShortSide, LongSide), 0f)
                : new PanelScaling(new Vector2Int(LongSide, ShortSide), 1f);
        }
    }
}
