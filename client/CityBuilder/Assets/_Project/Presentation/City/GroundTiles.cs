#nullable enable

using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The ground of the city: one flat tile per grid cell, an instance of the art's grass tile (CR-0005); if the tile model is missing, a cube
    /// primitive in two greens like a chessboard instead. Plus a hover marker and a selection marker that follow a <see cref="TileSelection"/>.
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

        /// <summary>The grid the tiles follow.</summary>
        private readonly CityGrid _grid;

        /// <summary>The state the markers show.</summary>
        private readonly TileSelection _selection;

        /// <summary>The materials this object created, destroyed with it. The first two are the chessboard greens of the cube fallback, and stay null when tile models are used.</summary>
        private readonly Material?[] _materials = new Material?[6];

        /// <summary>The material each tile shows when it has no hint, row by row: the art's shared material, or a chessboard green.</summary>
        private readonly Material[] _groundMaterials;

        /// <summary>The tile blocks, row by row (<c>y * width + x</c>), so a hint can change a tile's material.</summary>
        private readonly Renderer[] _tileRenderers;

        /// <summary>The hint each tile shows now, row by row; null while the tile shows its ground colour.</summary>
        private readonly TileHint?[] _shownHints;

        /// <summary>Whether <see cref="Destroy"/> has run.</summary>
        private bool _destroyed;

        /// <summary>Builds the tiles and the markers under a new object called <c>Ground</c> that is a child of <paramref name="parent"/>.</summary>
        /// <param name="grid">The grid to cover.</param>
        /// <param name="selection">The state the markers show; this object listens to it until <see cref="Destroy"/>.</param>
        /// <param name="parent">The transform the ground hangs under.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public GroundTiles(CityGrid grid, TileSelection selection, Transform parent)
            : this(grid, selection, parent, Resources.Load<GameObject>(BuildingLooks.GrassTilePath))
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
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            Root = new GameObject("Ground");
            Root.transform.SetParent(parent, false);

            _tileRenderers = new Renderer[grid.Width * grid.Height];
            _groundMaterials = new Material[grid.Width * grid.Height];
            _shownHints = new TileHint?[grid.Width * grid.Height];
            if (tileModel == null)
            {
                _materials[0] = PlaceholderArt.CreateMaterial(LightGround);
                _materials[1] = PlaceholderArt.CreateMaterial(DarkGround);
            }

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var tile = new GridCoordinate(x, y);
                    int index = y * grid.Width + x;
                    if (tileModel != null)
                    {
                        GameObject instance = Object.Instantiate(tileModel, Root.transform, false);
                        instance.name = "Tile " + tile;
                        instance.transform.localPosition = grid.ToWorldCenter(tile);
                        _tileRenderers[index] = instance.GetComponentInChildren<Renderer>();
                    }
                    else
                    {
                        GameObject block = CreateBlock("Tile " + tile, TileFill, TileThickness, -TileThickness * 0.5f, grid.ToWorldCenter(tile));
                        _tileRenderers[index] = block.GetComponent<Renderer>();
                        _tileRenderers[index].sharedMaterial = (x + y) % 2 == 0 ? _materials[0]! : _materials[1]!;
                    }

                    _groundMaterials[index] = _tileRenderers[index].sharedMaterial;
                }
            }

            HoverMarker = CreateMarker("Hover marker", _materials[2] = PlaceholderArt.CreateMaterial(HoverColor), 0f);
            SelectionMarker = CreateMarker("Selection marker", _materials[3] = PlaceholderArt.CreateMaterial(SelectionColor), MarkerThickness);
            _materials[4] = PlaceholderArt.CreateMaterial(FreeHintColor);
            _materials[5] = PlaceholderArt.CreateMaterial(OccupiedHintColor);

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
