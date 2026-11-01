#nullable enable

using System;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;

namespace CityBuilder.Client.Presentation.Connection
{
    /// <summary>
    /// The connection banner's wiring (M0-55): it joins the <see cref="ConnectionMonitor"/> (the state), the <see cref="ClientCityStore"/> (the age of
    /// the last snapshot) and the <see cref="ConnectionBanner"/>, and holds no rule of its own; the words are <see cref="ConnectionBannerModel"/>'s.
    /// The banner is rebuilt when the state changes, when a snapshot arrives, when <see cref="Update"/> says a second has passed (the age moves) and
    /// when a retry starts or ends. The action (Try again, Sign in again, Check again) asks the monitor for a poll: a poll after a refused token
    /// already logs in once more and repeats the request, so one action serves all three. It never touches the city: the city on screen is the last
    /// snapshot with display prediction, whatever the state. <c>GameRoot</c> creates it once connected and calls <see cref="Dispose"/> when it is destroyed.
    /// </summary>
    public sealed class ConnectionFlow : IDisposable
    {
        /// <summary>The state of the connection and the poll the action asks for.</summary>
        private readonly ConnectionMonitor _monitor;

        /// <summary>The store whose last snapshot's age the banner shows.</summary>
        private readonly ClientCityStore _store;

        /// <summary>The screen.</summary>
        private readonly ConnectionBanner _banner;

        /// <summary>Whether the player pressed the action and the poll it asked for has not ended.</summary>
        private bool _retrying;

        /// <summary>Whether <see cref="Dispose"/> has run.</summary>
        private bool _disposed;

        /// <summary>Starts listening to the monitor, the store and the screen, and shows the banner for the state now.</summary>
        /// <param name="monitor">The connection monitor.</param>
        /// <param name="store">The city store.</param>
        /// <param name="banner">The screen.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public ConnectionFlow(ConnectionMonitor monitor, ClientCityStore store, ConnectionBanner banner)
        {
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _banner = banner != null ? banner : throw new ArgumentNullException(nameof(banner));
            _monitor.StateChanged += Refresh;
            _monitor.PollFinished += OnPollFinished;
            _store.SnapshotChanged += Refresh;
            _banner.ActionRequested += OnAction;
            Refresh();
        }

        /// <summary>A second has passed (the displayed city changed): the age of the last sync may have moved. Call it from the same place that updates the other screens.</summary>
        public void Update()
        {
            Refresh();
        }

        /// <summary>Stops listening to everything it listened to. Safe to call twice.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _monitor.StateChanged -= Refresh;
            _monitor.PollFinished -= OnPollFinished;
            _store.SnapshotChanged -= Refresh;
            if (_banner != null)
            {
                _banner.ActionRequested -= OnAction;
            }
        }

        /// <summary>Shows the banner or the blocking screen for the state now, or neither.</summary>
        private void Refresh()
        {
            if (_disposed || _banner == null)
            {
                return;
            }

            _banner.Show(ConnectionBannerModel.Create(_monitor.State, _store.WholeSecondsSinceReceived, _retrying));
        }

        /// <summary>The action was pressed: the button says "Trying…" and the monitor is asked for a poll.</summary>
        private void OnAction()
        {
            _retrying = true;
            _monitor.RequestPoll();
            Refresh();
        }

        /// <summary>A poll ended, whatever it found: the button works again.</summary>
        private void OnPollFinished()
        {
            _retrying = false;
            Refresh();
        }
    }
}
