#nullable enable

using UnityEngine;

namespace CityBuilder.Client.Presentation.Ui
{
    /// <summary>
    /// The one place that knows the two screen coordinate systems apart: the Input System and <c>Screen</c> count pixels from the bottom left,
    /// UI Toolkit panels from the top left, and <c>RuntimePanelUtils.ScreenToPanel</c> scales but does not flip (checked in a PlayMode run, M0-45).
    /// A screen that asks its panel what is under a pointer converts with <see cref="ToTopLeft"/> first.
    /// </summary>
    public static class ScreenPoint
    {
        /// <summary>The same point counted from the top left: x stays, y becomes the screen height minus y.</summary>
        /// <param name="bottomLeft">A position in pixels from the bottom left (an Input System position).</param>
        /// <param name="screenHeight">The screen's height in pixels (<c>Screen.height</c>).</param>
        public static Vector2 ToTopLeft(Vector2 bottomLeft, float screenHeight)
        {
            return new Vector2(bottomLeft.x, screenHeight - bottomLeft.y);
        }
    }
}
