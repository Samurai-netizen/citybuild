#nullable enable

using UnityEngine;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The two small jobs every placeholder-art class of the city view needs (M0 draws everything from primitives, <c>09</c>): a material of one
    /// colour, and destroying a Unity object the way the current mode needs. Shared by <see cref="GroundTiles"/> and <see cref="BuildingViewFactory"/>.
    /// </summary>
    internal static class PlaceholderArt
    {
        /// <summary>
        /// Creates a material of one colour from the render pipeline's default look: a copy of the material a new primitive gets, so no
        /// shader is looked up by name (a lookup could be stripped from a device build). The caller destroys it.
        /// </summary>
        /// <param name="color">The colour of the material.</param>
        /// <returns>A new material.</returns>
        public static Material CreateMaterial(Color color)
        {
            GameObject sample = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var material = new Material(sample.GetComponent<Renderer>().sharedMaterial) { color = color };
            Destroy(sample);
            return material;
        }

        /// <summary>
        /// Creates a see-through material of one colour for the pending ghost of a building: the same default material as
        /// <see cref="CreateMaterial"/>, switched to the render pipeline's transparent surface (alpha blending, no depth writes, drawn after
        /// the opaque objects) and given <paramref name="alpha"/>. A shader without those properties keeps its look and only gets the alpha.
        /// The caller destroys it.
        /// </summary>
        /// <param name="color">The colour of the material; its own alpha is replaced.</param>
        /// <param name="alpha">How opaque it is, 0 (invisible) to 1 (solid).</param>
        /// <returns>A new material.</returns>
        public static Material CreateTranslucentMaterial(Color color, float alpha)
        {
            Material material = CreateMaterial(new Color(color.r, color.g, color.b, alpha));
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetShaderPassEnabled("ShadowCaster", false);
            }

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return material;
        }

        /// <summary>Destroys an object the way the current mode needs: a delayed destroy in play mode, an immediate one in the Editor's edit mode (tests). Null and already destroyed objects are ignored.</summary>
        /// <param name="target">The object to destroy, or null.</param>
        public static void Destroy(Object? target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
