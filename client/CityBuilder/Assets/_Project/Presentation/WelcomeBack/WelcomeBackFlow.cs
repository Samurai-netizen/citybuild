#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Client.Presentation.WelcomeBack
{
    /// <summary>
    /// The welcome-back report's wiring (M0-53): it looks at every snapshot the <see cref="ClientCityStore"/> takes, the poll's, a command's and the
    /// first <c>GET /city</c>'s alike, makes the <see cref="WelcomeBackModel"/> of it and has the <see cref="WelcomeBackPanel"/> show it when there is one
    /// (a settlement of five minutes or more). It holds no rule of its own: what counts as "away" is the model's threshold. It also looks at the snapshot the
    /// store holds when it is created, because the startup flow stores the first answer, the one after a long absence, before any view exists. Each
    /// snapshot is looked at once. A report shown while another is open replaces it. <c>GameRoot</c> creates it once connected and calls
    /// <see cref="Dispose"/> when it is destroyed.
    /// </summary>
    public sealed class WelcomeBackFlow : IDisposable
    {
        /// <summary>The game content the report's names come from.</summary>
        private readonly GameContent _content;

        /// <summary>The store whose snapshots are looked at.</summary>
        private readonly ClientCityStore _store;

        /// <summary>The panel that shows the report.</summary>
        private readonly WelcomeBackPanel _panel;

        /// <summary>How many city seconds pass per real second; what counts as "away" is real time.</summary>
        private readonly int _timeScale;

        /// <summary>Sends the re-energize command; null in tests that need no command.</summary>
        private readonly CommandSender? _sender;

        /// <summary>Ends a command that is still open when the game object goes away.</summary>
        private readonly CancellationToken _cancellationToken;

        /// <summary>The snapshot looked at last, so one snapshot is never reported twice.</summary>
        private CitySnapshot? _last;

        /// <summary>Whether <see cref="Dispose"/> has run.</summary>
        private bool _disposed;

        /// <summary>Starts listening to the store and looks at the snapshot it holds now.</summary>
        /// <param name="content">The game content.</param>
        /// <param name="store">The city store.</param>
        /// <param name="panel">The panel.</param>
        /// <param name="timeScale">The <c>timeScale</c> of <c>GET /content</c>, at least 1.</param>
        /// <param name="sender">The command sender the Re-energize button uses; null leaves the button without effect.</param>
        /// <param name="cancellationToken">Cancelled when the game object goes away.</param>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public WelcomeBackFlow(
            GameContent content,
            ClientCityStore store,
            WelcomeBackPanel panel,
            int timeScale,
            CommandSender? sender = null,
            CancellationToken cancellationToken = default)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _panel = panel != null ? panel : throw new ArgumentNullException(nameof(panel));
            _timeScale = timeScale;
            _sender = sender;
            _cancellationToken = cancellationToken;
            _panel.ReenergizeRequested += OnReenergizeRequested;
            _store.SnapshotChanged += OnSnapshotChanged;
            OnSnapshotChanged();
        }

        /// <summary>Stops listening to the store. Safe to call twice.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _store.SnapshotChanged -= OnSnapshotChanged;
            _panel.ReenergizeRequested -= OnReenergizeRequested;
        }

        /// <summary>The player pressed Re-energize couriers: send the command; success closes the report, a refusal leaves it open (the toast says why).</summary>
        private void OnReenergizeRequested()
        {
            if (_disposed || _sender == null)
            {
                return;
            }

            _ = ReenergizeAsync(_sender);
        }

        /// <summary>Sends <c>reenergize-couriers</c> and waits for the answer.</summary>
        private async Task ReenergizeAsync(CommandSender sender)
        {
            _panel.SetReenergizeBusy(true);
            try
            {
                CommandOutcome outcome = await sender.SendAsync(CommandIntent.ReenergizeCouriers(), _cancellationToken);
                if (!_disposed && outcome.IsSuccess)
                {
                    _panel.Dismiss();
                }
            }
            catch (OperationCanceledException)
            {
                // The game object went away; the flow is disposed with it.
            }
            catch (Exception exception)
            {
                ClientLog.Exception(exception);
            }
            finally
            {
                if (!_disposed)
                {
                    _panel.SetReenergizeBusy(false);
                }
            }
        }

        /// <summary>The store took a snapshot (or the flow was just made): shows its report when it has one, once per snapshot.</summary>
        private void OnSnapshotChanged()
        {
            CitySnapshot? snapshot = _store.Snapshot;
            if (_disposed || snapshot == null || ReferenceEquals(snapshot, _last))
            {
                return;
            }

            _last = snapshot;
            WelcomeBackModel? model = WelcomeBackModel.Create(_content, snapshot, _timeScale);
            if (model != null && _panel != null)
            {
                _panel.Show(model);
            }
        }
    }
}
