#nullable enable

using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Presentation.Ui
{
    /// <summary>
    /// Scales content down until it fits (owner directive OD-17, M0-59): what does not fit is made smaller as a whole, never cut off and never
    /// given a second scroll direction. <see cref="Factor"/> is the pure maths; <see cref="Height"/> applies it to an element as a uniform scale from
    /// its top left corner, with negative right and bottom margins so that it also takes only the scaled space in its parent's layout (a scroll
    /// view's range included). Stateless helpers.
    /// </summary>
    public static class ScaleToFit
    {
        /// <summary>
        /// The factor that makes <paramref name="needed"/> fit into <paramref name="available"/>: available / needed when it does not fit, else 1.
        /// An unknown size (zero, negative or not a number, as before the first layout) gives 1, so nothing collapses.
        /// </summary>
        /// <param name="needed">The content's size along one axis, unscaled.</param>
        /// <param name="available">The space along that axis.</param>
        public static float Factor(float needed, float available)
        {
            if (!(needed > 0f) || !(available > 0f) || needed <= available)
            {
                return 1f;
            }

            return available / needed;
        }

        /// <summary>
        /// Scales <paramref name="content"/> so that its height fits <paramref name="availableHeight"/>, or back to its full size when it fits.
        /// Call it when the space or the content's size changes (a <c>GeometryChangedEvent</c> of each); the element's own layout size is not
        /// changed by it, so the call does not trigger itself.
        /// </summary>
        /// <param name="content">The element to scale.</param>
        /// <param name="contentSize">Its unscaled layout size (<c>content.layout.size</c>).</param>
        /// <param name="availableHeight">The height it has to fit in.</param>
        /// <returns>The factor applied.</returns>
        public static float Height(VisualElement content, Vector2 contentSize, float availableHeight)
        {
            float factor = Factor(contentSize.y, availableHeight);
            float width = float.IsNaN(contentSize.x) ? 0f : contentSize.x;
            float height = float.IsNaN(contentSize.y) ? 0f : contentSize.y;
            content.style.transformOrigin = new TransformOrigin(new Length(0f), new Length(0f));
            content.style.scale = new Scale(new Vector2(factor, factor));
            content.style.marginRight = factor < 1f ? -width * (1f - factor) : 0f;
            content.style.marginBottom = factor < 1f ? -height * (1f - factor) : 0f;
            return factor;
        }
    }
}
