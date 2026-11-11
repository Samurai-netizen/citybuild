#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Build;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Presentation.Roads;
using CityBuilder.Client.Presentation.Routes;
using CityBuilder.Client.Presentation.Trips;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using UnityEngine;

namespace CityBuilder.Client.Presentation.Inspector
{
    /// <summary>
    /// The building inspector's wiring (M0-51): it joins the ground's tile selection, the displayed city, the command sender and the
    /// <see cref="BuildingInspector"/> screen, and holds no rule of its own. A tile the player taps that holds a building opens the inspector
    /// for it (while the placement flow has nothing in progress: then the tile is the placement's); the screen is rebuilt from
    /// <see cref="BuildingInspectorModel"/> whenever the city, the selection, the placement or the sender's pending list changes. The − and +
    /// buttons give the sender an absolute worker target one below or above the shown value (the sender coalesces rapid taps and waits out the
    /// debounce) and Upgrade gives it an upgrade intent; both are shown as pending, and the model switches the button off while its intent is
    /// on its way, so a double tap cannot submit twice. While the connection does not allow a command (<see cref="CommandAvailability"/>, set by
    /// <see cref="SetCommandAvailability"/>) all three are off with the reason, and a press that still arrives sends nothing. It subtracts nothing and changes no worker count: the new state comes with the snapshot
    /// the sender hands to the city store, and a refusal is shown as a toast by the feedback (M0-47). <c>GameRoot</c> creates it once connected
    /// and calls <see cref="Dispose"/> when it is destroyed.
    /// </summary>
    public sealed class InspectorFlow : IDisposable
    {
        /// <summary>The game content the screen's words come from.</summary>
        private readonly GameContent _content;

        /// <summary>The ground whose tile selection opens the inspector.</summary>
        private readonly CityWorld _world;

        /// <summary>The way a command reaches the server and the source of the pending state.</summary>
        private readonly CommandSender _sender;

        /// <summary>The screen.</summary>
        private readonly BuildingInspector _inspector;

        /// <summary>The placement flow's state, or null when there is no placement; the inspector stays closed while it has something in progress.</summary>
        private readonly PlacementFlowModel? _placement;

        /// <summary>The manual trip flow's state, or null when there is none; the inspector stays closed while the player picks a trip target (CR-0008).</summary>
        private readonly TripFlowModel? _trips;

        /// <summary>The road mode flow's state, or null when there is none; the inspector stays closed while the player builds or edits roads (CR-0008).</summary>
        private readonly RoadFlowModel? _roads;

        /// <summary>The route editor's state, or null when there is none; the inspector stays closed while the player edits a courier route (CR-0008).</summary>
        private readonly RouteEditorModel? _routes;

        /// <summary>Ends a command that is still open when the game object goes away.</summary>
        private readonly CancellationToken _cancellationToken;

        /// <summary>The latest displayed city, or null before the first <see cref="Update"/>.</summary>
        private CityState? _city;

        /// <summary>Whether a command may be sent now; allowed until <see cref="SetCommandAvailability"/> says otherwise.</summary>
        private CommandAvailability _availability = CommandAvailability.Allowed;

        /// <summary>Whether <see cref="Dispose"/> has run.</summary>
        private bool _disposed;

        /// <summary>Starts listening to the ground, the sender, the placement and the screen.</summary>
        /// <param name="content">The game content.</param>
        /// <param name="world">The city's ground and tile selection; its grid must be shown already.</param>
        /// <param name="sender">The command sender.</param>
        /// <param name="inspector">The screen.</param>
        /// <param name="placement">The placement flow's state, or null when there is none.</param>
        /// <param name="cancellationToken">Cancels a command still open when the game goes away (<c>destroyCancellationToken</c>).</param>
        /// <param name="trips">The manual trip flow's state, or null when there is none (CR-0008).</param>
        /// <param name="roads">The road mode flow's state, or null when there is none (CR-0008).</param>
        /// <param name="routes">The route editor's state, or null when there is none (CR-0008).</param>
        /// <exception cref="ArgumentNullException">An argument other than <paramref name="placement"/>, <paramref name="trips"/> or <paramref name="roads"/> is null.</exception>
        /// <exception cref="ArgumentException">The world has no grid shown yet.</exception>
        public InspectorFlow(
            GameContent content,
            CityWorld world,
            CommandSender sender,
            BuildingInspector inspector,
            PlacementFlowModel? placement,
            CancellationToken cancellationToken,
            TripFlowModel? trips = null,
            RoadFlowModel? roads = null,
            RouteEditorModel? routes = null)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _world = world != null ? world : throw new ArgumentNullException(nameof(world));
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _inspector = inspector != null ? inspector : throw new ArgumentNullException(nameof(inspector));
            _placement = placement;
            _trips = trips;
            _roads = roads;
            _routes = routes;
            _cancellationToken = cancellationToken;
            if (_world.Selection == null)
            {
                throw new ArgumentException("The world must show its grid before the flow starts.", nameof(world));
            }

            _world.Selection.Changed += Refresh;
            _sender.PendingChanged += Refresh;
            if (_placement != null)
            {
                _placement.Changed += Refresh;
            }

            if (_trips != null)
            {
                _trips.Changed += Refresh;
            }

            if (_roads != null)
            {
                _roads.Changed += Refresh;
            }

            if (_routes != null)
            {
                _routes.Changed += Refresh;
            }

            _inspector.MinusRequested += OnMinus;
            _inspector.PlusRequested += OnPlus;
            _inspector.UpgradeRequested += OnUpgrade;
            _inspector.ReenergizeRequested += OnReenergize;
            _inspector.CloseRequested += OnClose;
        }

