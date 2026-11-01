#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Client.Net;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The city's place in the scene: the camera and a light, made from code as soon as the object exists (so the screen is cleared even
    /// before the server answers), and, once <see cref="ShowGrid"/> is called with the grid size from the content's rules, the ground
    /// tiles, the camera controls and the tile selection, and, with <see cref="ShowCity"/>, the buildings, and, with <see cref="ShowGhosts"/>, the
    /// translucent buildings of commands on their way. A thin component: the logic is in
    /// <see cref="CityCameraModel"/>, <see cref="CityCameraRig"/>, <see cref="CityInputReader"/>, <see cref="TileSelection"/>,
    /// <see cref="GroundTiles"/>, <see cref="CityView"/> and <see cref="PlacementGhosts"/>. <c>GameRoot</c> creates it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CityWorld : MonoBehaviour
    {
        /// <summary>The colour the camera clears the screen to: dark, so the gaps between tiles read as grid lines.</summary>
        private static readonly Color Background = new Color(0.10f, 0.12f, 0.14f);

        /// <summary>The ground, or null before <see cref="ShowGrid"/>.</summary>
        private GroundTiles? _ground;

        /// <summary>The input reader, or null before <see cref="ShowGrid"/>.</summary>
        private CityInputReader? _input;

        /// <summary>The building views, or null before <see cref="ShowGrid"/>.</summary>
        private CityView? _buildings;

        /// <summary>The pending-building ghosts, or null before <see cref="ShowGrid"/>.</summary>
        private PlacementGhosts? _ghosts;

        /// <summary>
        /// Tells whether a screen position (pixels, origin bottom left) is over the UI, so a press there is the UI's and not the ground's; set by
        /// <c>GameRoot</c> once the UI exists. Read when the pointer is pressed, so setting it after <see cref="ShowGrid"/> works. Null: no UI.
        /// </summary>
        public Func<Vector2, bool>? IsPointerOverUi { get; set; }

        /// <summary>The camera that shows the city; made in <c>Awake</c>.</summary>
        public Camera Camera { get; private set; } = null!;

        /// <summary>The camera controls; null before <see cref="ShowGrid"/>.</summary>
        public CityCameraRig? Rig { get; private set; }

        /// <summary>The hovered and selected tile; null before <see cref="ShowGrid"/>.</summary>
        public TileSelection? Selection { get; private set; }

        /// <summary>The grid shown; null before <see cref="ShowGrid"/>.</summary>
        public CityGrid? Grid { get; private set; }

        /// <summary>The ground tiles; null before <see cref="ShowGrid"/>.</summary>
        public GroundTiles? Ground => _ground;

        /// <summary>The building views; null before <see cref="ShowGrid"/>.</summary>
        public CityView? Buildings => _buildings;

        /// <summary>The ghosts of buildings on their way; null before <see cref="ShowGrid"/>.</summary>
        public PlacementGhosts? Ghosts => _ghosts;

        /// <summary>Unity calls it once, right after the component is created: it makes the camera and the light.</summary>
        private void Awake()
        {
            var cameraObject = new GameObject("City Camera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(transform, false);
            Camera = cameraObject.AddComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = Background;
            Camera.fieldOfView = CityCameraModel.FieldOfViewDegrees;
            Camera.nearClipPlane = 0.3f;
            Camera.farClipPlane = 200f;

            var lightObject = new GameObject("Sun");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.None;
        }

        /// <summary>
        /// Builds the ground, the selection, the camera controls and the (empty) building views for a grid, and frames the camera on it.
        /// Calling it again replaces the earlier ground and buildings (a new city size); <see cref="ShowCity"/> then fills the buildings.
        /// </summary>
        /// <param name="grid">The grid from the content's rules.</param>
        public void ShowGrid(CityGrid grid)
        {
            _ground?.Destroy();
            _buildings?.Destroy();
            _ghosts?.Destroy();
            Grid = grid;
            Selection = new TileSelection(grid);
            _ground = new GroundTiles(grid, Selection, transform);
            _buildings = new CityView(grid, transform, Camera.transform);
            _ghosts = new PlacementGhosts(grid, transform);
            Rig = new CityCameraRig(Camera, grid, Selection);
            _input = new CityInputReader(Rig, Screen.dpi, position => IsPointerOverUi != null && IsPointerOverUi(position));
        }

        /// <summary>Makes the building views match a city (<see cref="CityView.Show"/>). Does nothing before <see cref="ShowGrid"/>.</summary>
        /// <param name="city">The displayed city.</param>
        /// <param name="content">The game content, which the building statuses need; null shows no status.</param>
        public void ShowCity(CityState city, GameContent? content = null)
        {
            _buildings?.Show(city, content);
        }

        /// <summary>Paints the tiles green or red by their hints, or gives them their ground colour back (<see cref="GroundTiles.ShowHints"/>). Does nothing before <see cref="ShowGrid"/>.</summary>
        /// <param name="hints">One hint per tile, row by row, or null to show none.</param>
        public void ShowHints(TileHint[]? hints)
        {
            _ground?.ShowHints(hints);
        }

        /// <summary>Makes the ghosts match the commands on their way (<see cref="PlacementGhosts.Show"/>). Does nothing before <see cref="ShowGrid"/>.</summary>
        /// <param name="pending">The command sender's pending commands.</param>
        /// <param name="era">The city's era.</param>
        public void ShowGhosts(IReadOnlyList<PendingCommand> pending, int era)
        {
            _ghosts?.Show(pending, era);
        }

        /// <summary>Unity calls it every frame: the player's input moves the camera and the selection.</summary>
        private void Update()
        {
            _input?.Tick();
        }

        /// <summary>Unity calls it when the object is destroyed: the ground takes its listener off the selection, and the buildings' and ghosts' materials go.</summary>
        private void OnDestroy()
        {
            _ground?.Destroy();
            _buildings?.Destroy();
            _ghosts?.Destroy();
        }
    }
}
