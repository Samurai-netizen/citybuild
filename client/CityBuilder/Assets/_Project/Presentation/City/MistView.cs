#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The mist over land research has not opened yet (M1-03, AR-M1-03). Each closed tile holds a <c>Mist (x, y)</c> object with two puffs;
    /// a closed tile beside open land is the edge: its puffs are the flatter edge puff, and it carries a survey stake. When an area opens,
    /// its tiles' mist lifts (the puffs rise and shrink to nothing over <see cref="RevealSeconds"/>; the palette material has no alpha to fade)
    /// and is then destroyed. Without the art the puffs are white boxes and the stake a red post. Display only: which land is open comes from
    /// the engine (<see cref="CityQueries.IsLandOpen"/>).
    /// </summary>
    public sealed class MistView
    {
        /// <summary>How long, in real seconds, an opened area's mist takes to lift.</summary>
        public const float RevealSeconds = 1.5f;

        /// <summary>The name of an edge tile's survey stake, a child of its mist object.</summary>
        public const string StakeName = "Survey stake";

        /// <summary>How high, in world units, a lifting puff rises by the end of the reveal.</summary>
        private const float RevealRise = 0.6f;

        /// <summary>The colour of a placeholder puff while the mist model is missing.</summary>
        private static readonly Color PuffTint = new Color(0.92f, 0.93f, 0.95f);

        /// <summary>The colour of a placeholder survey stake while its model is missing.</summary>
        private static readonly Color StakeTint = new Color(0.75f, 0.15f, 0.12f);

        /// <summary>The grid the mist covers.</summary>
        private readonly CityGrid _grid;

        /// <summary>The mist object of each tile, row by row; null where there is none (open, or lifted).</summary>
        private readonly GameObject?[] _tiles;

        /// <summary>Whether each tile's mist is the edge kind, row by row.</summary>
        private readonly bool[] _edges;

        /// <summary>The tiles whose mist is lifting, with the seconds lifted so far.</summary>
        private readonly List<(int Index, float Seconds)> _revealing = new List<(int Index, float Seconds)>();

        /// <summary>The placeholder materials, made when first needed and destroyed with the view: the puff, the stake.</summary>
        private readonly Material?[] _materials = new Material?[2];

        /// <summary>Which tiles were open at the last <see cref="Show"/>, row by row; null before the first.</summary>
        private bool[]? _open;

        /// <summary>The open tiles of the next <see cref="Show"/>, swapped with <see cref="_open"/> when the land changed, so a call allocates nothing.</summary>
        private bool[] _scratchOpen;

        /// <summary>Creates an empty mist under a new object called <c>Mist</c>, a child of <paramref name="parent"/>.</summary>
        /// <param name="grid">The grid the mist covers.</param>
        /// <param name="parent">The transform the mist hangs under.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public MistView(CityGrid grid, Transform parent)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            Root = new GameObject("Mist");
            Root.transform.SetParent(parent, false);
            _tiles = new GameObject?[grid.Width * grid.Height];
            _edges = new bool[grid.Width * grid.Height];
            _scratchOpen = new bool[grid.Width * grid.Height];
        }

        /// <summary>The object that holds every tile's mist.</summary>
        public GameObject Root { get; }

        /// <summary>How many tiles have mist now (closed land), not counting mist that is lifting.</summary>
        public int MistTileCount { get; private set; }

        /// <summary>How many tiles' mist is lifting now.</summary>
        public int RevealingTileCount => _revealing.Count;

        /// <summary>Whether the tile has mist now (closed land); false for open land, lifting mist and tiles outside the grid.</summary>
        /// <param name="tile">The tile.</param>
        /// <returns>Whether it has mist.</returns>
        public bool HasMistAt(GridCoordinate tile)
        {
            return _grid.Contains(tile) && _open != null && !_open[Index(tile.X, tile.Y)];
        }

        /// <summary>Whether the tile has edge mist: it is closed and beside open land.</summary>
        /// <param name="tile">The tile.</param>
        /// <returns>Whether it is the edge.</returns>
        public bool IsEdgeAt(GridCoordinate tile)
        {
            return HasMistAt(tile) && _edges[Index(tile.X, tile.Y)];
        }

        /// <summary>
        /// Makes the mist match the open land of <paramref name="city"/>: the first time, mist on every closed tile; later, the mist of tiles
        /// that opened starts to lift, and tiles whose edge changed are rebuilt. Land that did not change touches nothing and allocates nothing.
        /// </summary>
        /// <param name="city">The displayed city.</param>
        /// <param name="content">The content with the map; without one every tile is open and there is no mist.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public void Show(CityState city, GameContent content)
        {
            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            bool first = _open == null;
            bool[] open = _scratchOpen;
            bool changed = first;
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    int index = Index(x, y);
                    open[index] = content.Map == null || CityQueries.IsLandOpen(city, content, x, y);
                    changed |= !first && open[index] != _open![index];
                }
            }

            if (!changed)
            {
                return;
            }

            bool[]? before = _open;
            _open = open;
            _scratchOpen = before ?? new bool[_tiles.Length];
            MistTileCount = 0;
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    int index = Index(x, y);
                    if (open[index])
                    {
                        if (before != null && !before[index] && _tiles[index] != null)
                        {
                            _revealing.Add((index, 0f));
                        }

                        continue;
                    }

                    MistTileCount++;
                    bool edge = IsOpen(x + 1, y) || IsOpen(x - 1, y) || IsOpen(x, y + 1) || IsOpen(x, y - 1);
                    if (_tiles[index] == null || _edges[index] != edge)
                    {
                        BuildTile(x, y, edge);
                    }
                }
            }
        }

        /// <summary>Moves the lifting mist on by <paramref name="deltaSeconds"/>; mist that has lifted for <see cref="RevealSeconds"/> is destroyed. Allocates nothing.</summary>
        /// <param name="deltaSeconds">The real seconds since the last tick; a value that is not positive does nothing.</param>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds > 0f))
            {
                return;
            }

            for (int i = _revealing.Count - 1; i >= 0; i--)
            {
                (int index, float seconds) = _revealing[i];
                seconds += deltaSeconds;
                GameObject? tile = _tiles[index];
                if (tile == null || seconds >= RevealSeconds)
                {
                    PlaceholderArt.Destroy(tile);
                    _tiles[index] = null;
                    _revealing.RemoveAt(i);
                    continue;
                }

                _revealing[i] = (index, seconds);
                float left = 1f - (seconds / RevealSeconds);
                tile.transform.localScale = new Vector3(left, left, left);
                Vector3 position = tile.transform.localPosition;
                tile.transform.localPosition = new Vector3(position.x, RevealRise * (1f - left), position.z);
            }
        }

        /// <summary>Destroys every object and material the view made.</summary>
        public void Destroy()
        {
            PlaceholderArt.Destroy(Root);
            foreach (Material? material in _materials)
            {
                PlaceholderArt.Destroy(material);
            }

            _revealing.Clear();
        }

        /// <summary>The row-by-row index of a tile.</summary>
        private int Index(int x, int y) => (y * _grid.Width) + x;

        /// <summary>Whether the tile is inside the grid and open at the last <see cref="Show"/>.</summary>
        private bool IsOpen(int x, int y)
        {
            return x >= 0 && y >= 0 && x < _grid.Width && y < _grid.Height && _open![Index(x, y)];
        }

        /// <summary>
        /// Builds (or rebuilds) the mist of one closed tile: two puffs at fixed spots by the tile's hash, the edge puff and a survey stake on an
        /// edge tile.
        /// </summary>
        private void BuildTile(int x, int y, bool edge)
        {
            int index = Index(x, y);
            PlaceholderArt.Destroy(_tiles[index]);
            var tile = new GameObject("Mist " + new GridCoordinate(x, y));
            tile.transform.SetParent(Root.transform, false);
            tile.transform.localPosition = _grid.ToWorldCenter(new GridCoordinate(x, y));
            uint hash = GroundVariants.Hash(x, y);
            string puffPath = edge ? GroundLooks.MistEdgePuffPath : GroundLooks.MistPuffPath;
            AddPart(tile.transform, "Puff a", puffPath, new Vector3(-0.2f, 0f, -0.15f + (hash & 3u) * 0.1f), 90f * ((hash >> 2) & 3u), PuffSlot);
            AddPart(tile.transform, "Puff b", puffPath, new Vector3(0.2f, 0f, 0.15f - ((hash >> 4) & 3u) * 0.1f), 90f * ((hash >> 6) & 3u), PuffSlot);
            if (edge)
            {
                AddPart(tile.transform, StakeName, GroundLooks.SurveyStakePath, new Vector3(0.35f, 0f, 0.35f), 0f, StakeSlot);
            }

            _tiles[index] = tile;
            _edges[index] = edge;
        }

        /// <summary>The slot of <see cref="_materials"/> that holds the placeholder puff's colour.</summary>
        private const int PuffSlot = 0;

        /// <summary>The slot of <see cref="_materials"/> that holds the placeholder stake's colour.</summary>
        private const int StakeSlot = 1;

        /// <summary>Adds one model (or its placeholder box) under a mist tile at a spot and a turn about the vertical axis.</summary>
        private void AddPart(Transform tile, string name, string path, Vector3 spot, float turn, int placeholderSlot)
        {
            GameObject? model = Resources.Load<GameObject>(path);
            GameObject part;
            if (model != null)
            {
                part = Object.Instantiate(model, tile, false);
                part.transform.localRotation = Quaternion.Euler(0f, turn, 0f) * part.transform.localRotation;
            }
            else
            {
                part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                PlaceholderArt.Destroy(part.GetComponent<Collider>());
                part.transform.SetParent(tile, false);
                part.transform.localScale = placeholderSlot == StakeSlot ? new Vector3(0.05f, 0.25f, 0.05f) : new Vector3(0.5f, 0.2f, 0.5f);
                part.GetComponent<Renderer>().sharedMaterial = _materials[placeholderSlot] ??=
                    PlaceholderArt.CreateMaterial(placeholderSlot == StakeSlot ? StakeTint : PuffTint);
            }

            part.name = name;
            part.transform.localPosition = spot;
        }
    }
}
