#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Feedback;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Simulation;
using CityBuilder.Simulation.State;
using UnityEngine;

namespace CityBuilder.Client.Presentation.Build
{
    /// <summary>
    /// The placement flow's wiring (M0-46): it joins the build menu's pick, the ground's tile selection, the bar's buttons and the command
    /// sender around a <see cref="PlacementFlowModel"/>, and holds no rule of its own. A card picked in the menu starts the flow and closes
    /// the sheet; a tap on the ground picks the tile; <b>Place</b> gives the sender a place-building intent and waits for how it ended;
    /// the ground shows the model's hints and, from the sender's pending list, the ghost of every command on its way. It subtracts nothing
    /// and creates no building: the real building comes with the snapshot the sender hands to the city store. <c>GameRoot</c> creates it
    /// once connected and calls <see cref="Dispose"/> when it is destroyed.
    /// </summary>
    public sealed class PlacementFlow : IDisposable
    {
        /// <summary>The flow's state and words.</summary>
        private readonly PlacementFlowModel _model;

        /// <summary>The build menu's state: which card is picked.</summary>
        private readonly BuildMenuModel _menu;

        /// <summary>Closes the menu's sheet (a card was picked, so the ground should be visible).</summary>
        private readonly Action _closeMenu;

        /// <summary>The ground, the ghosts and the tile selection.</summary>
        private readonly CityWorld _world;

        /// <summary>The way the command reaches the server.</summary>
        private readonly CommandSender _sender;

        /// <summary>The bar whose buttons start and cancel the flow.</summary>
        private readonly PlacementBar _bar;

        /// <summary>The era of the displayed city, which picks the ghosts' look.</summary>
        private readonly Func<int> _era;

        /// <summary>Ends a command that is still open when the game object goes away.</summary>
        private readonly CancellationToken _cancellationToken;

        /// <summary>True while the flow itself changes the menu's pick, so its own change is not taken for the player's.</summary>
        private bool _settingMenuPick;

        /// <summary>Whether <see cref="Dispose"/> has run.</summary>
        private bool _disposed;

        /// <summary>Starts listening to the menu, the ground, the bar and the sender.</summary>
        /// <param name="model">The flow's state.</param>
        /// <param name="menu">The build menu's state.</param>
        /// <param name="closeMenu">Closes the menu's sheet.</param>
        /// <param name="world">The city's ground, ghosts and tile selection; its grid must be shown already.</param>
        /// <param name="sender">The command sender.</param>
        /// <param name="bar">The placement bar.</param>
        /// <param name="era">The era of the displayed city.</param>
        /// <param name="cancellationToken">Cancels a command still open when the game goes away (<c>destroyCancellationToken</c>).</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">The world has no grid shown yet.</exception>
        public PlacementFlow(
            PlacementFlowModel model,
            BuildMenuModel menu,
            Action closeMenu,
            CityWorld world,
            CommandSender sender,
            PlacementBar bar,
            Func<int> era,
            CancellationToken cancellationToken)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _menu = menu ?? throw new ArgumentNullException(nameof(menu));
            _closeMenu = closeMenu ?? throw new ArgumentNullException(nameof(closeMenu));
            _world = world != null ? world : throw new ArgumentNullException(nameof(world));
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _bar = bar != null ? bar : throw new ArgumentNullException(nameof(bar));
            _era = era ?? throw new ArgumentNullException(nameof(era));
            _cancellationToken = cancellationToken;
            if (_world.Selection == null)
            {
                throw new ArgumentException("The world must show its grid before the flow starts.", nameof(world));
            }

            _menu.SelectionChanged += OnMenuPickChanged;
            _world.Selection.Changed += OnTileChanged;
            _model.Changed += ShowHints;
            _model.Ended += OnEnded;
            _sender.PendingChanged += ShowGhosts;
            _bar.PlaceRequested += OnPlaceRequested;
            _bar.CancelRequested += _model.Cancel;
            ShowHints();
            ShowGhosts();
        }

        /// <summary>The flow's state, for the bar and for tests.</summary>
        public PlacementFlowModel Model => _model;

        /// <summary>The displayed city changed: the hints follow its buildings. Call it from the same place that updates the build menu.</summary>
        /// <param name="city">The displayed city.</param>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> is null.</exception>
        public void Update(CityState city)
        {
            _model.Update(city);
        }

        /// <summary>Stops listening to everything it listened to. Safe to call twice.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _menu.SelectionChanged -= OnMenuPickChanged;
            if (_world != null && _world.Selection != null)
            {
                _world.Selection.Changed -= OnTileChanged;
            }

