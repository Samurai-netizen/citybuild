#nullable enable

using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// One primitive of a building's placeholder look (<see cref="BuildingLook"/>): a box or a cylinder with its place, size, turn and colour.
    /// Places and sizes are in world units, relative to the middle of the building's tile on the ground (y = 0), for a level 1 building;
    /// the whole building is stretched upward for higher levels (<see cref="BuildingLooks.HeightPermille"/>). A cylinder's size is its real
    /// extent (diameter, length along its own axis, diameter), not Unity's primitive scale, and its axis is local y before the turn.
    /// </summary>
    public readonly struct BuildingPart
    {
        /// <summary>Creates a part.</summary>
        /// <param name="shape">The primitive: <see cref="PrimitiveType.Cube"/> or <see cref="PrimitiveType.Cylinder"/>.</param>
        /// <param name="center">The middle of the part, relative to the tile's middle on the ground.</param>
        /// <param name="size">The extent of the part before its turn.</param>
        /// <param name="eulerAngles">The turn of the part in degrees (x, y, z), applied after the placement.</param>
        /// <param name="color">The colour of the part.</param>
        public BuildingPart(PrimitiveType shape, Vector3 center, Vector3 size, Vector3 eulerAngles, Color color)
        {
            Shape = shape;
            Center = center;
            Size = size;
            EulerAngles = eulerAngles;
            Color = color;
        }

        /// <summary>The primitive this part is made from.</summary>
        public PrimitiveType Shape { get; }

        /// <summary>The middle of the part, relative to the tile's middle on the ground.</summary>
        public Vector3 Center { get; }

        /// <summary>The extent of the part before its turn.</summary>
        public Vector3 Size { get; }

        /// <summary>The turn of the part in degrees.</summary>
        public Vector3 EulerAngles { get; }

        /// <summary>The colour of the part.</summary>
        public Color Color { get; }

        /// <summary>A box that is not turned.</summary>
        /// <param name="center">The middle of the box.</param>
        /// <param name="size">The width (x), height (y) and depth (z) of the box.</param>
        /// <param name="color">The colour of the box.</param>
        /// <returns>The part.</returns>
        public static BuildingPart Box(Vector3 center, Vector3 size, Color color)
        {
            return new BuildingPart(PrimitiveType.Cube, center, size, Vector3.zero, color);
        }

        /// <summary>A box turned by <paramref name="eulerAngles"/>.</summary>
        /// <param name="center">The middle of the box.</param>
        /// <param name="size">The extent of the box before its turn.</param>
        /// <param name="eulerAngles">The turn in degrees.</param>
        /// <param name="color">The colour of the box.</param>
        /// <returns>The part.</returns>
        public static BuildingPart Box(Vector3 center, Vector3 size, Vector3 eulerAngles, Color color)
        {
            return new BuildingPart(PrimitiveType.Cube, center, size, eulerAngles, color);
        }

        /// <summary>A cylinder lying along the z axis (a log, a wheel seen from the side).</summary>
        /// <param name="center">The middle of the cylinder.</param>
        /// <param name="diameter">The width of its circle.</param>
        /// <param name="length">Its length along the z axis.</param>
        /// <param name="color">The colour of the cylinder.</param>
        /// <returns>The part.</returns>
        public static BuildingPart CylinderAlongZ(Vector3 center, float diameter, float length, Color color)
        {
            return new BuildingPart(PrimitiveType.Cylinder, center, new Vector3(diameter, length, diameter), new Vector3(90f, 0f, 0f), color);
        }
    }
}
