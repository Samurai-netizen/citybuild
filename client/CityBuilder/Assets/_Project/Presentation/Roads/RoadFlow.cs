#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Build;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Presentation.Trips;

namespace CityBuilder.Client.Presentation.Roads
{
    /// <summary>
    /// Wires road mode (CR-0008): starts from the build menu's <see cref="BuildMenu.RoadsRequested"/> or by tapping an existing road tile on the map
    /// while no other flow is active, draws the selected tile line on <see cref="CityWorld.ShowPath"/> and free/occupied hints on
    /// <see cref="CityWorld.ShowHints"/>, updates <see cref="RoadBar"/>, and sends <c>place-roads</c>, <c>upgrade-roads</c> or <c>remove-roads</c>
    /// through <see cref="CommandSender"/>.
    /// </summary>
    public sealed class RoadFlow : IDisposable
    {
        /// <summary>The state model.</summary>
        private readonly RoadFlowModel _model;

        /// <summary>The city world (selection, hints and line preview).</summary>
        private readonly CityWorld _world;

        /// <summary>The command sender.</summary>
        private readonly CommandSender _sender;

        /// <summary>The road bar view.</summary>
        private readonly RoadBar _bar;

        /// <summary>The build menu whose Roads button starts road build mode.</summary>
        private readonly BuildMenu _buildMenu;

        /// <summary>The building placement flow model, checked before opening an existing road tile.</summary>
        private readonly PlacementFlowModel? _placement;

        /// <summary>The manual trip flow model, checked before opening an existing road tile.</summary>
        private readonly TripFlowModel? _trips;

        /// <summary>Cancels in-flight commands on shutdown.</summary>
        private readonly CancellationToken _cancellationToken;

        /// <summary>The selection the flow currently listens to.</summary>
        private TileSelection? _selection;

        /// <summary>The last selected tile seen on <see cref="_selection"/>, so hover changes can be told apart from taps.</summary>
        private GridCoordinate? _lastSelected;

        /// <summary>Whether <see cref="Dispose"/> has been called.</summary>
        private bool _disposed;

        /// <summary>Creates and connects the road flow.</summary>
        /// <exception cref="ArgumentNullException">An argument other than <paramref name="placement"/> or <paramref name="trips"/> is null.</exception>
        public RoadFlow(
            RoadFlowModel model,
            CityWorld world,
            CommandSender sender,
            RoadBar bar,
            BuildMenu buildMenu,
            PlacementFlowModel? placement,
            TripFlowModel? trips,
            CancellationToken cancellationToken)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _world = world != null ? world : throw new ArgumentNullException(nameof(world));
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _bar = bar != null ? bar : throw new ArgumentNullException(nameof(bar));
            _buildMenu = buildMenu != null ? buildMenu : throw new ArgumentNullException(nameof(buildMenu));
            _placement = placement;
            _trips = trips;
            _cancellationToken = cancellationToken;

            _buildMenu.RoadsRequested += OnRoadsRequested;
            _bar.PlaceRequested += OnPlaceRequested;
            _bar.UpgradeRequested += OnUpgradeRequested;
            _bar.RemoveRequested += OnRemoveRequested;
            _bar.CancelRequested += OnCancelRequested;
            _model.Changed += OnModelChanged;
            RebindSelection();
            OnModelChanged();
        }

        /// <summary>The state model, for tests and wiring.</summary>
        public RoadFlowModel Model => _model;

        /// <summary>Re-attaches to <see cref="CityWorld.Selection"/> after <see cref="CityWorld.ShowGrid"/>.</summary>
        public void RebindSelection()
        {
            if (_disposed || ReferenceEquals(_selection, _world.Selection))
            {
                return;
            }

            if (_selection != null)
            {
                _selection.Changed -= OnSelectionChanged;
            }

            _selection = _world.Selection;
            _lastSelected = _selection?.Selected;
            if (_selection != null)
            {
                _selection.Changed += OnSelectionChanged;
            }
        }