            _model.Changed -= ShowHints;
            _model.Ended -= OnEnded;
            _sender.PendingChanged -= ShowGhosts;
            if (_bar != null)
            {
                _bar.PlaceRequested -= OnPlaceRequested;
                _bar.CancelRequested -= _model.Cancel;
            }
        }

        /// <summary>
        /// The player picked a card or dropped it. A new pick starts the flow with no tile (a tile tapped before the card is not part of it) and
        /// closes the sheet so the ground can be seen; a dropped pick ends the flow's tile too.
        /// </summary>
        private void OnMenuPickChanged()
        {
            if (_settingMenuPick || _model.Phase == PlacementPhase.Sending)
            {
                return;
            }

            bool starts = _menu.Selected.HasValue && !_model.TypeId.HasValue;
            if (starts || !_menu.Selected.HasValue)
            {
                _world.Selection!.ClearSelection();
            }

            _model.SetType(_menu.Selected);
            if (starts)
            {
                _closeMenu();
            }
        }

        /// <summary>The player tapped a tile of the ground (or the same one again): the model follows.</summary>
        private void OnTileChanged()
        {
            _model.SetTile(_world.Selection!.Selected);
        }

        /// <summary>The model changed: the ground paints its hints while a card is picked, and gives the tiles their colour back otherwise.</summary>
        private void ShowHints()
        {
            _world.ShowHints(_model.ShowsHints ? _model.Hints : null);
        }

        /// <summary>The sender's pending list changed: the ghosts follow it.</summary>
        private void ShowGhosts()
        {
            _world.ShowGhosts(_sender.Pending, _era());
        }

        /// <summary>The flow ended (a success or a cancel): the card and the tile are dropped, which the model cannot do for the menu and the ground.</summary>
        private void OnEnded()
        {
            _world.Selection!.ClearSelection();
            SetMenuPick(null);
        }

        /// <summary>The player pressed Place.</summary>
        private void OnPlaceRequested()
        {
            _ = ConfirmAsync();
        }

        /// <summary>
        /// Makes the intent and waits for how the sender ended it. Never throws: a cancelled token (the game is closing) is the normal end, and
        /// whatever else goes wrong is logged.
        /// </summary>
        private async Task ConfirmAsync()
        {
            if (!_model.TryConfirm(out CommandIntent? intent))
            {
                return;
            }

            try
            {
                CommandOutcome outcome = await _sender.SendAsync(intent!, _cancellationToken);
                if (_disposed)
                {
                    return;
                }

                if (outcome.IsSuccess)
                {
                    _model.Succeeded();
                }
                else
                {
                    Fail(outcome.Error ?? new ApiError(FeedbackMessages.CommandFailedCode, 0, "The command did not finish.", null, null, null));
                }
            }
            catch (OperationCanceledException)
            {
                // The game is closing: nothing to show.
            }
            catch (Exception exception)
            {
                // Whatever it was, the bar must not stay on "Building…" for good.
                Debug.LogException(exception);
                if (!_disposed)
                {
                    Fail(new ApiError(FeedbackMessages.CommandFailedCode, 0, exception.Message, null, null, null));
                }
            }
        }

        /// <summary>Tells the model the command failed, and puts the ground and the menu in step with the card the model kept.</summary>
        private void Fail(ApiError error)
        {
            _model.Failed(error);
            AfterFailure();
        }

        /// <summary>
        /// A refusal left the card picked and dropped the tile: the ground forgets the tile, and the menu shows the card again if the player
        /// changed its pick while the command was on its way (the model ignored that, the menu did not).
        /// </summary>
        private void AfterFailure()
        {
            _world.Selection!.ClearSelection();
            if (_model.TypeId != _menu.Selected)
            {
                SetMenuPick(_model.TypeId);
                if (_menu.Selected != _model.TypeId)
                {
                    // The card cannot be picked any more (locked since): the flow cannot go on without it.
                    _model.Cancel();
                }
            }
        }

        /// <summary>Makes the menu's pick <paramref name="typeId"/> without the flow taking its own change for the player's.</summary>
        private void SetMenuPick(BuildingTypeId? typeId)
        {
            _settingMenuPick = true;
            try
            {
                if (_menu.Selected != typeId)
                {
                    _menu.ClearSelection();
                    if (typeId.HasValue)
                    {
                        _menu.TryToggle(typeId.Value);
                    }
                }
            }
            finally
            {
                _settingMenuPick = false;
            }
        }
    }
}
