#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Presentation.Inspector;

namespace CityBuilder.Client.Presentation.Trips
{
    /// <summary>
    /// Wires the manual trip flow (CR-0008): starts from the inspector's <see cref="BuildingInspector.SendTripRequested"/>, listens to map tile
    /// taps on <see cref="CityWorld.Selection"/>, draws the <c>PathFinder</c> path on <see cref="CityWorld.ShowPath"/>, updates <see cref="TripBar"/>,
    /// and sends <c>start-trip</c> through <see cref="CommandSender"/>.
    /// </summary>
    public sealed class TripFlow : IDisposable
    {
        /// <summary>The state model.</summary>
        private readonly TripFlowModel _model;

        /// <summary>The city world (selection and path preview).</summary>
        private readonly CityWorld _world;

        /// <summary>The command sender.</summary>
        private readonly CommandSender _sender;

        /// <summary>The trip bar view.</summary>
        private readonly TripBar _bar;

        /// <summary>The building inspector whose Send trip button starts the flow.</summary>
        private readonly BuildingInspector _inspector;

        /// <summary>Cancels in-flight commands on shutdown.</summary>
        private readonly CancellationToken _cancellationToken;

        /// <summary>The selection the flow currently listens to.</summary>
        private TileSelection? _selection;

        /// <summary>Whether <see cref="Dispose"/> has been called.</summary>
        private bool _disposed;

        /// <summary>Creates and connects the trip flow.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public TripFlow(
            TripFlowModel model,
            CityWorld world,
            CommandSender sender,
            TripBar bar,
            BuildingInspector inspector,
            CancellationToken cancellationToken)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _bar = bar ?? throw new ArgumentNullException(nameof(bar));
            _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
            _cancellationToken = cancellationToken;

            _inspector.SendTripRequested += OnSendTripRequested;
            _bar.ItemCycleRequested += _model.CycleItem;
            _bar.PresetRequested += _model.SetPreset;
            _bar.PercentRequested += _model.CyclePercent;
            _bar.ConfirmRequested += OnConfirmRequested;
            _bar.CancelRequested += OnCancelRequested;
            _model.Changed += OnModelChanged;
            RebindSelection();
            OnModelChanged();
        }

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
            if (_selection != null)
            {
                _selection.Changed += OnSelectionChanged;
            }
        }

        /// <summary>Disconnects all listeners and clears the path preview.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _inspector.SendTripRequested -= OnSendTripRequested;
            _bar.ItemCycleRequested -= _model.CycleItem;
            _bar.PresetRequested -= _model.SetPreset;
            _bar.PercentRequested -= _model.CyclePercent;
            _bar.ConfirmRequested -= OnConfirmRequested;
            _bar.CancelRequested -= OnCancelRequested;
            _model.Changed -= OnModelChanged;
            if (_selection != null)
            {
                _selection.Changed -= OnSelectionChanged;
                _selection = null;
            }

            _world.ShowPath(null);
        }

        /// <summary>Starts the trip flow from the currently inspected building and clears the map selection so the next tap picks the target.</summary>
        private void OnSendTripRequested()
        {
            if (_disposed || _inspector.Model == null)
            {
                return;
            }

            int sourceId = _inspector.Model.BuildingId;
            _selection?.ClearSelection();
            _model.StartFrom(sourceId);
        }

        /// <summary>Forwards a map tile tap to <see cref="TripFlowModel.SelectTile"/> while the trip flow is active.</summary>
        private void OnSelectionChanged()
        {
            if (_disposed || (_model.Phase != TripPhase.PickingTarget && _model.Phase != TripPhase.Confirming))
            {
                return;
            }

            _model.SelectTile(_selection?.Selected);
        }

        /// <summary>Cancels the trip flow and clears the map selection.</summary>
        private void OnCancelRequested()
        {
            if (_disposed)
            {
                return;
            }

            _model.Cancel();
            _selection?.ClearSelection();
        }

        /// <summary>Confirms the trip and sends <c>start-trip</c>.</summary>
        private void OnConfirmRequested()
        {
            if (_disposed || !_model.TryConfirm(out CommandIntent? intent))
            {
                return;
            }

            _ = SendAsync(intent!);
        }

        /// <summary>Sends <paramref name="intent"/> and finishes the flow when the server answers.</summary>
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

        /// <summary>Updates the bar and the ground path preview whenever the model changes.</summary>
        private void OnModelChanged()
        {
            if (_disposed)
            {
                return;
            }

            _bar.Refresh();
            _world.ShowPath(_model.Phase == TripPhase.Idle ? null : _model.Path);
        }
    }
}
