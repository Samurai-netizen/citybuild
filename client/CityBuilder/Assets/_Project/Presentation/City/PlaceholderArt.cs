#nullable enable

using UnityEngine;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The small jobs the art classes of the city view share: a material of one colour for the primitive fallbacks (M0-44), a see-through
    /// material for ghosts, and destroying a Unity object the way the current mode needs. Shared by <see cref="GroundTiles"/> and <see cref="BuildingViewFactory"/>.
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
            return MakeTranslucent(CreateMaterial(new Color(color.r, color.g, color.b, alpha)));
        }

        /// <summary>
        /// Creates the two materials the ghost of a model needs from <paramref name="source"/> (the art's shared material, with its palette texture),
        /// to be put on each renderer in this order. A model has several layers (roof over walls, props), and a see-through material alone would
        /// blend every layer over the ones behind it, so the ghost would look nearly solid and pale. The first material therefore only writes
        /// depth (blending that keeps the colour already on the screen, drawn just before the second), and the second blends the colour with
        /// <paramref name="alpha"/> where it is the nearest layer. Both are copies: the source is not changed. The caller destroys them.
        /// </summary>
        /// <param name="source">The material to copy.</param>
        /// <param name="alpha">How opaque the ghost is, 0 (invisible) to 1 (solid).</param>
        /// <returns>The depth-only material and the see-through one.</returns>
        public static Material[] CreateGhostMaterials(Material source, float alpha)
        {
            var depthOnly = new Material(source);
            if (depthOnly.HasProperty("_SrcBlend"))
            {
                depthOnly.SetFloat("_Surface", 0f);
                depthOnly.SetFloat("_ZWrite", 1f);
                depthOnly.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                depthOnly.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                depthOnly.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.Zero);
                depthOnly.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                depthOnly.SetShaderPassEnabled("ShadowCaster", false);
            }

            // After every opaque object (the ground), before the see-through material.
            depthOnly.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 1;
            return new[] { depthOnly, MakeTranslucent(new Material(source) { color = new Color(1f, 1f, 1f, alpha) }) };
        }

        /// <summary>Switches a material to the render pipeline's transparent surface; a shader without those properties keeps its look and only gets the later render queue.</summary>
        private static Material MakeTranslucent(Material material)
        {
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
