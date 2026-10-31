#nullable enable

using UnityEngine;

namespace CityBuilder.Client.Presentation.Ui
{
    /// <summary>
    /// How far a screen's safe area (notches, rounded corners, system bars) is inside the screen, in the units of a UI panel, so a screen can
    /// pad its root by it (<c>09</c> UX rules: respect <c>Screen.safeArea</c> on the UI root). Pure maths on numbers, so a test checks it
    /// without a device; the screen supplies <c>Screen.safeArea</c>, <c>Screen.width/height</c> and its root's size.
    /// </summary>
    public readonly struct SafeAreaInsets
    {
        /// <summary>Creates insets in panel units.</summary>
        private SafeAreaInsets(float left, float top, float right, float bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        /// <summary>The gap between the screen's left edge and the safe area.</summary>
        public float Left { get; }

        /// <summary>The gap between the screen's top edge and the safe area.</summary>
        public float Top { get; }

        /// <summary>The gap between the screen's right edge and the safe area.</summary>
        public float Right { get; }

        /// <summary>The gap between the screen's bottom edge and the safe area.</summary>
        public float Bottom { get; }

        /// <summary>
        /// Converts the safe area to panel units. Screen coordinates have their origin at the bottom left, as <c>Screen.safeArea</c> does. A zero
        /// screen or panel size, or a safe area that is the whole screen, gives no insets; a safe area outside the screen is clamped to it.
        /// </summary>
        /// <param name="safeArea">The safe area in screen pixels (<c>Screen.safeArea</c>).</param>
        /// <param name="screenWidth">The screen's width in pixels.</param>
        /// <param name="screenHeight">The screen's height in pixels.</param>
        /// <param name="panelWidth">The width of the UI root in panel units.</param>
        /// <param name="panelHeight">The height of the UI root in panel units.</param>
        public static SafeAreaInsets From(Rect safeArea, int screenWidth, int screenHeight, float panelWidth, float panelHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0 || panelWidth <= 0f || panelHeight <= 0f)
            {
                return new SafeAreaInsets(0f, 0f, 0f, 0f);
            }

            float scaleX = panelWidth / screenWidth;
            float scaleY = panelHeight / screenHeight;
            return new SafeAreaInsets(
                Mathf.Max(0f, safeArea.xMin) * scaleX,
                Mathf.Max(0f, screenHeight - safeArea.yMax) * scaleY,
                Mathf.Max(0f, screenWidth - safeArea.xMax) * scaleX,
                Mathf.Max(0f, safeArea.yMin) * scaleY);
        }
    }
}