        /// <summary>Disconnects all listeners and clears the ground hints and path preview.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _buildMenu.RoadsRequested -= OnRoadsRequested;
            _bar.PlaceRequested -= OnPlaceRequested;
            _bar.UpgradeRequested -= OnUpgradeRequested;
            _bar.RemoveRequested -= OnRemoveRequested;
            _bar.CancelRequested -= OnCancelRequested;
            _model.Changed -= OnModelChanged;
            if (_selection != null)
            {
                _selection.Changed -= OnSelectionChanged;
                _selection = null;
            }

            if (_placement == null || !_placement.ShowsHints)
            {
                _world.ShowHints(null);
            }

            if (_trips == null || _trips.Phase == TripPhase.Idle)
            {
                _world.ShowPath(null);
            }
        }

        /// <summary>Opens road build mode from the build menu, closing the sheet and clearing any building card selection.</summary>
        private void OnRoadsRequested()
        {
            if (_disposed)
            {
                return;
            }

            _buildMenu.Model?.ClearSelection();
            _placement?.Cancel();
            _trips?.Cancel();
            _buildMenu.SetOpen(false);
            _selection?.ClearSelection();
            _lastSelected = null;
            _model.StartBuildMode();
        }

        /// <summary>Handles a tap or hover change on <see cref="CityWorld.Selection"/>.</summary>
        private void OnSelectionChanged()
        {
            if (_disposed || _selection == null)
            {
                return;
            }

            GridCoordinate? selected = _selection.Selected;
            bool selectedChanged = selected != _lastSelected;
            _lastSelected = selected;

            if (_model.Phase == RoadPhase.Idle)
            {
                if (!selectedChanged || !selected.HasValue)
                {
                    return;
                }

                if ((_placement != null && _placement.Phase != PlacementPhase.Idle)
                    || (_trips != null && _trips.Phase != TripPhase.Idle))
                {
                    return;
                }

                _model.InspectRoadAt(selected.Value);
                return;
            }

            if (!selectedChanged)
            {
                return;
            }

            _model.TapTile(selected);
        }

        /// <summary>Cancels road mode and clears the map selection.</summary>
        private void OnCancelRequested()
        {
            if (_disposed)
            {
                return;
            }

            _model.Cancel();
            _selection?.ClearSelection();
            _lastSelected = null;
        }

        /// <summary>Confirms placing roads and sends <c>place-roads</c>.</summary>
        private void OnPlaceRequested()
        {
            if (_disposed || !_model.TryConfirmPlace(out CommandIntent? intent))
            {
                return;
            }

            _ = SendAsync(intent!);
        }

        /// <summary>Confirms upgrading roads and sends <c>upgrade-roads</c>.</summary>
        private void OnUpgradeRequested()
        {
            if (_disposed || !_model.TryConfirmUpgrade(out CommandIntent? intent))
            {
                return;
            }

            _ = SendAsync(intent!);
        }

        /// <summary>Confirms removing roads and sends <c>remove-roads</c>.</summary>
        private void OnRemoveRequested()
        {
            if (_disposed || !_model.TryConfirmRemove(out CommandIntent? intent))
            {
                return;
            }

            _ = SendAsync(intent!);
        }

        /// <summary>Sends <paramref name="intent"/> and finishes road mode when the server answers.</summary>
        private async Task SendAsync(CommandIntent intent)
        {
            try
            {
                CommandOutcome outcome = await _sender.SendAsync(intent, _cancellationToken);
                if (_disposed)
                {
                    return;
                }

                bool succeeded = outcome.IsSuccess;
                _model.FinishCommand(succeeded);
                if (succeeded)
                {
                    _selection?.ClearSelection();
                    _lastSelected = null;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                ClientLog.Exception(exception);
                if (!_disposed)
                {
                    _model.FinishCommand(false);
                }
            }
        }

        /// <summary>Updates the bar, ground hints and line preview whenever the model changes.</summary>
        private void OnModelChanged()
        {
            if (_disposed)
            {
                return;
            }

            _bar.Refresh();
            if (_placement == null || !_placement.ShowsHints)
            {
                _world.ShowHints(_model.ShowsHints ? _model.Hints : null);
            }

            if (_trips == null || _trips.Phase == TripPhase.Idle)
            {
                _world.ShowPath(_model.Phase == RoadPhase.Idle ? null : _model.PreviewSteps);
            }
        }
    }
}
