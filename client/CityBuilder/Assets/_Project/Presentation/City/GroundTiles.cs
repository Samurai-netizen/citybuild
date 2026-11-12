#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Map;
using CityBuilder.Simulation.State;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The ground of the city: one flat tile per grid cell, an instance of the art's grass tile (CR-0005), varied by the grass variants of visual v2
    /// and their quarter turns (<see cref="GroundVariants"/>); if the tile model is missing, a cube primitive in two greens like a chessboard instead. Plus a hover marker and a selection marker that follow a <see cref="TileSelection"/>.
    /// The tiles have no colliders: the pointer's tile is found with maths (<see cref="CityGrid"/>), not physics. <see cref="Destroy"/> removes
    /// everything it made.
    /// </summary>
    public sealed class GroundTiles
    {
        /// <summary>How much of a tile's width the tile block fills: the small gap lets the dark background draw the grid lines.</summary>
        private const float TileFill = 0.96f;

        /// <summary>The height of a tile block; its top is the ground plane (y = 0).</summary>
        private const float TileThickness = 0.1f;

        /// <summary>The height of a marker block, which sits on the ground.</summary>
        private const float MarkerThickness = 0.02f;

        /// <summary>
        /// How far the path markers and the road preview float above the ground plane: just over the tallest grass detail of visual v2
        /// (0.084 above the plane), so the tiles never hide them (CR-0008-T3). Display only.
        /// </summary>
        public const float OverlayLift = 0.09f;

        /// <summary>How opaque a road of the road preview is: the real road model at 75 % transparency (the owner, CR-0008-T3).</summary>
        public const float RoadPreviewAlpha = 0.25f;

        /// <summary>How much of a tile's width a marker fills: a little more than a tile block, so it frames it.</summary>
        private const float MarkerFill = 1f;

        /// <summary>One of the two chessboard colours.</summary>
        private static readonly Color LightGround = new Color(0.45f, 0.62f, 0.32f);

        /// <summary>The other chessboard colour.</summary>
        private static readonly Color DarkGround = new Color(0.40f, 0.56f, 0.28f);

        /// <summary>The colour of the marker on the tile under the pointer.</summary>
        private static readonly Color HoverColor = new Color(0.95f, 0.88f, 0.55f);

        /// <summary>The colour of the marker on the selected tile.</summary>
        private static readonly Color SelectionColor = new Color(0.95f, 0.55f, 0.20f);

        /// <summary>The colour of a free tile while the player picks where to build: brighter than the ground, so it reads as lit.</summary>
        private static readonly Color FreeHintColor = new Color(0.30f, 0.90f, 0.35f);

        /// <summary>The colour of an occupied tile while the player picks where to build.</summary>
        private static readonly Color OccupiedHintColor = new Color(0.85f, 0.20f, 0.20f);

        /// <summary>The colour of a path step marker while the player sets up a trip or route (CR-0008).</summary>
        private static readonly Color PathColor = new Color(0.95f, 0.78f, 0.25f);

        /// <summary>How much of a tile's width a path step marker fills.</summary>
        private const float PathMarkerFill = 0.36f;

        /// <summary>The grid the tiles follow.</summary>
        private readonly CityGrid _grid;

        /// <summary>The state the markers show.</summary>
        private readonly TileSelection _selection;

        /// <summary>The materials this object created, destroyed with it. The first two are the chessboard greens of the cube fallback, and stay null when tile models are used.</summary>
        private readonly Material?[] _materials = new Material?[7];

        /// <summary>Pooled step markers that draw a preview path across the map (CR-0008).</summary>
        private readonly List<GameObject> _pathMarkers = new List<GameObject>();

        /// <summary>The path steps shown now; empty when no path is shown (CR-0008).</summary>
        private IReadOnlyList<(int X, int Y)> _shownPath = Array.Empty<(int X, int Y)>();

        /// <summary>Pooled see-through roads of the road preview, made when first needed (CR-0008-T3).</summary>
        private readonly List<GameObject> _roadGhosts = new List<GameObject>();

        /// <summary>The tiles the road preview shows now, inside the grid; empty when it shows none.</summary>
        private readonly List<(int X, int Y)> _shownRoadPreview = new List<(int X, int Y)>();

        /// <summary>The depth-only and the see-through copy of the road model's material that the road preview uses; made with the first ghost.</summary>
        private Material[]? _roadGhostMaterials;

        /// <summary>The material each tile shows when it has no hint, row by row: the art's shared material, or a chessboard green.</summary>
        private readonly Material[] _groundMaterials;

        /// <summary>The tile blocks, row by row (<c>y * width + x</c>), so a hint can change a tile's material.</summary>
        private readonly Renderer[] _tileRenderers;

        /// <summary>The tile objects, row by row (<c>y * width + x</c>), so a road can replace a tile in place.</summary>
        private readonly GameObject[] _tileObjects;

        /// <summary>The road level each tile shows now, row by row; 0 while the tile shows grass (CR-0008).</summary>
        private readonly int[] _roadLevels;

        /// <summary>Scratch road levels for the next <see cref="ShowRoads"/> call, row by row (CR-0008).</summary>
        private readonly int[] _scratchRoadLevels;

        /// <summary>The default grass model passed to the constructor; null in the cube fallback.</summary>
        private readonly GameObject? _tileModel;

        /// <summary>The grass variants passed to the constructor; null when variants are not used.</summary>
        private readonly GameObject?[]? _variants;

        /// <summary>The hint each tile shows now, row by row; null while the tile shows its ground colour.</summary>
        private readonly TileHint?[] _shownHints;

        /// <summary>Whether <see cref="Destroy"/> has run.</summary>
        private bool _destroyed;

        /// <summary>
        /// Builds the tiles and the markers under a new object called <c>Ground</c> that is a child of <paramref name="parent"/>. Each tile is
        /// one of the grass variants of visual v2 (<see cref="GroundVariants"/>), and the plain grass tile where a variant is missing.
        /// </summary>
        /// <param name="grid">The grid to cover.</param>
        /// <param name="selection">The state the markers show; this object listens to it until <see cref="Destroy"/>.</param>
        /// <param name="parent">The transform the ground hangs under.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public GroundTiles(CityGrid grid, TileSelection selection, Transform parent)
            : this(grid, selection, parent, Resources.Load<GameObject>(BuildingLooks.GrassTilePath), LoadVariants())
        {
        }

        /// <summary>
        /// Builds the tiles from <paramref name="tileModel"/> (or, when it is null, from cubes) and the markers, like the other constructor.
        /// Lets a test build the fallback.
        /// </summary>
        /// <param name="grid">The grid to cover.</param>
        /// <param name="selection">The state the markers show; this object listens to it until <see cref="Destroy"/>.</param>
        /// <param name="parent">The transform the ground hangs under.</param>
        /// <param name="tileModel">The model of one tile, whose top is the ground plane, or null for cube blocks.</param>
        /// <exception cref="ArgumentNullException">An argument other than <paramref name="tileModel"/> is null.</exception>
        public GroundTiles(CityGrid grid, TileSelection selection, Transform parent, GameObject? tileModel)
            : this(grid, selection, parent, tileModel, null)
        {
        }

        /// <summary>
        /// Builds the tiles and the markers: each tile from its grass variant in <paramref name="variants"/> when there is one, else from
        /// <paramref name="tileModel"/>, else from a cube. With variants, every model tile is turned by its quarter turns
        /// (<see cref="GroundVariants.RotationFor"/>).
        /// </summary>
        /// <param name="grid">The grid to cover.</param>
        /// <param name="selection">The state the markers show; this object listens to it until <see cref="Destroy"/>.</param>
        /// <param name="parent">The transform the ground hangs under.</param>
        /// <param name="tileModel">The model of one tile, whose top is the ground plane, or null for cube blocks.</param>
        /// <param name="variants">The grass variants by <see cref="GroundVariants.For"/>, any of them null, or null for none.</param>
        /// <exception cref="ArgumentNullException">An argument other than the models is null.</exception>
        private GroundTiles(CityGrid grid, TileSelection selection, Transform parent, GameObject? tileModel, GameObject?[]? variants)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            Root = new GameObject("Ground");
            Root.transform.SetParent(parent, false);

            _tileModel = tileModel;
            _variants = variants;
            _tileObjects = new GameObject[grid.Width * grid.Height];
            _tileRenderers = new Renderer[grid.Width * grid.Height];
            _groundMaterials = new Material[grid.Width * grid.Height];
            _shownHints = new TileHint?[grid.Width * grid.Height];
            _roadLevels = new int[grid.Width * grid.Height];
            _scratchRoadLevels = new int[grid.Width * grid.Height];
            if (tileModel == null)
            {
                _materials[0] = PlaceholderArt.CreateMaterial(LightGround);
                _materials[1] = PlaceholderArt.CreateMaterial(DarkGround);
            }

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    BuildTileAt(x, y, 0);
                }
            }

            HoverMarker = CreateMarker("Hover marker", _materials[2] = PlaceholderArt.CreateMaterial(HoverColor), 0f);
            SelectionMarker = CreateMarker("Selection marker", _materials[3] = PlaceholderArt.CreateMaterial(SelectionColor), MarkerThickness);
            _materials[4] = PlaceholderArt.CreateMaterial(FreeHintColor);
            _materials[5] = PlaceholderArt.CreateMaterial(OccupiedHintColor);
            _materials[6] = PlaceholderArt.CreateMaterial(PathColor);

            _selection.Changed += ShowSelection;
            ShowSelection();
        }

        /// <summary>The object that holds every tile and marker.</summary>
        public GameObject Root { get; }

        /// <summary>The marker that sits on the hovered tile; inactive while no tile is hovered.</summary>
        public GameObject HoverMarker { get; }

        /// <summary>The marker that sits on the selected tile, above the hover marker; inactive while no tile is selected.</summary>
        public GameObject SelectionMarker { get; }

        /// <summary>The number of tile blocks: the grid's width times its height.</summary>
        public int TileCount => _grid.Width * _grid.Height;

        /// <summary>
        /// Paints the tiles by their hints while the player picks where to build: a free tile green, an occupied one red. <c>null</c> gives
        /// every tile its ground colour back. A tile whose hint did not change is not touched, so calling it each second allocates nothing.
        /// A hint array of the wrong size is a programming mistake.
        /// </summary>
        /// <param name="hints">One hint per tile, row by row (<c>y * width + x</c>), or null to show none.</param>
        /// <exception cref="ArgumentException"><paramref name="hints"/> does not have one hint per tile.</exception>
        public void ShowHints(TileHint[]? hints)
        {
            if (hints != null && hints.Length != _tileRenderers.Length)
            {
                throw new ArgumentException("There must be one hint per tile (" + _tileRenderers.Length + ").", nameof(hints));
            }

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    int index = y * _grid.Width + x;
                    TileHint? hint = hints == null ? (TileHint?)null : hints[index];
                    if (_shownHints[index] == hint)
                    {
                        continue;
                    }

                    _shownHints[index] = hint;
                    _tileRenderers[index].sharedMaterial = hint == null
                        ? _groundMaterials[index]
                        : (hint == TileHint.Occupied ? _materials[5]! : _materials[4]!);
                }
            }
        }

        /// <summary>The hint a tile shows now, or null when it shows its ground colour (or is outside the grid).</summary>
        public TileHint? ShownHintAt(GridCoordinate tile)
        {
            return _grid.Contains(tile) ? _shownHints[tile.Y * _grid.Width + tile.X] : null;
        }

        /// <summary>The road level a tile shows now (1 or 2), or 0 when it shows grass or lies outside the grid (CR-0008).</summary>
        public int ShownRoadLevelAt(GridCoordinate tile)
        {
            return _grid.Contains(tile) ? _roadLevels[tile.Y * _grid.Width + tile.X] : 0;
        }

        /// <summary>
        /// Makes the ground tiles match <paramref name="roads"/> by level and shows each road tile's arms toward neighbouring road tiles
        /// (<see cref="RoadArms"/>, CR-0008). When the roads have not changed, allocates nothing and touches no GameObjects.
        /// </summary>
        /// <param name="roads">The road tiles of the displayed city, or null for no roads.</param>
        public void ShowRoads(IReadOnlyList<RoadTile>? roads)
        {
            Array.Clear(_scratchRoadLevels, 0, _scratchRoadLevels.Length);
            if (roads != null)
            {
                for (int i = 0; i < roads.Count; i++)
                {
                    RoadTile road = roads[i];
                    var coord = new GridCoordinate(road.X, road.Y);
                    if (_grid.Contains(coord))
                    {
                        _scratchRoadLevels[road.Y * _grid.Width + road.X] = road.Level;
                    }
                }
            }

            bool anyChanged = false;
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    int index = y * _grid.Width + x;
                    int nextLevel = _scratchRoadLevels[index];
                    if (_roadLevels[index] == nextLevel)
                    {
                        continue;
                    }

                    anyChanged = true;
                    _roadLevels[index] = nextLevel;
                    BuildTileAt(x, y, nextLevel);
                }
            }

            if (!anyChanged)
            {
                return;
            }

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    int index = y * _grid.Width + x;
                    if (_roadLevels[index] <= 0)
                    {
                        continue;
                    }

                    RoadArms.Show(
                        _tileObjects[index].transform,
                        HasRoad(x + 1, y),
                        HasRoad(x - 1, y),
                        HasRoad(x, y + 1),
                        HasRoad(x, y - 1));
                }
            }
        }

        /// <summary>Whether the tile at (<paramref name="x"/>, <paramref name="y"/>) is inside the grid and shows a road.</summary>
        private bool HasRoad(int x, int y)
        {
            var coord = new GridCoordinate(x, y);
            return _grid.Contains(coord) && _roadLevels[y * _grid.Width + x] > 0;
        }

        /// <summary>Builds or replaces the tile object at (<paramref name="x"/>, <paramref name="y"/>) for road <paramref name="roadLevel"/> (0 = grass).</summary>
        private void BuildTileAt(int x, int y, int roadLevel)
        {
            var tile = new GridCoordinate(x, y);
            int index = y * _grid.Width + x;
            if (_tileObjects[index] != null)
            {
                PlaceholderArt.Destroy(_tileObjects[index]);
            }

            GameObject? model = null;
            bool turnGrass = false;
            if (roadLevel > 0 && _tileModel != null)
            {
                string roadPath = RoadArms.PathOf(roadLevel, RoadArms.VariantFor(x, y));
                model = Resources.Load<GameObject>(roadPath) ?? Resources.Load<GameObject>(RoadArms.PathOf(roadLevel, 0)) ?? _tileModel;
            }
            else if (_tileModel != null)
            {
                model = _tileModel;
                if (_variants != null)
                {
                    GameObject? variant = _variants[GroundVariants.For(x, y)];
                    if (variant != null)
                    {
                        model = variant;
                    }

                    turnGrass = true;
                }
            }

            GameObject instance;
            if (model != null)
            {
                instance = Object.Instantiate(model, Root.transform, false);
                instance.name = "Tile " + tile;
                instance.transform.localPosition = _grid.ToWorldCenter(tile);
                if (turnGrass)
                {
                    instance.transform.localRotation = Quaternion.Euler(0f, 90f * GroundVariants.RotationFor(x, y), 0f) * instance.transform.localRotation;
                }

                _tileRenderers[index] = instance.GetComponentInChildren<Renderer>();
            }
            else
            {
                instance = CreateBlock("Tile " + tile, TileFill, TileThickness, -TileThickness * 0.5f, _grid.ToWorldCenter(tile));
                _tileRenderers[index] = instance.GetComponent<Renderer>();
                _tileRenderers[index].sharedMaterial = (x + y) % 2 == 0 ? _materials[0]! : _materials[1]!;
            }

            _tileObjects[index] = instance;
            _groundMaterials[index] = _tileRenderers[index].sharedMaterial;
            TileHint? hint = _shownHints[index];
            if (hint != null)
            {
                _tileRenderers[index].sharedMaterial = hint == TileHint.Occupied ? _materials[5]! : _materials[4]!;
            }
        }

        /// <summary>The path steps shown on the ground now; empty when no path is shown (CR-0008).</summary>
        public IReadOnlyList<(int X, int Y)> ShownPath => _shownPath;

        /// <summary>Draws <paramref name="steps"/> across the ground with pooled step markers, or hides them for null/empty (CR-0008).</summary>
        /// <param name="steps">The tile steps from <see cref="PathFinder.Route"/>, or null to clear.</param>
        public void ShowPath(IReadOnlyList<(int X, int Y)>? steps)
        {
            _shownPath = steps ?? Array.Empty<(int X, int Y)>();
            int count = _shownPath.Count;
            while (_pathMarkers.Count < count)
            {
                GameObject marker = CreateBlock(
                    "Path step " + _pathMarkers.Count,
                    PathMarkerFill,
                    MarkerThickness,
                    OverlayLift + MarkerThickness * 0.5f,
                    Vector3.zero);
                marker.GetComponent<Renderer>().sharedMaterial = _materials[6]!;
                _pathMarkers.Add(marker);
            }

            for (int i = 0; i < _pathMarkers.Count; i++)
            {
                GameObject marker = _pathMarkers[i];
                if (i >= count)
                {
                    marker.SetActive(false);
                    continue;
                }

                (int x, int y) = _shownPath[i];
                var coord = new GridCoordinate(x, y);
                if (!_grid.Contains(coord))
                {
                    marker.SetActive(false);
                    continue;
                }

                Vector3 center = _grid.ToWorldCenter(coord);
                marker.transform.localPosition = new Vector3(center.x, marker.transform.localPosition.y, center.z);
                marker.SetActive(true);
            }
        }

        /// <summary>The tiles the road preview shows now; empty when it shows none (CR-0008-T3).</summary>
        public IReadOnlyList<(int X, int Y)> ShownRoadPreview => _shownRoadPreview;

        /// <summary>The pooled roads of the road preview, shown or hidden; for tests (CR-0008-T3).</summary>
        public IReadOnlyList<GameObject> RoadPreviewObjects => _roadGhosts;

        /// <summary>
        /// Shows the roads the player is about to build (CR-0008-T3): on each of <paramref name="tiles"/> inside the grid, the level 1 road model
        /// in a see-through copy of its material (<see cref="RoadPreviewAlpha"/>), floating <see cref="OverlayLift"/> above the ground so no grass
        /// hides it, with its arms toward the other previewed tiles and the roads already built. Null or empty hides them. Without a road model
        /// (tests of the cube fallback) a tile gets a path marker instead. The roads are pooled: after the first calls, it creates nothing.
        /// </summary>
        /// <param name="tiles">The tiles to preview, or null to clear.</param>
        public void ShowRoadPreview(IReadOnlyList<(int X, int Y)>? tiles)
        {
            _shownRoadPreview.Clear();
            if (tiles != null)
            {
                for (int i = 0; i < tiles.Count; i++)
                {
                    if (_grid.Contains(new GridCoordinate(tiles[i].X, tiles[i].Y)))
                    {
                        _shownRoadPreview.Add(tiles[i]);
                    }
                }
            }

            int count = _shownRoadPreview.Count;
            GameObject? model = _roadGhosts.Count < count ? Resources.Load<GameObject>(RoadArms.PathOf(1)) : null;
            while (_roadGhosts.Count < count)
            {
                _roadGhosts.Add(CreateRoadGhost(model));
            }

            for (int i = 0; i < _roadGhosts.Count; i++)
            {
                GameObject ghost = _roadGhosts[i];
                if (i >= count)
                {
                    ghost.SetActive(false);
                    continue;
                }

                (int x, int y) = _shownRoadPreview[i];
                Vector3 center = _grid.ToWorldCenter(new GridCoordinate(x, y));
                ghost.transform.localPosition = new Vector3(center.x, ghost.transform.localPosition.y, center.z);
                RoadArms.Show(ghost.transform, JoinsPreview(x + 1, y), JoinsPreview(x - 1, y), JoinsPreview(x, y + 1), JoinsPreview(x, y - 1));
                ghost.SetActive(true);
            }
        }

        /// <summary>Whether a previewed road at a neighbouring tile joins (<paramref name="x"/>, <paramref name="y"/>): it is previewed too, or a built road.</summary>
        private bool JoinsPreview(int x, int y)
        {
            if (HasRoad(x, y))
            {
                return true;
            }

            for (int i = 0; i < _shownRoadPreview.Count; i++)
            {
                if (_shownRoadPreview[i].X == x && _shownRoadPreview[i].Y == y)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Makes one hidden road of the road preview from <paramref name="model"/>, or a path marker when there is no model.</summary>
        private GameObject CreateRoadGhost(GameObject? model)
        {
            string name = "Road preview " + _roadGhosts.Count;
            if (model == null)
            {
                GameObject marker = CreateBlock(name, PathMarkerFill, MarkerThickness, OverlayLift + MarkerThickness * 0.5f, Vector3.zero);
                marker.GetComponent<Renderer>().sharedMaterial = _materials[6]!;
                marker.SetActive(false);
                return marker;
            }

            GameObject ghost = Object.Instantiate(model, Root.transform, false);
            ghost.name = name;
            ghost.transform.localPosition = new Vector3(0f, OverlayLift, 0f);
            Renderer[] renderers = ghost.GetComponentsInChildren<Renderer>(true);
            if (_roadGhostMaterials == null && renderers.Length > 0)
            {
                _roadGhostMaterials = PlaceholderArt.CreateGhostMaterials(renderers[0].sharedMaterial, RoadPreviewAlpha);
            }

            if (_roadGhostMaterials != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].sharedMaterials = _roadGhostMaterials;
                }
            }

            ghost.SetActive(false);
            return ghost;
        }

        /// <summary>The grass variants of visual v2 by <see cref="GroundVariants.For"/>, each loaded from <c>Resources</c>; a missing one is null.</summary>
        /// <returns>One entry per variant.</returns>
        private static GameObject?[] LoadVariants()
        {
            var variants = new GameObject?[GroundVariants.Count];
            for (int i = 0; i < variants.Length; i++)
            {
                variants[i] = Resources.Load<GameObject>(GroundVariants.PathOf(i));
            }

            return variants;
        }

        /// <summary>Stops listening to the selection and destroys every object and material this created. Safe to call twice.</summary>
        public void Destroy()
        {
            if (_destroyed)
            {
                return;
            }

            _destroyed = true;
            _selection.Changed -= ShowSelection;
            PlaceholderArt.Destroy(Root);
            foreach (Material? material in _materials)
            {
                PlaceholderArt.Destroy(material);
            }

            if (_roadGhostMaterials != null)
            {
                foreach (Material material in _roadGhostMaterials)
                {
                    PlaceholderArt.Destroy(material);
                }

                _roadGhostMaterials = null;
            }
        }

        /// <summary>Puts each marker on its tile, or hides it when its tile is none.</summary>
        private void ShowSelection()
        {
            Place(HoverMarker, _selection.Hovered, _selection.Selected == _selection.Hovered);
            Place(SelectionMarker, _selection.Selected, false);
        }

        /// <summary>Moves <paramref name="marker"/> to <paramref name="tile"/> and shows it; hides it when the tile is null or <paramref name="hide"/> is true.</summary>
        private void Place(GameObject marker, GridCoordinate? tile, bool hide)
        {
            if (!tile.HasValue || hide)
            {
                marker.SetActive(false);
                return;
            }

            Vector3 center = _grid.ToWorldCenter(tile.Value);
            marker.transform.localPosition = new Vector3(center.x, marker.transform.localPosition.y, center.z);
            marker.SetActive(true);
        }

        /// <summary>Creates a marker block with its own colour, hidden until a tile is given. <paramref name="lift"/> raises it above the other marker.</summary>
        private GameObject CreateMarker(string name, Material material, float lift)
        {
            GameObject marker = CreateBlock(name, MarkerFill, MarkerThickness, MarkerThickness * 0.5f + lift, Vector3.zero);
            marker.GetComponent<Renderer>().sharedMaterial = material;
            marker.SetActive(false);
            return marker;
        }

        /// <summary>
        /// Creates a flat cube under the ground object: <paramref name="fill"/> of a tile wide, <paramref name="thickness"/> high, its middle at
        /// height <paramref name="centerY"/> above the plane and at the x and z of <paramref name="position"/>. Its collider is removed.
        /// </summary>
        private GameObject CreateBlock(string name, float fill, float thickness, float centerY, Vector3 position)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            PlaceholderArt.Destroy(block.GetComponent<Collider>());
            block.transform.SetParent(Root.transform, false);
            block.transform.localScale = new Vector3(fill * CityGrid.TileSize, thickness, fill * CityGrid.TileSize);
            block.transform.localPosition = new Vector3(position.x, centerY, position.z);
            return block;
        }
    }
}