        /// <summary>The displayed city changed (a new snapshot, or a whole second of prediction): the screen follows it. Call it from the same place that updates the other screens.</summary>
        /// <param name="city">The displayed city.</param>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> is null.</exception>
        public void Update(CityState city)
        {
            _city = city ?? throw new ArgumentNullException(nameof(city));
            Refresh();
        }

        /// <summary>The connection changed (M0-55): the buttons follow whether a command may be sent. Pending commands are not touched.</summary>
        /// <param name="availability">Whether a command may be sent now.</param>
        public void SetCommandAvailability(CommandAvailability availability)
        {
            if (_availability.Equals(availability))
            {
                return;
            }

            _availability = availability;
            Refresh();
        }

        /// <summary>
        /// Closes the inspector because another menu opened (<see cref="Ui.MenuGroup"/>): the tile is dropped, which hides the screen. Does nothing
        /// while the inspector is hidden: the tile may then be the placement flow's, and dropping it would lose the player's pick.
        /// </summary>
        public void Close()
        {
            if (!_disposed && _inspector.IsShown)
            {
                _world.Selection!.ClearSelection();
            }
        }

        /// <summary>Stops listening to everything it listened to. Safe to call twice.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_world != null && _world.Selection != null)
            {
                _world.Selection.Changed -= Refresh;
            }

            _sender.PendingChanged -= Refresh;
            if (_placement != null)
            {
                _placement.Changed -= Refresh;
            }

            if (_trips != null)
            {
                _trips.Changed -= Refresh;
            }

            if (_roads != null)
            {
                _roads.Changed -= Refresh;
            }

            if (_routes != null)
            {
                _routes.Changed -= Refresh;
            }

            if (_inspector != null)
            {
                _inspector.MinusRequested -= OnMinus;
                _inspector.PlusRequested -= OnPlus;
                _inspector.UpgradeRequested -= OnUpgrade;
                _inspector.ReenergizeRequested -= OnReenergize;
                _inspector.CloseRequested -= OnClose;
            }
        }

        /// <summary>
        /// Shows the inspector for the building on the selected tile, or hides it: no city yet, a placement in progress, no tile, or a tile
        /// with no building (the content may also not know the building: the model is then null).
        /// </summary>
        private void Refresh()
        {
            if (_disposed || _inspector == null)
            {
                return;
            }

            _inspector.Show(ModelForSelection());
        }

        /// <summary>The model of the building on the selected tile, or null when nothing is to be shown.</summary>
        private BuildingInspectorModel? ModelForSelection()
        {
            GridCoordinate? tile = _world.Selection!.Selected;
            CityState? city = _city;
            if (city == null
                || !tile.HasValue
                || (_placement != null && _placement.Phase != PlacementPhase.Idle)
                || (_trips != null && _trips.Phase != TripPhase.Idle)
                || (_routes != null && _routes.Phase != RouteEditorPhase.Idle)
                || (_roads != null && _roads.Phase != RoadPhase.Idle && _roads.IsBuildMode))
            {
                return null;
            }

            for (int index = 0; index < city.Buildings.Count; index++)
            {
                Building building = city.Buildings[index];
                if (building.X == tile.Value.X && building.Y == tile.Value.Y)
                {
                    return BuildingInspectorModel.Create(_content, city, building, InspectorPending.From(building.Id, _sender.Pending), _availability);
                }
            }

            return null;
        }

        /// <summary>− was pressed: the target one below the value on the screen.</summary>
        private void OnMinus()
        {
            SendWorkers(-1);
        }

        /// <summary>+ was pressed: the target one above the value on the screen.</summary>
        private void OnPlus()
        {
            SendWorkers(1);
        }

        /// <summary>Gives the sender the absolute worker target <paramref name="step"/> away from the value on the screen.</summary>
        private void SendWorkers(int step)
        {
            BuildingInspectorModel? model = _inspector.Model;
            if (model != null && _availability.IsAllowed)
            {
                Send(CommandIntent.AssignWorkers(model.BuildingId, model.WorkersTarget + step));
            }
        }

        /// <summary>Upgrade was pressed.</summary>
        private void OnUpgrade()
        {
            BuildingInspectorModel? model = _inspector.Model;
            if (model != null && _availability.IsAllowed)
            {
                Send(CommandIntent.UpgradeBuilding(model.BuildingId));
            }
        }

        /// <summary>Re-energize couriers was pressed: the command has no argument.</summary>
        private void OnReenergize()
        {
            if (_inspector.Model != null && _availability.IsAllowed)
            {
                Send(CommandIntent.ReenergizeCouriers());
            }
        }

        /// <summary>The close button was pressed: the tile is dropped, which hides the screen.</summary>
        private void OnClose()
        {
            _world.Selection!.ClearSelection();
        }

        /// <summary>
        /// Gives the sender an intent and does not wait for it: the pending list shows it, and the feedback shows how it ended (a toast). The
        /// sender never throws but for a cancelled token, which is the normal end when the game is closing.
        /// </summary>
        private void Send(CommandIntent intent)
        {
            _ = SendAsync(intent);
        }

        /// <summary>Waits for the sender and ignores a cancel; logs anything else, so an un-awaited task never hides a fault.</summary>
        private async Task SendAsync(CommandIntent intent)
        {
            try
            {
                await _sender.SendAsync(intent, _cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The game is closing: nothing to show.
            }
            catch (Exception exception)
            {
                ClientLog.Exception(exception);
            }
        }
    }
}
