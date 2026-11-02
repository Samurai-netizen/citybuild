#nullable enable

using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Presentation.Ui
{
    /// <summary>
    /// The one runtime copy of <c>UI/PanelSettings</c> that every screen shares (M0-59). It is a copy so that changing the scaling for
    /// portrait never edits the asset in the Editor; <see cref="Refresh"/> follows the screen shape and is called once per frame.
    /// </summary>
    public static class UiPanelSettings
    {
        /// <summary>The Resources path of the panel settings asset.</summary>
        private const string AssetPath = "UI/PanelSettings";

        /// <summary>The shared copy, or null until first used or when the asset is missing.</summary>
        private static PanelSettings? _shared;

        /// <summary>Returns the shared panel settings, loading the asset on first use; null when the asset is missing.</summary>
        public static PanelSettings? Shared()
        {
            if (_shared == null)
            {
                var asset = Resources.Load<PanelSettings>(AssetPath);
                if (asset == null)
                {
                    return null;
                }

                _shared = Object.Instantiate(asset);
                Apply(_shared, Screen.width, Screen.height);
            }

            return _shared;
        }

        /// <summary>Re-applies the scaling for the current screen shape (a phone turned, the Device Simulator rotated).</summary>
        public static void Refresh()
        {
            if (_shared != null)
            {
                Apply(_shared, Screen.width, Screen.height);
            }
        }

        /// <summary>Sets <paramref name="settings"/> to the scaling for a screen size, touching it only when it changes.</summary>
        /// <param name="settings">The panel settings to change.</param>
        /// <param name="screenWidth">The screen's width in pixels.</param>
        /// <param name="screenHeight">The screen's height in pixels.</param>
        public static void Apply(PanelSettings settings, int screenWidth, int screenHeight)
        {
            PanelScaling scaling = PanelScaling.For(screenWidth, screenHeight);
            if (settings.referenceResolution != scaling.ReferenceResolution)
            {
                settings.referenceResolution = scaling.ReferenceResolution;
            }

            if (!Mathf.Approximately(settings.match, scaling.Match))
            {
                settings.match = scaling.Match;
            }
        }
    }
}
