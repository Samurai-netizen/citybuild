#nullable enable

using System;
using UnityEngine;

namespace CityBuilder.Client.Core
{
    /// <summary>
    /// Holds the last authoritative snapshot and when it arrived (<c>core-instructions/09-unity-client.md</c>, "Runtime structure"). Every
    /// server answer goes through <see cref="Apply"/>; an answer that is not newer than the one held, such as an out-of-order response, is
    /// ignored. It holds what the server said and nothing else: a refused command never reaches it.
    /// </summary>
    public sealed class ClientCityStore
    {
        /// <summary>Reads the real-time clock, in seconds.</summary>
        private readonly Func<double> _realtimeSeconds;

        /// <summary>Creates a store on Unity's real-time clock, which ignores the game's time scale and pauses.</summary>
        public ClientCityStore()
            : this(() => Time.realtimeSinceStartupAsDouble)
        {
        }

        /// <summary>Creates a store on the given clock.</summary>
        /// <param name="realtimeSeconds">Reads a clock in seconds that only moves forward; a test passes its own.</param>
        /// <exception cref="ArgumentNullException"><paramref name="realtimeSeconds"/> is null.</exception>
        public ClientCityStore(Func<double> realtimeSeconds)
        {
            _realtimeSeconds = realtimeSeconds ?? throw new ArgumentNullException(nameof(realtimeSeconds));
        }

        /// <summary>Raised after <see cref="Snapshot"/> changed.</summary>
        public event Action? SnapshotChanged;

        /// <summary>The last snapshot, or null before the first one.</summary>
        public CitySnapshot? Snapshot { get; private set; }

        /// <summary>The real-time clock when <see cref="Snapshot"/> arrived, in seconds; 0 before the first one.</summary>
        public double ReceivedAtSeconds { get; private set; }

        /// <summary>The whole seconds since <see cref="Snapshot"/> arrived; 0 before the first one and never below 0.</summary>
        public long WholeSecondsSinceReceived
        {
            get
            {
                return Snapshot == null ? 0 : Math.Max(0, (long)Math.Floor(_realtimeSeconds() - ReceivedAtSeconds));
            }
        }

        /// <summary>Takes <paramref name="snapshot"/> unless the one held is as new or newer, and raises <see cref="SnapshotChanged"/> when it does.</summary>
        /// <param name="snapshot">The server's snapshot.</param>
        /// <returns>True when the store took it.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
        public bool Apply(CitySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (Snapshot != null && !snapshot.IsNewerThan(Snapshot))
            {
                return false;
            }

            Take(snapshot);
            return true;
        }

        /// <summary>
        /// Takes <paramref name="snapshot"/> whatever the one held is, and raises <see cref="SnapshotChanged"/>: for the answer right after a new login,
        /// which may be another city than the one held, whose version says nothing about this one's (a reset development database starts over).
        /// </summary>
        /// <param name="snapshot">The server's snapshot.</param>
        /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
        public void Replace(CitySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            Take(snapshot);
        }

        /// <summary>Holds <paramref name="snapshot"/> from now on and says so.</summary>
        private void Take(CitySnapshot snapshot)
        {
            Snapshot = snapshot;
            ReceivedAtSeconds = _realtimeSeconds();
            SnapshotChanged?.Invoke();
        }
    }
}
