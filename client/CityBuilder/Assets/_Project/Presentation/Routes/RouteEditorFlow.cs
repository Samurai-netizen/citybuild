#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Presentation.Inspector;

namespace CityBuilder.Client.Presentation.Routes
{
    /// <summary>
    /// Joins the courier route editor (CR-0008-T2 part C): the inspector's Edit routes button opens it on slot 1 of the selected station, a tap on a
    /// building of the map adds it as a stop, every button of <see cref="RouteEditorScreen"/> becomes a call on <see cref="RouteEditorModel"/>,
    /// Save sends the <c>set-route</c> intent through the <see cref="CommandSender"/> and closes the editor on success, and the lap's path is drawn
    /// on the map while the editor is open (display only). Subscribes in its constructor and unsubscribes in <see cref="Dispose"/>.
    /// </summary>
    public sealed class RouteEditorFlow : IDisposable
    {
        /// <summary>The editor's state.</summary>
        private readonly RouteEditorModel _model;

        /// <summary>The world that shows the path and holds the tile selection.</summary>
        private readonly CityWorld _world;

        /// <summary>Sends the set-route command.</summary>
        private readonly CommandSender _sender;

        /// <summary>The sheet.</summary>
        private readonly RouteEditorScreen _screen;

        /// <summary>The inspector whose Edit routes button opens the editor.</summary>
        private readonly BuildingInspector _inspector;

        /// <summary>Ends a command that is still open when the game object goes away.</summary>
        private readonly CancellationToken _cancellationToken;

        /// <summary>The selection the flow is subscribed to; null before the world showed its grid.</summary>
        private TileSelection? _selection;

        /// <summary>Whether <see cref="Dispose"/> ran.</summary>
        private bool _disposed;

        /// <summary>Creates the flow and subscribes to the inspector, the screen and the model.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public RouteEditorFlow(
            RouteEditorModel model,
            CityWorld world,
            CommandSender sender,
            RouteEditorScreen screen,
            BuildingInspector inspector,
            CancellationToken cancellationToken)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _screen = screen ?? throw new ArgumentNullException(nameof(screen));
            _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
            _cancellationToken = cancellationToken;
            _inspector.EditRoutesRequested += OnEditRoutesRequested;
            _screen.SlotRequested += _model.CycleSlot;
            _screen.RevertRequested += _model.Revert;
            _screen.ConfirmRequested += OnConfirmRequested;
            _screen.CloseRequested += Close;
            _screen.StopMoveRequested += _model.MoveStop;
            _screen.StopRemoveRequested += _model.RemoveStop;
            _screen.ActionAddRequested += _model.AddAction;
            _screen.ActionKindRequested += _model.ToggleKind;
            _screen.ActionItemRequested += _model.CycleItem;
            _screen.ActionAmountRequested += _model.CycleAmount;
            _screen.ActionRemoveRequested += _model.RemoveAction;
            _model.Changed += OnModelChanged;
            _screen.Show(_model);
            RebindSelection();
        }

        /// <summary>Follows the world's tile selection; call it again after the world shows a new grid.</summary>
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

        /// <summary>
        /// Closes the editor without sending anything; the other menus call it when one of them opens (<see cref="Ui.MenuGroup"/>). Does nothing
        /// while the editor is closed: the tile may then be the inspector's, and dropping it would close the inspector that just opened (CR-0008-T3).
        /// </summary>
        public void Close()
        {
            if (_disposed || _model.Phase == RouteEditorPhase.Idle)
            {
                return;
            }

            _model.Cancel();
            _selection?.ClearSelection();
        }

        /// <summary>Unsubscribes from everything and clears the path.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _inspector.EditRoutesRequested -= OnEditRoutesRequested;
            _screen.SlotRequested -= _model.CycleSlot;
            _screen.RevertRequested -= _model.Revert;
            _screen.ConfirmRequested -= OnConfirmRequested;
            _screen.CloseRequested -= Close;
            _screen.StopMoveRequested -= _model.MoveStop;
            _screen.StopRemoveRequested -= _model.RemoveStop;
            _screen.ActionAddRequested -= _model.AddAction;
            _screen.ActionKindRequested -= _model.ToggleKind;
            _screen.ActionItemRequested -= _model.CycleItem;
            _screen.ActionAmountRequested -= _model.CycleAmount;
            _screen.ActionRemoveRequested -= _model.RemoveAction;
            _model.Changed -= OnModelChanged;
            if (_selection != null)
            {
                _selection.Changed -= OnSelectionChanged;
                _selection = null;
            }

            _world.ShowPath(null);
        }

        /// <summary>The player pressed Edit routes in the inspector: open the editor on the shown station and leave the inspector.</summary>
        private void OnEditRoutesRequested()
        {
            if (_disposed || _inspector.Model == null)
            {
                return;
            }

            int stationId = _inspector.Model.BuildingId;
            _selection?.ClearSelection();
            if (_model.Open(stationId, _model.FirstFreeSlot(stationId)))
            {
                _screen.SetOpen(true);
            }
        }

        /// <summary>A tile was tapped: while the editor is open, a building on it becomes the last stop, and the tap is then used up.</summary>
        private void OnSelectionChanged()
        {
            if (_disposed || _model.Phase != RouteEditorPhase.Editing || _selection == null || !_selection.Selected.HasValue)
            {
                return;
            }

            _model.SelectTile(_selection.Selected);
            _selection.ClearSelection();
        }

        /// <summary>The player pressed Save: send the route and wait for the answer.</summary>
        private void OnConfirmRequested()
        {
            if (_disposed || !_model.TryConfirm(out CommandIntent? intent))
            {
                return;
            }

            _ = SendAsync(intent!);
        }

        /// <summary>Sends <paramref name="intent"/>; success closes the editor, any failure returns to editing with the refusal shown as a toast.</summary>
        private async Task SendAsync(CommandIntent intent)
        {
            try
            {
                CommandOutcome outcome = await _sender.SendAsync(intent, _cancellationToken);
                if (!_disposed)
                {
                    _model.FinishCommand(outcome.IsSuccess);
                }
            }
            catch (OperationCanceledException)
            {
                // The game object went away; the flow is disposed with it.
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

        /// <summary>The model changed: write the sheet, draw the lap's path, and close the sheet once the editor is idle.</summary>
        private void OnModelChanged()
        {
            if (_disposed)
            {
                return;
            }

            _screen.Refresh();
            _world.ShowPath(_model.Phase == RouteEditorPhase.Idle ? null : _model.Path);
            if (_model.Phase == RouteEditorPhase.Idle && _screen.IsOpen)
            {
                _screen.SetOpen(false);
            }
        }
    }
}
