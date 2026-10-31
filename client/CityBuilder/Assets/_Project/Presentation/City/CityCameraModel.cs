#nullable enable

using System;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// Where the city camera stands: the angled 3/4 view as a focus point on the ground, a distance and two fixed angles. Panning moves the
    /// focus and keeps it inside the grid's outline; zooming changes the distance between a near and a far limit. Plain maths with no
    /// <c>Camera</c> or input in it, so EditMode tests run it; <see cref="CityCameraRig"/> turns pointer input into calls and applies
    /// <see cref="Position"/> and <see cref="Rotation"/>.
    /// </summary>
    public sealed class CityCameraModel
    {
        /// <summary>The compass turn of the view in degrees: 45 looks diagonally across the grid, so the grid shows as a diamond.</summary>
        public const float YawDegrees = 45f;

        /// <summary>How steeply the camera looks down in degrees: 0 is level, 90 straight down.</summary>
        public const float PitchDegrees = 50f;

        /// <summary>The camera's vertical field of view in degrees.</summary>
        public const float FieldOfViewDegrees = 40f;

        /// <summary>The closest the camera comes to its focus, in world units: a few tiles fill the screen.</summary>
        public const float NearestDistance = 3f;

        /// <summary>How much room <see cref="Frame"/> leaves around the grid: 1.15 is a seventh larger than a tight fit.</summary>
        private const float FramingMargin = 1.15f;

        /// <summary>The grid the focus stays inside.</summary>
        private readonly CityGrid _grid;

        /// <summary>Creates a camera model that frames the whole grid for a screen as wide as it is tall; call <see cref="Frame"/> with the real aspect.</summary>
        /// <param name="grid">The grid to look at and stay inside.</param>
        /// <exception cref="ArgumentNullException"><paramref name="grid"/> is null.</exception>
        public CityCameraModel(CityGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Frame(1f);
        }

        /// <summary>The point on the ground the camera looks at: x, and the world's z as y. Always inside the grid's outline.</summary>
        public Vector2 Focus { get; private set; }

        /// <summary>How far the camera is from its focus along its line of sight, between <see cref="NearestDistance"/> and <see cref="FarthestDistance"/>.</summary>
        public float Distance { get; private set; }

        /// <summary>The farthest the camera goes: the distance at which the whole grid fits the screen (set by <see cref="Frame"/>).</summary>
        public float FarthestDistance { get; private set; }

        /// <summary>The camera's turn: it looks down at <see cref="PitchDegrees"/> and across at <see cref="YawDegrees"/>.</summary>
        public Quaternion Rotation => Quaternion.Euler(PitchDegrees, YawDegrees, 0f);

        /// <summary>The camera's world position: back along its line of sight from the focus on the ground.</summary>
        public Vector3 Position => new Vector3(Focus.x, 0f, Focus.y) - Rotation * Vector3.forward * Distance;

        /// <summary>
        /// Looks at the middle of the grid from the farthest distance, which is the one where the whole grid fits a screen of the given shape
        /// with a margin. A narrow (portrait) screen is farther than a wide one.
        /// </summary>
        /// <param name="aspect">The screen's width divided by its height; a value that is not a positive number counts as 1.</param>
        public void Frame(float aspect)
        {
            if (!(aspect > 0f) || float.IsInfinity(aspect))
            {
                aspect = 1f;
            }

            // The grid fits in a circle on the ground; the view must see that circle's radius both up and sideways.
            float radius = _grid.WorldSize.magnitude * 0.5f;
            float halfVertical = Mathf.Tan(FieldOfViewDegrees * 0.5f * Mathf.Deg2Rad);
            float halfSmaller = aspect >= 1f ? halfVertical : halfVertical * aspect;

            FarthestDistance = Mathf.Max(NearestDistance, radius / halfSmaller * FramingMargin);
            Distance = FarthestDistance;
            Focus = _grid.WorldCenter;
        }

        /// <summary>Moves the focus by a distance on the ground (x, and z as y); the focus stops at the grid's outline.</summary>
        public void Pan(Vector2 groundDelta)
        {
            if (float.IsNaN(groundDelta.x) || float.IsNaN(groundDelta.y))
            {
                return;
            }

            Focus = _grid.ClampToBounds(Focus + groundDelta);
        }

        /// <summary>
        /// Changes the distance by a factor: below 1 comes closer, above 1 goes farther. The result stays between the limits. A factor that
        /// is not a positive number is ignored.
        /// </summary>
        public void Zoom(float factor)
        {
            if (!(factor > 0f) || float.IsInfinity(factor))
            {
                return;
            }

            Distance = Mathf.Clamp(Distance * factor, NearestDistance, FarthestDistance);
        }
    }
}
