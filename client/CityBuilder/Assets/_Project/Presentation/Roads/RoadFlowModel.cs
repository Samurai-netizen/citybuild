#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Presentation.Roads
{
    /// <summary>
    /// The state of road mode (CR-0008): entered from the build menu (<see cref="StartBuildMode"/>) or by tapping an existing road tile on the map
    /// (<see cref="InspectRoadAt"/>). In build mode the player taps or drags a line of tiles (drawn across <see cref="GroundTiles.ShowPath"/>) and
    /// sees the total coin cost and any refused tile's reason before confirming <c>place-roads</c>. When existing road tiles are selected, the bar
    /// offers <c>upgrade-roads</c> (with the next level's coin cost and refusal reason) and <c>remove-roads</c> (free, no refund). Pure C# with no
    /// Unity objects so EditMode tests check every transition and refusal reason.
    /// </summary>
    public sealed class RoadFlowModel
    {
        /// <summary>The server's maximum road tiles per command (<c>05-api-contract.md</c>).</summary>
        public const int MaxTilesPerCommand = 64;

        /// <summary>The reason shown when a picked tile already holds a building or a road in build mode.</summary>
        public const string TileOccupiedReason = "Tile occupied";

        /// <summary>The reason shown when a picked tile is outside the grid.</summary>
        public const string OutOfBoundsReason = "Outside the city";

        /// <summary>The reason shown when a picked tile has no road to upgrade or remove.</summary>
        public const string NoRoadReason = "No road on tile";

        /// <summary>The reason shown when a picked road tile is already at the highest road level.</summary>
        public const string MaxLevelReason = "Already at highest level";

        /// <summary>The game content.</summary>
        private readonly GameContent _content;

        /// <summary>The city grid.</summary>
        private readonly CityGrid _grid;

        /// <summary>The picked tiles in order.</summary>
        private readonly List<GridCoordinate> _tiles = new List<GridCoordinate>();

        /// <summary>The picked tiles as <c>(X, Y)</c> pairs for <see cref="GroundTiles.ShowPath"/>.</summary>
        private readonly List<(int X, int Y)> _previewSteps = new List<(int X, int Y)>();

        /// <summary>The tile hints while road mode is open (free vs occupied).</summary>
        private readonly TileHint[] _hints;

        /// <summary>The displayed city.</summary>
        private CityState? _city;

        /// <summary>The first endpoint of a line drag in build mode; null before the first tile is tapped.</summary>
        private GridCoordinate? _anchorTile;

        /// <summary>Whether road mode was opened from the build menu to place new roads (true) or by tapping an existing road tile (false).</summary>
        private bool _buildMode;

        /// <summary>Whether commands are allowed by the connection.</summary>
        private CommandAvailability _availability = CommandAvailability.Allowed;

        /// <summary>Creates a road flow model over <paramref name="content"/> and <paramref name="grid"/>.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public RoadFlowModel(GameContent content, CityGrid grid)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _hints = new TileHint[grid.Width * grid.Height];
        }

        /// <summary>Raised whenever the phase, selected tiles, hints, or words change.</summary>
        public event Action? Changed;

        /// <summary>Where the player is in road mode.</summary>
        public RoadPhase Phase { get; private set; } = RoadPhase.Idle;

        /// <summary>Whether road mode is open for placing new roads (as opposed to inspecting an existing road tile).</summary>
        public bool IsBuildMode => _buildMode;

        /// <summary>Whether the ground should show free/occupied hints.</summary>
        public bool ShowsHints => Phase != RoadPhase.Idle && _buildMode;

        /// <summary>The free/occupied tile hints for the ground while in road build mode.</summary>
        public TileHint[] Hints => _hints;

        /// <summary>The picked tiles in order.</summary>
        public IReadOnlyList<GridCoordinate> Tiles => _tiles;

        /// <summary>The picked tiles as <c>(X, Y)</c> steps for <see cref="GroundTiles.ShowPath"/>.</summary>
        public IReadOnlyList<(int X, int Y)> PreviewSteps => _previewSteps;

        /// <summary>The total coin cost of placing or upgrading the picked tiles.</summary>
        public long TotalCoins { get; private set; }

        /// <summary>The main prompt text of the road bar.</summary>
        public string PromptText { get; private set; } = string.Empty;

        /// <summary>The detail line of the road bar (cost, speed or tile count).</summary>
        public string DetailText { get; private set; } = string.Empty;

        /// <summary>The refusal reason for the primary action (Place or Upgrade), or empty when allowed.</summary>
        public string HintText { get; private set; } = string.Empty;

        /// <summary>Whether the Place button is shown.</summary>
        public bool ShowsPlace => Phase != RoadPhase.Idle && _buildMode;

        /// <summary>Whether the Upgrade and Remove buttons are shown.</summary>
        public bool ShowsEditExisting => Phase != RoadPhase.Idle && !_buildMode;

        /// <summary>Whether <see cref="TryConfirmPlace"/> can succeed right now.</summary>
        public bool CanPlace { get; private set; }

        /// <summary>Whether <see cref="TryConfirmUpgrade"/> can succeed right now.</summary>
        public bool CanUpgrade { get; private set; }

        /// <summary>Whether <see cref="TryConfirmRemove"/> can succeed right now.</summary>
        public bool CanRemove { get; private set; }

        /// <summary>Updates the displayed city and refreshes hints, costs and availability.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> is null.</exception>
        public void Update(CityState city)
        {
            _city = city ?? throw new ArgumentNullException(nameof(city));
            RefreshHints();
            if (Phase != RoadPhase.Idle)
            {
                if (!_buildMode && Phase != RoadPhase.Sending && !AllTilesHaveRoads())
                {
                    Cancel();
                    return;
                }

                Rebuild();
                Changed?.Invoke();
            }
        }

        /// <summary>Updates whether the connection allows commands (<see cref="CommandAvailability.For"/>).</summary>
        public void SetCommandAvailability(CommandAvailability availability)
        {
            if (_availability.Equals(availability))
            {
                return;
            }

            _availability = availability;
            if (Phase != RoadPhase.Idle)
            {
                Rebuild();
                Changed?.Invoke();
            }
        }

        /// <summary>Opens road mode from the build menu to place level-1 roads.</summary>
        public void StartBuildMode()
        {
            if (Phase == RoadPhase.Sending)
            {
                return;
            }

            _buildMode = true;
            _anchorTile = null;
            ClearTiles();
            Phase = RoadPhase.PickingTiles;
            RefreshHints();
            Rebuild();
            Changed?.Invoke();
        }

        /// <summary>
        /// Opens the road bar for an existing road at <paramref name="tile"/> so the player can upgrade or remove it.
        /// Returns false when there is no road on <paramref name="tile"/>.
        /// </summary>
        public bool InspectRoadAt(GridCoordinate tile)
        {
            if (Phase == RoadPhase.Sending || _city == null || !_city.TryGetRoad(tile.X, tile.Y, out _))
            {
                return false;
            }

            _buildMode = false;
            _anchorTile = tile;
            ClearTiles();
            AddTile(tile);
            Phase = RoadPhase.Confirming;
            Rebuild();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Sets the selected tiles to the orthogonal line from <paramref name="start"/> to <paramref name="end"/> (up to <see cref="MaxTilesPerCommand"/> tiles).
        /// When <paramref name="end"/> is not in the same row or column as <paramref name="start"/>, horizontal steps come first, then vertical.
        /// </summary>
        public void SelectLine(GridCoordinate start, GridCoordinate end)
        {
            if (Phase == RoadPhase.Idle || Phase == RoadPhase.Sending)
            {
                return;
            }

            _anchorTile = start;
            ClearTiles();
            int x = start.X;
            int y = start.Y;
            AddTile(new GridCoordinate(x, y));
            while (x != end.X && _tiles.Count < MaxTilesPerCommand)
            {
                x += end.X > x ? 1 : -1;
                AddTile(new GridCoordinate(x, y));
            }

            while (y != end.Y && _tiles.Count < MaxTilesPerCommand)
            {
                y += end.Y > y ? 1 : -1;
                AddTile(new GridCoordinate(x, y));
            }

            Phase = _tiles.Count > 0 ? RoadPhase.Confirming : RoadPhase.PickingTiles;
            Rebuild();
            Changed?.Invoke();
        }

        /// <summary>
        /// Handles a tile tap or hover-drag while in road mode: in build mode, the first tap picks the start of a line and a second tap extends
        /// a line from that start tile to <paramref name="tile"/> (or tapping the single selected tile again clears the selection).
        /// </summary>
        public void TapTile(GridCoordinate? tile)
        {
            if (Phase == RoadPhase.Idle || Phase == RoadPhase.Sending)
            {
                return;
            }

            if (!tile.HasValue)
            {
                if (!_buildMode)
                {
                    Cancel();
                    return;
                }

                _anchorTile = null;
                ClearTiles();
                Phase = RoadPhase.PickingTiles;
                Rebuild();
                Changed?.Invoke();
                return;
            }

            if (!_buildMode)
            {
                if (_city != null && _city.TryGetRoad(tile.Value.X, tile.Value.Y, out _))
                {
                    if (_anchorTile.HasValue && _anchorTile.Value != tile.Value)
                    {
                        SelectExistingRoadLine(_anchorTile.Value, tile.Value);
                    }
                    else
                    {
                        InspectRoadAt(tile.Value);
                    }
                }
                else
                {
                    Cancel();
                }

                return;
            }

            if (!_anchorTile.HasValue)
            {
                SelectLine(tile.Value, tile.Value);
                return;
            }

            if (_tiles.Count == 1 && _tiles[0] == tile.Value)
            {
                _anchorTile = null;
                ClearTiles();
                Phase = RoadPhase.PickingTiles;
                Rebuild();
                Changed?.Invoke();
                return;
            }

            SelectLine(_anchorTile.Value, tile.Value);
        }

        /// <summary>Extends the line from the anchor tile to <paramref name="hovered"/> while dragging in build mode.</summary>
        public void DragTo(GridCoordinate hovered)
        {
            if (Phase == RoadPhase.Idle || Phase == RoadPhase.Sending || !_buildMode || !_anchorTile.HasValue)
            {
                return;
            }

            SelectLine(_anchorTile.Value, hovered);
        }

        /// <summary>Confirms placing level-1 roads on the picked tiles and returns the <c>place-roads</c> intent.</summary>
        public bool TryConfirmPlace(out CommandIntent? intent)
        {
            if (!CanPlace || _tiles.Count == 0)
            {
                intent = null;
                return false;
            }

            Phase = RoadPhase.Sending;
            intent = CommandIntent.PlaceRoads(ToDtoTiles());
            Rebuild();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Confirms upgrading the picked road tiles and returns the <c>upgrade-roads</c> intent.</summary>
        public bool TryConfirmUpgrade(out CommandIntent? intent)
        {
            if (!CanUpgrade || _tiles.Count == 0)
            {
                intent = null;
                return false;
            }

            Phase = RoadPhase.Sending;
            intent = CommandIntent.UpgradeRoads(ToDtoTiles());
            Rebuild();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Confirms removing the picked road tiles and returns the <c>remove-roads</c> intent.</summary>
        public bool TryConfirmRemove(out CommandIntent? intent)
        {
            if (!CanRemove || _tiles.Count == 0)
            {
                intent = null;
                return false;
            }

            Phase = RoadPhase.Sending;
            intent = CommandIntent.RemoveRoads(ToDtoTiles());
            Rebuild();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Called when the sent command finishes: closes road mode on success, or returns to confirming on failure.</summary>
        public void FinishCommand(bool succeeded)
        {
            if (Phase != RoadPhase.Sending)
            {
                return;
            }

            if (succeeded)
            {
                Cancel();
                return;
            }

            Phase = _tiles.Count > 0 ? RoadPhase.Confirming : RoadPhase.PickingTiles;
            Rebuild();
            Changed?.Invoke();
        }

        /// <summary>Closes road mode and clears any selected tiles.</summary>
        public void Cancel()
        {
            if (Phase == RoadPhase.Idle || Phase == RoadPhase.Sending)
            {
                if (Phase == RoadPhase.Idle)
                {
                    return;
                }
            }

            Phase = RoadPhase.Idle;
            _buildMode = false;
            _anchorTile = null;
            ClearTiles();
            TotalCoins = 0;
            PromptText = string.Empty;
            DetailText = string.Empty;
            HintText = string.Empty;
            CanPlace = false;
            CanUpgrade = false;
            CanRemove = false;
            Changed?.Invoke();
        }

        /// <summary>Selects a line of existing road tiles from <paramref name="start"/> to <paramref name="end"/>.</summary>
        private void SelectExistingRoadLine(GridCoordinate start, GridCoordinate end)
        {
            ClearTiles();
            int x = start.X;
            int y = start.Y;
            if (_city != null && _city.TryGetRoad(x, y, out _))
            {
                AddTile(new GridCoordinate(x, y));
            }

            while (x != end.X && _tiles.Count < MaxTilesPerCommand)
            {
                x += end.X > x ? 1 : -1;
                if (_city != null && _city.TryGetRoad(x, y, out _))
                {
                    AddTile(new GridCoordinate(x, y));
                }
            }

            while (y != end.Y && _tiles.Count < MaxTilesPerCommand)
            {
                y += end.Y > y ? 1 : -1;
                if (_city != null && _city.TryGetRoad(x, y, out _))
                {
                    AddTile(new GridCoordinate(x, y));
                }
            }

            Phase = RoadPhase.Confirming;
            Rebuild();
            Changed?.Invoke();
        }

        /// <summary>Whether every selected tile currently has a road in <see cref="_city"/>.</summary>
        private bool AllTilesHaveRoads()
        {
            if (_city == null || _tiles.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < _tiles.Count; i++)
            {
                if (!_city.TryGetRoad(_tiles[i].X, _tiles[i].Y, out _))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Rebuilds the free/occupied tile hints for <see cref="_city"/>.</summary>
        private void RefreshHints()
        {
            Array.Clear(_hints, 0, _hints.Length);
            if (_city == null)
            {
                return;
            }

            for (int i = 0; i < _city.Buildings.Count; i++)
            {
                Building b = _city.Buildings[i];
                var c = new GridCoordinate(b.X, b.Y);
                if (_grid.Contains(c))
                {
                    _hints[b.Y * _grid.Width + b.X] = TileHint.Occupied;
                }
            }

            for (int i = 0; i < _city.Roads.Count; i++)
            {
                RoadTile r = _city.Roads[i];
                var c = new GridCoordinate(r.X, r.Y);
                if (_grid.Contains(c))
                {
                    _hints[r.Y * _grid.Width + r.X] = TileHint.Occupied;
                }
            }
        }

        /// <summary>Recomputes the prompt, detail, refusal reason and button states.</summary>
        private void Rebuild()
        {
            CanPlace = false;
            CanUpgrade = false;
            CanRemove = false;
            HintText = string.Empty;
            TotalCoins = 0;

            if (_city == null || Phase == RoadPhase.Idle)
            {
                PromptText = string.Empty;
                DetailText = string.Empty;
                return;
            }

            if (_buildMode)
            {
                RebuildPlaceMode();
            }
            else
            {
                RebuildExistingMode();
            }
        }

        /// <summary>Recomputes texts and button state for placing level-1 roads.</summary>
        private void RebuildPlaceMode()
        {
            RoadLevelDefinition level1 = _content.GetRoadLevel(1);
            long perTileCoins = CoinsOf(level1.Cost);
            if (_tiles.Count == 0)
            {
                PromptText = level1.Name + ": tap or drag tiles to build roads";
                DetailText = "Cost: " + perTileCoins.ToString(CultureInfo.InvariantCulture) + " Coins / tile · " + FormatSpeedMillis(level1.TileMillis);
                return;
            }

            TotalCoins = checked(perTileCoins * _tiles.Count);
            string tileWord = _tiles.Count == 1 ? "1 tile" : _tiles.Count.ToString(CultureInfo.InvariantCulture) + " tiles";
            if (Phase == RoadPhase.Sending)
            {
                PromptText = "Building " + level1.Name + " (" + tileWord + ")…";
                DetailText = "Cost: " + TotalCoins.ToString(CultureInfo.InvariantCulture) + " Coins";
                return;
            }

            PromptText = "Build " + level1.Name + " (" + tileWord + ")?";
            DetailText = "Cost: " + TotalCoins.ToString(CultureInfo.InvariantCulture) + " Coins · " + FormatSpeedMillis(level1.TileMillis);
            HintText = PlaceRefusalReason();
            CanPlace = HintText.Length == 0;
        }

        /// <summary>Recomputes texts and button states for upgrading or removing existing road tiles.</summary>
        private void RebuildExistingMode()
        {
            if (_tiles.Count == 0 || _city == null)
            {
                PromptText = string.Empty;
                DetailText = string.Empty;
                return;
            }

            _city.TryGetRoad(_tiles[0].X, _tiles[0].Y, out RoadTile firstRoad);
            int level = firstRoad.Level > 0 ? firstRoad.Level : 1;
            RoadLevelDefinition currentDef = _content.GetRoadLevel(level);
            string tileWord = _tiles.Count == 1
                ? "at " + _tiles[0]
                : "(" + _tiles.Count.ToString(CultureInfo.InvariantCulture) + " tiles)";

            if (Phase == RoadPhase.Sending)
            {
                PromptText = currentDef.Name + " " + tileWord + "…";
                DetailText = string.Empty;
                return;
            }

            PromptText = currentDef.Name + " · Level " + level.ToString(CultureInfo.InvariantCulture) + " " + tileWord;
            string? upgradeRefusal = UpgradeRefusalReason(out long upgradeCoins, out RoadLevelDefinition? nextDef);
            TotalCoins = upgradeCoins;
            if (nextDef != null)
            {
                DetailText = FormatSpeedMillis(currentDef.TileMillis)
                    + " → Upgrade to " + nextDef.Name + " (" + FormatSpeedMillis(nextDef.TileMillis) + "): "
                    + upgradeCoins.ToString(CultureInfo.InvariantCulture) + " Coins";
            }
            else
            {
                DetailText = FormatSpeedMillis(currentDef.TileMillis) + " · Highest road level";
            }

            HintText = upgradeRefusal ?? string.Empty;
            CanUpgrade = upgradeRefusal == null;
            CanRemove = _availability.IsAllowed && EveryTileHasRoadReason() == null;
        }

        /// <summary>The refusal reason for <c>place-roads</c> in <see cref="RoadRules.PlaceRoads"/> order, or empty when allowed.</summary>
        private string PlaceRefusalReason()
        {
            if (!_availability.IsAllowed)
            {
                return _availability.Reason;
            }

            for (int i = 0; i < _tiles.Count; i++)
            {
                GridCoordinate tile = _tiles[i];
                if (!_grid.Contains(tile))
                {
                    return OutOfBoundsReason + " at " + tile;
                }
            }

            for (int i = 0; i < _tiles.Count; i++)
            {
                GridCoordinate tile = _tiles[i];
                if (CityQueries.TileAt(_city!, tile.X, tile.Y) != TileContent.Ground)
                {
                    return TileOccupiedReason + " at " + tile;
                }
            }

            if (_city!.Purse < TotalCoins)
            {
                return "Not enough Coins";
            }

            return string.Empty;
        }

        /// <summary>The refusal reason for <c>upgrade-roads</c> in <see cref="RoadRules.UpgradeRoads"/> order, or null when allowed.</summary>
        private string? UpgradeRefusalReason(out long upgradeCoins, out RoadLevelDefinition? nextLevelDef)
        {
            upgradeCoins = 0;
            nextLevelDef = null;

            if (!_availability.IsAllowed)
            {
                return _availability.Reason;
            }

            string? missingRoad = EveryTileHasRoadReason();
            if (missingRoad != null)
            {
                return missingRoad;
            }

            for (int i = 0; i < _tiles.Count; i++)
            {
                GridCoordinate tile = _tiles[i];
                _city!.TryGetRoad(tile.X, tile.Y, out RoadTile road);
                if (road.Level >= _content.MaxRoadLevel)
                {
                    return MaxLevelReason;
                }

                RoadLevelDefinition next = _content.GetRoadLevel(road.Level + 1);
                nextLevelDef ??= next;
                upgradeCoins = checked(upgradeCoins + CoinsOf(next.Cost));
            }

            if (_city!.Purse < upgradeCoins)
            {
                return "Not enough Coins";
            }

            return null;
        }

        /// <summary>Returns a reason when any selected tile has no road, or null when every selected tile has a road.</summary>
        private string? EveryTileHasRoadReason()
        {
            for (int i = 0; i < _tiles.Count; i++)
            {
                GridCoordinate tile = _tiles[i];
                if (!_city!.TryGetRoad(tile.X, tile.Y, out _))
                {
                    return NoRoadReason + " at " + tile;
                }
            }

            return null;
        }

        /// <summary>Clears the selected tile lists.</summary>
        private void ClearTiles()
        {
            _tiles.Clear();
            _previewSteps.Clear();
        }

        /// <summary>Adds a distinct tile to the selection.</summary>
        private void AddTile(GridCoordinate tile)
        {
            if (!_tiles.Contains(tile))
            {
                _tiles.Add(tile);
                _previewSteps.Add((tile.X, tile.Y));
            }
        }

        /// <summary>Converts the selected tiles to <see cref="RoadTileRequestDto"/> entries.</summary>
        private IReadOnlyList<RoadTileRequestDto> ToDtoTiles()
        {
            var list = new RoadTileRequestDto[_tiles.Count];
            for (int i = 0; i < _tiles.Count; i++)
            {
                list[i] = new RoadTileRequestDto(_tiles[i].X, _tiles[i].Y);
            }

            return list;
        }

        /// <summary>The coin cost in <paramref name="cost"/>.</summary>
        private long CoinsOf(IReadOnlyList<ItemQuantity> cost)
        {
            for (int i = 0; i < cost.Count; i++)
            {
                if (cost[i].Item == _content.Currency)
                {
                    return cost[i].Quantity;
                }
            }

            return 0;
        }

        /// <summary>Formats a road level's milliseconds per tile as seconds per tile (for example <c>2.5s / tile</c>).</summary>
        private static string FormatSpeedMillis(long tileMillis)
        {
            long whole = tileMillis / 1000;
            long tenths = (tileMillis % 1000) / 100;
            return tenths == 0
                ? whole.ToString(CultureInfo.InvariantCulture) + "s / tile"
                : whole.ToString(CultureInfo.InvariantCulture) + "." + tenths.ToString(CultureInfo.InvariantCulture) + "s / tile";
        }
    }
}
