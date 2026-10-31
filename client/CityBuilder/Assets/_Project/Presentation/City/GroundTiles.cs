#nullable enable

using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The ground of the city: one flat tile per grid cell, built from cube primitives at runtime (M0 uses placeholder art, <c>09</c>),
    /// in two greens like a chessboard, plus a hover marker and a selection marker that follow a <see cref="TileSelection"/>. The tiles have
    /// no colliders: the pointer's tile is found with maths (<see cref="CityGrid"/>), not physics. <see cref="Destroy"/> removes everything it made.
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

        /// <summary>The grid the tiles follow.</summary>
        private readonly CityGrid _grid;

        /// <summary>The state the markers show.</summary>
        private readonly TileSelection _selection;

        /// <summary>The materials this object created, destroyed with it.</summary>
        private readonly Material[] _materials = new Material[4];

        /// <summary>Whether <see cref="Destroy"/> has run.</summary>
        private bool _destroyed;

        /// <summary>Builds the tiles and the markers under a new object called <c>Ground</c> that is a child of <paramref name="parent"/>.</summary>
        /// <param name="grid">The grid to cover.</param>
        /// <param name="selection">The state the markers show; this object listens to it until <see cref="Destroy"/>.</param>
        /// <param name="parent">The transform the ground hangs under.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public GroundTiles(CityGrid grid, TileSelection selection, Transform parent)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            Root = new GameObject("Ground");
            Root.transform.SetParent(parent, false);

            Material lightMaterial = _materials[0] = CreateMaterial(LightGround);
            Material darkMaterial = _materials[1] = CreateMaterial(DarkGround);
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var tile = new GridCoordinate(x, y);
                    GameObject block = CreateBlock("Tile " + tile, TileFill, TileThickness, -TileThickness * 0.5f, grid.ToWorldCenter(tile));
                    block.GetComponent<Renderer>().sharedMaterial = (x + y) % 2 == 0 ? lightMaterial : darkMaterial;
                }
            }

            HoverMarker = CreateMarker("Hover marker", _materials[2] = CreateMaterial(HoverColor), 0f);
            SelectionMarker = CreateMarker("Selection marker", _materials[3] = CreateMaterial(SelectionColor), MarkerThickness);

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

        /// <summary>Stops listening to the selection and destroys every object and material this created. Safe to call twice.</summary>
        public void Destroy()
        {
            if (_destroyed)
            {
                return;
            }

            _destroyed = true;
            _selection.Changed -= ShowSelection;
            DestroyObject(Root);
            foreach (Material material in _materials)
            {
                DestroyObject(material);
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
            DestroyObject(block.GetComponent<Collider>());
            block.transform.SetParent(Root.transform, false);
            block.transform.localScale = new Vector3(fill * CityGrid.TileSize, thickness, fill * CityGrid.TileSize);
            block.transform.localPosition = new Vector3(position.x, centerY, position.z);
            return block;
        }

        /// <summary>
        /// Creates a material of one colour from the render pipeline's default look: a copy of the material a new primitive gets, so no
        /// shader is looked up by name (a lookup could be stripped from a device build).
        /// </summary>
        private static Material CreateMaterial(Color color)
        {
            GameObject sample = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var material = new Material(sample.GetComponent<Renderer>().sharedMaterial) { color = color };
            DestroyObject(sample);
            return material;
        }

        /// <summary>Destroys an object the way the current mode needs: a delayed destroy in play mode, an immediate one in the Editor's edit mode (tests).</summary>
        private static void DestroyObject(Object? target)
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
