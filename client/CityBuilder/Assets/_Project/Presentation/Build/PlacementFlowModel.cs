#nullable enable

using System;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Feedback;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Presentation.Build
{
    /// <summary>
    /// The placement flow without a screen (M0-46): pick a card, pick a tile, confirm, wait for the server. It holds which type and tile are
    /// picked, a hint for every tile of the grid from the latest city (free or occupied), the words of the bar, and what each ending does:
    /// a success ends the flow, a failure keeps the card, drops the tile and keeps the error to show; while a command is on its way the picks are frozen. An occupied tile
    /// (red) can be picked, so the bar can say why, but it cannot be confirmed: nothing is sent for a tile the player was told is taken. That is
    /// a courtesy, not the rule: the server checks every command again (<c>09</c> UX rules). The model subtracts nothing and never creates a
    /// building; <see cref="TryConfirm"/> only makes the <see cref="CommandIntent"/> that the caller gives the <see cref="CommandSender"/>.
    /// While the connection does not allow a command (<see cref="SetCommandAvailability"/>, M0-55) Place is off and <see cref="HintText"/> gives the
    /// reason; the picks stay, so the player can go on choosing a tile and place when the connection is back.
    /// </summary>
    public sealed class PlacementFlowModel
    {
        /// <summary>The content the building names come from.</summary>
        private readonly GameContent _content;

        /// <summary>The grid whose tiles are hinted.</summary>
        private readonly CityGrid _grid;

        /// <summary>The hints of the latest city, one per tile, row by row.</summary>
        private TileHint[] _hints;

        /// <summary>The hints of the city before; swapped with <see cref="_hints"/> on each update so a second without a change allocates nothing.</summary>
        private TileHint[] _scratch;

        /// <summary>The intent that was confirmed and has not ended; null when none is.</summary>
        private CommandIntent? _sending;

        /// <summary>Whether a command may be sent now; allowed until <see cref="SetCommandAvailability"/> says otherwise.</summary>
        private CommandAvailability _availability = CommandAvailability.Allowed;

        /// <summary>Creates a flow with nothing picked, over a grid whose tiles are all free until <see cref="Update"/> says otherwise.</summary>
        /// <param name="content">The content that names the building types.</param>
        /// <param name="grid">The grid of the city.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public PlacementFlowModel(GameContent content, CityGrid grid)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _hints = new TileHint[grid.Width * grid.Height];
            _scratch = new TileHint[grid.Width * grid.Height];
        }

        /// <summary>Raised after anything the screen shows changed: the phase, the type, the tile, the hints or the error.</summary>
        public event Action? Changed;

        /// <summary>Raised when the flow ended by itself or by <see cref="Cancel"/>: the card and the tile should be dropped by whoever holds them.</summary>
        public event Action? Ended;

        /// <summary>The type picked, or null when none is.</summary>
        public BuildingTypeId? TypeId { get; private set; }

        /// <summary>The tile picked, or null when none is.</summary>
        public GridCoordinate? Tile { get; private set; }

        /// <summary>The words of the last failure (<see cref="FeedbackMessages"/>'s sentence for its code, never the code or the server's text), or empty; cleared by the next pick, confirm or cancel.</summary>
        public string ErrorText { get; private set; } = string.Empty;

        /// <summary>Where the player is.</summary>
        public PlacementPhase Phase
        {
            get
            {
                if (_sending != null)
                {
                    return PlacementPhase.Sending;
                }

                if (!TypeId.HasValue)
                {
                    return PlacementPhase.Idle;
                }

                return Tile.HasValue ? PlacementPhase.Confirming : PlacementPhase.PickingTile;
            }
        }

        /// <summary>Whether the ground should show the hints: a card is picked, or a command is on its way.</summary>
        public bool ShowsHints => Phase != PlacementPhase.Idle;

        /// <summary>
        /// Whether <see cref="TryConfirm"/> can succeed: a type and a free tile are picked, nothing is on its way and the connection allows a
        /// command. A tile the displayed city shows occupied (red) cannot be confirmed; if a snapshot occupies the picked tile, this turns false at once.
        /// </summary>
        public bool CanConfirm => Phase == PlacementPhase.Confirming && _availability.IsAllowed && HintAt(Tile!.Value) != TileHint.Occupied;

        /// <summary>Whether <see cref="Cancel"/> does anything: a command that was sent cannot be taken back.</summary>
        public bool CanCancel => Phase != PlacementPhase.Idle && Phase != PlacementPhase.Sending;

        /// <summary>The hint of every tile, row by row (<c>y * width + x</c>). The array is reused: read it, don't keep it.</summary>
        public TileHint[] Hints => _hints;

        /// <summary>The hint of one tile of the grid; <see cref="TileHint.Free"/> for a tile outside it.</summary>
        public TileHint HintAt(GridCoordinate tile)
        {
            return _grid.Contains(tile) ? _hints[tile.Y * _grid.Width + tile.X] : TileHint.Free;
        }

        /// <summary>The line above the buttons: what to do next, or what is happening.</summary>
        public string PromptText
        {
            get
            {
                string name = NameOfType();
                switch (Phase)
                {
                    case PlacementPhase.PickingTile:
                        return name + ": tap a tile to build on";
                    case PlacementPhase.Confirming:
                        return "Build " + name + " at " + Tile + "?";
                    case PlacementPhase.Sending:
                        return "Building " + name + " at " + SendingTile() + "…";
                    default:
                        return string.Empty;
                }
            }
        }

        /// <summary>
        /// A warning about the pick, or empty: the connection's reason while no command may be sent, else that the picked tile is occupied in the
        /// displayed city. While it is shown <see cref="CanConfirm"/> is false.
        /// </summary>
        public string HintText
        {
            get
            {
                if (Phase == PlacementPhase.Confirming && !_availability.IsAllowed)
                {
                    return _availability.Reason;
                }

                bool occupied = Phase == PlacementPhase.Confirming && HintAt(Tile!.Value) == TileHint.Occupied;
                return occupied ? "This tile is occupied" : string.Empty;
            }
        }

        /// <summary>
        /// Sets the picked type (from the build menu's selection); null clears it, and with it the tile and the error. Does nothing when the
        /// type is the one already picked, and while a command is on its way: what was confirmed stays what it was until it ends.
        /// </summary>
        public void SetType(BuildingTypeId? typeId)
        {
            if (_sending != null || TypeId == typeId)
            {
                return;
            }

            TypeId = typeId;
            ErrorText = string.Empty;
            if (!typeId.HasValue)
            {
                Tile = null;
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Sets the picked tile (from the ground's selection); null clears it. Does nothing when it is the one already picked, and while a
        /// command is on its way.
        /// </summary>
        public void SetTile(GridCoordinate? tile)
        {
            if (_sending != null || Tile == tile)
            {
                return;
            }

            Tile = tile;
            ErrorText = string.Empty;
            Changed?.Invoke();
        }

        /// <summary>
        /// The connection changed (M0-55): Place follows whether a command may be sent. Raises <see cref="Changed"/> when the availability differs.
        /// A command already on its way is not touched.
        /// </summary>
        /// <param name="availability">Whether a command may be sent now.</param>
        public void SetCommandAvailability(CommandAvailability availability)
        {
            if (_availability.Equals(availability))
            {
                return;
            }

            _availability = availability;
            Changed?.Invoke();
        }

        /// <summary>
        /// Makes the hints for <paramref name="city"/>: a tile with a building of any phase is occupied, the others are free. Raises
        /// <see cref="Changed"/> only when a hint differs.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> is null.</exception>
        public void Update(CityState city)
        {
            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            Array.Clear(_scratch, 0, _scratch.Length);
            for (int index = 0; index < city.Buildings.Count; index++)
            {
                Building building = city.Buildings[index];
                if (_grid.Contains(new GridCoordinate(building.X, building.Y)))
                {
                    _scratch[building.Y * _grid.Width + building.X] = TileHint.Occupied;
                }
            }

            bool changed = false;
            for (int index = 0; !changed && index < _hints.Length; index++)
            {
                changed = _hints[index] != _scratch[index];
            }

            if (!changed)
            {
                return;
            }

            TileHint[] old = _hints;
            _hints = _scratch;
            _scratch = old;
            Changed?.Invoke();
        }

        /// <summary>
        /// The player pressed Place. Starts the command: the flow is <see cref="PlacementPhase.Sending"/> until <see cref="Succeeded"/> or
        /// <see cref="Failed"/>.
        /// </summary>
        /// <param name="intent">What to give the command sender, or null when <see cref="CanConfirm"/> is false.</param>
        /// <returns>True when there is an intent to send.</returns>
        public bool TryConfirm(out CommandIntent? intent)
        {
            if (!CanConfirm)
            {
                intent = null;
                return false;
            }

            GridCoordinate tile = Tile!.Value;
            intent = _sending = CommandIntent.PlaceBuilding(TypeId!.Value.Value, tile.X, tile.Y);
            ErrorText = string.Empty;
            Changed?.Invoke();
            return true;
        }

        /// <summary>The server accepted the command: the building comes with the snapshot, and the flow ends.</summary>
        public void Succeeded()
        {
            if (_sending == null)
            {
                return;
            }

            End();
        }

        /// <summary>
        /// The command did not take effect: the card stays picked, the tile is dropped, and <see cref="ErrorText"/> says why, so the player can
        /// try another tile.
        /// </summary>
        /// <param name="error">Why it failed.</param>
        /// <exception cref="ArgumentNullException"><paramref name="error"/> is null.</exception>
        public void Failed(ApiError error)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            if (_sending == null)
            {
                return;
            }

            _sending = null;
            Tile = null;
            ErrorText = FeedbackMessages.ForError(error);
            Changed?.Invoke();
        }

        /// <summary>The player cancelled: the card, the tile and the error are dropped. Does nothing while a command is on its way, or when no card is picked.</summary>
        public void Cancel()
        {
            if (CanCancel)
            {
                End();
            }
        }

        /// <summary>Drops everything and tells the holders of the card and the tile.</summary>
        private void End()
        {
            _sending = null;
            TypeId = null;
            Tile = null;
            ErrorText = string.Empty;
            Changed?.Invoke();
            Ended?.Invoke();
        }

        /// <summary>The name of the picked type from the content, or its id when the content has none.</summary>
        private string NameOfType()
        {
            if (!TypeId.HasValue)
            {
                return string.Empty;
            }

            return _content.TryGetBuilding(TypeId.Value, out BuildingDefinition? definition) ? definition.Name : TypeId.Value.Value;
        }

        /// <summary>The tile of the command on its way.</summary>
        private GridCoordinate SendingTile()
        {
            return new GridCoordinate(_sending!.X, _sending.Y);
        }
    }
}
