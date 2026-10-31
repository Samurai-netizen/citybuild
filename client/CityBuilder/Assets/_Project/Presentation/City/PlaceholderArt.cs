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
