#nullable enable

using System;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// Where the city camera stands: the angled 3/4 view as a focus point on the ground, a distance and two fixed angles. Panning moves the
    /// focus and keeps it inside the limits' outline (the open land, M1-03; the whole grid by default); zooming changes the distance between a near and a far limit. Plain maths with no
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

        /// <summary>The tiles the focus stays over and the framing fits (M1-03): the whole grid until <see cref="SetLimits"/>.</summary>
        private RectInt _limits;

        /// <summary>The screen shape of the last <see cref="Frame"/>, so new limits can work out the farthest distance again.</summary>
        private float _aspect = 1f;

        /// <summary>Creates a camera model that frames the whole grid for a screen as wide as it is tall; call <see cref="Frame"/> with the real aspect.</summary>
        /// <param name="grid">The grid to look at and stay inside.</param>
        /// <exception cref="ArgumentNullException"><paramref name="grid"/> is null.</exception>
        public CityCameraModel(CityGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _limits = new RectInt(0, 0, grid.Width, grid.Height);
            Frame(1f);
        }

        /// <summary>The point on the ground the camera looks at: x, and the world's z as y. Always inside the limits' outline (the grid's by default).</summary>
        public Vector2 Focus { get; private set; }

        /// <summary>How far the camera is from its focus along its line of sight, between <see cref="NearestDistance"/> and <see cref="FarthestDistance"/>.</summary>
        public float Distance { get; private set; }

        /// <summary>The farthest the camera goes: the distance at which the limits (the whole grid by default) fit the screen (set by <see cref="Frame"/>).</summary>
        public float FarthestDistance { get; private set; }

        /// <summary>The camera's turn: it looks down at <see cref="PitchDegrees"/> and across at <see cref="YawDegrees"/>.</summary>
        public Quaternion Rotation => Quaternion.Euler(PitchDegrees, YawDegrees, 0f);

        /// <summary>The camera's world position: back along its line of sight from the focus on the ground.</summary>
        public Vector3 Position => new Vector3(Focus.x, 0f, Focus.y) - Rotation * Vector3.forward * Distance;

        /// <summary>
        /// Looks at the middle of the limits from the farthest distance, which is the one where the limits (the whole grid by default) fit a
        /// screen of the given shape with a margin. A narrow (portrait) screen is farther than a wide one.
        /// </summary>
        /// <param name="aspect">The screen's width divided by its height; a value that is not a positive number counts as 1.</param>
        public void Frame(float aspect)
        {
            if (!(aspect > 0f) || float.IsInfinity(aspect))
            {
                aspect = 1f;
            }

            _aspect = aspect;
            FarthestDistance = FittingDistance();
            Distance = FarthestDistance;
            Focus = LimitsCenter();
        }

        /// <summary>
        /// Sets the tiles the camera may look over (M1-03, <see cref="CameraLimits"/>), clipped to the grid: the focus moves inside them if it
        /// was outside, the farthest distance fits them, and the distance only shrinks if it is now too far. Opening land so widens the view
        /// without a jump.
        /// </summary>
        /// <param name="tiles">The open land's rectangle of tiles; an empty one is ignored.</param>
        public void SetLimits(RectInt tiles)
        {
            int xMin = Mathf.Max(0, tiles.xMin);
            int yMin = Mathf.Max(0, tiles.yMin);
            int xMax = Mathf.Min(_grid.Width, tiles.xMax);
            int yMax = Mathf.Min(_grid.Height, tiles.yMax);
            if (xMax <= xMin || yMax <= yMin)
            {
                return;
            }

            _limits = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
            FarthestDistance = FittingDistance();
            Distance = Mathf.Min(Distance, FarthestDistance);
            Focus = ClampToLimits(Focus);
        }

        /// <summary>The distance at which the limits' bounding circle fits the last screen shape with the margin, never nearer than <see cref="NearestDistance"/>.</summary>
        private float FittingDistance()
        {
            // The limits fit in a circle on the ground; the view must see that circle's radius both up and sideways.
            float radius = new Vector2(_limits.width * CityGrid.TileSize, _limits.height * CityGrid.TileSize).magnitude * 0.5f;
            float halfVertical = Mathf.Tan(FieldOfViewDegrees * 0.5f * Mathf.Deg2Rad);
            float halfSmaller = _aspect >= 1f ? halfVertical : halfVertical * _aspect;
            return Mathf.Max(NearestDistance, radius / halfSmaller * FramingMargin);
        }

        /// <summary>The middle of the limits on the ground.</summary>
        private Vector2 LimitsCenter()
        {
            return new Vector2(_limits.x + _limits.width * 0.5f, _limits.y + _limits.height * 0.5f) * CityGrid.TileSize;
        }

        /// <summary>The point of the limits' outline nearest to <paramref name="ground"/> (the point itself when inside).</summary>
        private Vector2 ClampToLimits(Vector2 ground)
        {
            return new Vector2(
                Mathf.Clamp(ground.x, _limits.xMin * CityGrid.TileSize, _limits.xMax * CityGrid.TileSize),
                Mathf.Clamp(ground.y, _limits.yMin * CityGrid.TileSize, _limits.yMax * CityGrid.TileSize));
        }

        /// <summary>Moves the focus by a distance on the ground (x, and z as y); the focus stops at the limits' outline (the grid's by default).</summary>
        public void Pan(Vector2 groundDelta)
        {
            if (float.IsNaN(groundDelta.x) || float.IsNaN(groundDelta.y))
            {
                return;
            }

            Focus = ClampToLimits(Focus + groundDelta);
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
