#nullable enable

using System;
using CityBuilder.Client.Core;
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

        /// <summary>The snapshot looked at last, so one snapshot is never reported twice.</summary>
        private CitySnapshot? _last;

        /// <summary>Whether <see cref="Dispose"/> has run.</summary>
        private bool _disposed;

        /// <summary>Starts listening to the store and looks at the snapshot it holds now.</summary>
        /// <param name="content">The game content.</param>
        /// <param name="store">The city store.</param>
        /// <param name="panel">The panel.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public WelcomeBackFlow(GameContent content, ClientCityStore store, WelcomeBackPanel panel)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _panel = panel != null ? panel : throw new ArgumentNullException(nameof(panel));
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
            WelcomeBackModel? model = WelcomeBackModel.Create(_content, snapshot);
            if (model != null && _panel != null)
            {
                _panel.Show(model);
            }
        }
    }
}
