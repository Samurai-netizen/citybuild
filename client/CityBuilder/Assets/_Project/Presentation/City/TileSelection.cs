#nullable enable

using System;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// Which tile the pointer is over and which tile the player tapped. State only, for the ground's highlights and, later, the placement
    /// and inspector views: it decides nothing about the economy and never calls the server. A coordinate outside the grid counts as no tile.
    /// </summary>
    public sealed class TileSelection
    {
        /// <summary>The grid whose tiles can be hovered and selected.</summary>
        private readonly CityGrid _grid;

        /// <summary>Creates an empty selection over <paramref name="grid"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="grid"/> is null.</exception>
        public TileSelection(CityGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        /// <summary>Raised after <see cref="Hovered"/> or <see cref="Selected"/> changed; never when a call changes nothing.</summary>
        public event Action? Changed;

        /// <summary>The tile under the pointer, or null when it is over no tile (or there is no pointer, as on a touch screen between taps).</summary>
        public GridCoordinate? Hovered { get; private set; }

        /// <summary>The tile the player tapped last, or null when none is selected.</summary>
        public GridCoordinate? Selected { get; private set; }

        /// <summary>Sets the hovered tile; null, or a tile outside the grid, clears it.</summary>
        public void SetHovered(GridCoordinate? tile)
        {
            GridCoordinate? value = Normalize(tile);
            if (Hovered == value)
            {
                return;
            }

            Hovered = value;
            Changed?.Invoke();
        }

        /// <summary>
        /// The player tapped <paramref name="tile"/>. A tile selects; tapping the selected tile again, tapping outside the grid (null) or
        /// tapping a tile outside it clears the selection.
        /// </summary>
        public void Tap(GridCoordinate? tile)
        {
            GridCoordinate? value = Normalize(tile);
            GridCoordinate? next = value.HasValue && value == Selected ? null : value;
            if (Selected == next)
            {
                return;
            }

            Selected = next;
            Changed?.Invoke();
        }

        /// <summary>Clears the selection (for example when a view closes).</summary>
        public void ClearSelection()
        {
            Tap(null);
        }

        /// <summary>The tile, or null when it is null or outside the grid.</summary>
        private GridCoordinate? Normalize(GridCoordinate? tile)
        {
            return tile.HasValue && _grid.Contains(tile.Value) ? tile : null;
        }
    }
}
