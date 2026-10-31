#nullable enable

using System;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// Puts the player's pointer actions on the city: a drag pans the camera so the ground point under the finger stays under it, a zoom
    /// changes the distance, a tap selects the tile under the pointer and a hover marks it. It owns no input device (<see cref="CityInputReader"/>
    /// reads those) and no economy: it moves a <see cref="CityCameraModel"/>, writes its pose to a <see cref="Camera"/> and updates a
    /// <see cref="TileSelection"/>. Screen positions are in pixels, as the Input System gives them.
    /// </summary>
    public sealed class CityCameraRig
    {
        /// <summary>The camera that shows the city.</summary>
        private readonly Camera _camera;

        /// <summary>The grid whose tiles can be picked.</summary>
        private readonly CityGrid _grid;

        /// <summary>The state a tap and a hover change.</summary>
        private readonly TileSelection _selection;

        /// <summary>The ground plane, y = 0, that screen points are projected onto.</summary>
        private Plane _ground = new Plane(Vector3.up, Vector3.zero);

        /// <summary>Creates the rig and puts the camera in its framed starting pose.</summary>
        /// <param name="camera">The camera to move.</param>
        /// <param name="grid">The grid to look at and pick tiles in.</param>
        /// <param name="selection">The selection that taps and hovers change.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public CityCameraRig(Camera camera, CityGrid grid, TileSelection selection)
        {
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            Model = new CityCameraModel(grid);
            Model.Frame(camera.aspect);
            camera.fieldOfView = CityCameraModel.FieldOfViewDegrees;
            Apply();
        }

        /// <summary>The camera's position maths; tests and views read the focus and the distance from it.</summary>
        public CityCameraModel Model { get; }

        /// <summary>Writes the model's pose to the camera.</summary>
        public void Apply()
        {
            _camera.transform.SetPositionAndRotation(Model.Position, Model.Rotation);
        }

        /// <summary>Finds where the line of sight through a screen point meets the ground. False when the line misses it (above the horizon).</summary>
        public bool TryGetGroundPoint(Vector2 screenPoint, out Vector3 groundPoint)
        {
            Ray ray = _camera.ScreenPointToRay(screenPoint);
            if (_ground.Raycast(ray, out float distance))
            {
                groundPoint = ray.GetPoint(distance);
                return true;
            }

            groundPoint = default;
            return false;
        }

        /// <summary>Finds the tile under a screen point. False when the point is off the grid or above the horizon.</summary>
        public bool TryGetTile(Vector2 screenPoint, out GridCoordinate tile)
        {
            if (TryGetGroundPoint(screenPoint, out Vector3 groundPoint))
            {
                return _grid.TryGetCoordinate(groundPoint, out tile);
            }

            tile = default;
            return false;
        }

        /// <summary>
        /// The pointer moved from <paramref name="previous"/> to <paramref name="current"/> while dragging: the camera pans so the ground point
        /// that was under the pointer is under it now. It stops at the grid's outline.
        /// </summary>
        public void DragBy(Vector2 previous, Vector2 current)
        {
            if (TryGetGroundPoint(previous, out Vector3 before) && TryGetGroundPoint(current, out Vector3 after))
            {
                Model.Pan(new Vector2(before.x - after.x, before.z - after.z));
                Apply();
            }
        }

        /// <summary>Zooms by a factor (see <see cref="CityCameraModel.Zoom"/>: below 1 comes closer).</summary>
        public void Zoom(float factor)
        {
            Model.Zoom(factor);
            Apply();
        }

        /// <summary>The player tapped <paramref name="screenPoint"/>: the tile there is selected, a tap off the grid clears the selection.</summary>
        public void TapAt(Vector2 screenPoint)
        {
            _selection.Tap(TryGetTile(screenPoint, out GridCoordinate tile) ? tile : (GridCoordinate?)null);
        }

        /// <summary>The pointer is over <paramref name="screenPoint"/>, or over nothing (null): the tile there is marked.</summary>
        public void HoverAt(Vector2? screenPoint)
        {
            if (screenPoint.HasValue && TryGetTile(screenPoint.Value, out GridCoordinate tile))
            {
                _selection.SetHovered(tile);
            }
            else
            {
                _selection.SetHovered(null);
            }
        }
    }
}
