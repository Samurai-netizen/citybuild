#nullable enable

using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Presentation.Ui
{
    /// <summary>
    /// What every screen with a <see cref="UIDocument"/> of its own needs from its root element, in one place (<c>docs/how-to/add-ui-screen.md</c>):
    /// to tell whether a screen position is over something that takes the pointer, so the ground leaves that press to the UI, and to pad the
    /// root by the screen's safe area. Pure helpers over a root; they keep no state.
    /// </summary>
    public static class UiRoot
    {
        /// <summary>
        /// Whether a screen position (pixels, origin bottom left) is over an element of <paramref name="root"/>'s panel that takes the pointer.
        /// False when there is no root or it is not in a panel yet.
        /// </summary>
        /// <param name="root">The screen's root element, or null before the document exists.</param>
        /// <param name="screenPosition">The position, as the Input System gives it.</param>
        public static bool IsOver(VisualElement? root, Vector2 screenPosition)
        {
            if (root == null || root.panel == null)
            {
                return false;
            }

            // The panel counts from the top left and the Input System from the bottom left; ScreenToPanel only scales.
            Vector2 topLeft = ScreenPoint.ToTopLeft(screenPosition, Screen.height);
            return root.panel.Pick(RuntimePanelUtils.ScreenToPanel(root.panel, topLeft)) != null;
        }

        /// <summary>Pads <paramref name="root"/> by the screen's safe area on top of <paramref name="basePadding"/>, in panel units.</summary>
        /// <param name="root">The screen's root element, laid out already (its resolved size is read).</param>
        /// <param name="basePadding">The padding in panel units before the safe area is added.</param>
        public static void PadBySafeArea(VisualElement root, float basePadding)
        {
            SafeAreaInsets insets = SafeAreaInsets.From(
                Screen.safeArea, Screen.width, Screen.height, root.resolvedStyle.width, root.resolvedStyle.height);
            root.style.paddingLeft = basePadding + insets.Left;
            root.style.paddingTop = basePadding + insets.Top;
            root.style.paddingRight = basePadding + insets.Right;
            root.style.paddingBottom = basePadding + insets.Bottom;
        }
    }
}
