#nullable enable

using System;

namespace CityBuilder.Client.Core
{
    /// <summary>
    /// The city clock the client shows between snapshots (<c>core-instructions/05-api-contract.md</c>, <c>clientClock</c>): the snapshot's clock
    /// plus the whole seconds since it arrived, times the server clock's speed. For display only: it is never sent anywhere.
    /// </summary>
    public sealed class DisplayClock
    {
        /// <summary>The store whose snapshot the clock counts from.</summary>
        private readonly ClientCityStore _store;

        /// <summary>How many city seconds pass per real second.</summary>
        private readonly int _timeScale;

        /// <summary>Creates a clock.</summary>
        /// <param name="store">The store whose snapshot it counts from.</param>
        /// <param name="timeScale">The <c>timeScale</c> of <c>GET /content</c>, at least 1: 1 except on a Development server with a dev time scale.</param>
        /// <exception cref="ArgumentNullException"><paramref name="store"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeScale"/> is below 1.</exception>
        public DisplayClock(ClientCityStore store, int timeScale)
        {
            if (timeScale < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(timeScale), timeScale, "The time scale must be at least 1.");
            }

            _store = store ?? throw new ArgumentNullException(nameof(store));
            _timeScale = timeScale;
        }

        /// <summary>The city seconds that have passed since the snapshot arrived; 0 before the first one.</summary>
        public long ElapsedSeconds => _store.WholeSecondsSinceReceived * _timeScale;

        /// <summary>The city clock to show, in seconds; 0 before the first snapshot.</summary>
        public long Now
        {
            get
            {
                CitySnapshot? snapshot = _store.Snapshot;
                return snapshot == null ? 0 : snapshot.Clock + ElapsedSeconds;
            }
        }
    }
}
