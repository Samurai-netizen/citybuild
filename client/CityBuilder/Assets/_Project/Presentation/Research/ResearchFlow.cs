#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using UnityEngine;

namespace CityBuilder.Client.Presentation.Research
{
    /// <summary>
    /// The research screen's wiring (M0-52): it joins the displayed city, the command sender and the <see cref="ResearchScreen"/>, and holds no
    /// rule of its own. The screen is rebuilt from <see cref="ResearchScreenModel"/> whenever the city or the sender's pending list changes; a
    /// press on Finance gives the sender a <c>StartResearch</c> intent, which the model shows as pending (every Finance button is off while it
    /// is on its way, so a double tap cannot submit twice). While the connection does not allow a command (<see cref="CommandAvailability"/>, set by
    /// <see cref="SetCommandAvailability"/>) every Finance button is off with the reason, and a press that still arrives sends nothing. It subtracts nothing and starts nothing: the new state comes with the snapshot the
    /// sender hands to the city store, and a refusal is shown as a toast by the feedback (M0-47). <c>GameRoot</c> creates it once connected and
    /// calls <see cref="Dispose"/> when it is destroyed.
    /// </summary>
    public sealed class ResearchFlow : IDisposable
    {
        /// <summary>The game content the screen's words come from.</summary>
        private readonly GameContent _content;

        /// <summary>The way a command reaches the server and the source of the pending state.</summary>
        private readonly CommandSender _sender;

        /// <summary>The screen.</summary>
        private readonly ResearchScreen _screen;

        /// <summary>Ends a command that is still open when the game object goes away.</summary>
        private readonly CancellationToken _cancellationToken;

        /// <summary>The latest displayed city, or null before the first <see cref="Update"/>.</summary>
        private CityState? _city;

        /// <summary>Whether a command may be sent now; allowed until <see cref="SetCommandAvailability"/> says otherwise.</summary>
        private CommandAvailability _availability = CommandAvailability.Allowed;

        /// <summary>Whether <see cref="Dispose"/> has run.</summary>
        private bool _disposed;

        /// <summary>Starts listening to the sender and the screen.</summary>
        /// <param name="content">The game content.</param>
        /// <param name="sender">The command sender.</param>
        /// <param name="screen">The screen.</param>
        /// <param name="cancellationToken">Cancels a command still open when the game goes away (<c>destroyCancellationToken</c>).</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public ResearchFlow(GameContent content, CommandSender sender, ResearchScreen screen, CancellationToken cancellationToken)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _screen = screen != null ? screen : throw new ArgumentNullException(nameof(screen));
            _cancellationToken = cancellationToken;
            _sender.PendingChanged += Refresh;
            _screen.FinanceRequested += OnFinance;
        }

        /// <summary>The displayed city changed (a new snapshot, or a whole second of prediction): the screen follows it. Call it from the same place that updates the other screens.</summary>
        /// <param name="city">The displayed city.</param>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> is null.</exception>
        public void Update(CityState city)
        {
            _city = city ?? throw new ArgumentNullException(nameof(city));
            Refresh();
        }

        /// <summary>The connection changed (M0-55): the buttons follow whether a command may be sent. A start already on its way is not touched.</summary>
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

        /// <summary>Closes the screen because another menu opened (<see cref="Ui.MenuGroup"/>). Safe when it is closed already.</summary>
        public void Close()
        {
            if (!_disposed)
            {
                _screen.SetOpen(false);
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
            _sender.PendingChanged -= Refresh;
            if (_screen != null)
            {
                _screen.FinanceRequested -= OnFinance;
            }
        }

        /// <summary>Shows the screen for the latest city and the pending start; does nothing before the first city.</summary>
        private void Refresh()
        {
            if (_disposed || _screen == null || _city == null)
            {
                return;
            }

            _screen.Show(ResearchScreenModel.Create(_content, _city, ResearchPending.From(_sender.Pending), _availability));
        }

        /// <summary>Finance was pressed on the node <paramref name="id"/>: gives the sender the intent and does not wait for it.</summary>
        private void OnFinance(string id)
        {
            if (!_availability.IsAllowed)
            {
                return;
            }

            _ = SendAsync(CommandIntent.StartResearch(id));
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
                Debug.LogException(exception);
            }
        }
    }
}
