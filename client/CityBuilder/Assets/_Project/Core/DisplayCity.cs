#nullable enable

using System;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using UnityEngine;

namespace CityBuilder.Client.Core
{
    /// <summary>
    /// The city the client shows between snapshots (<c>core-instructions/09-unity-client.md</c>, "Display prediction"): the shared engine run on
    /// a copy of the store's snapshot, up to the <see cref="DisplayClock"/>. Each prediction starts again from the snapshot, so it equals one
    /// <see cref="Simulator.Advance"/> of the whole time, as the server's next settlement will. A new snapshot replaces it at once. It decides
    /// nothing and is never sent anywhere. It lives as long as the store it listens to, so it never unsubscribes.
    /// </summary>
    public sealed class DisplayCity
    {
        /// <summary>The store whose snapshot it predicts from.</summary>
        private readonly ClientCityStore _store;

        /// <summary>The clock it predicts up to.</summary>
        private readonly DisplayClock _clock;

        /// <summary>The content the engine runs with.</summary>
        private readonly GameContent _content;

        /// <summary>The elapsed seconds of <see cref="Current"/>.</summary>
        private long _computedElapsedSeconds;

        /// <summary>Whether the engine refused the current snapshot; it is then shown as it is until the next one arrives.</summary>
        private bool _predictionFailed;

        /// <summary>Creates a display city and computes it when the store already has a snapshot.</summary>
        /// <param name="store">The store whose snapshot it predicts from.</param>
        /// <param name="clock">The clock it predicts up to, over the same store.</param>
        /// <param name="content">The content the engine runs with.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public DisplayCity(ClientCityStore store, DisplayClock clock, GameContent content)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _store.SnapshotChanged += OnSnapshotChanged;
            if (_store.Snapshot != null)
            {
                OnSnapshotChanged();
            }
        }

        /// <summary>Raised after <see cref="Current"/> was replaced.</summary>
        public event Action? Changed;

        /// <summary>The predicted city, or null before the first snapshot. It is replaced, not changed, by the next prediction: read it, don't keep it.</summary>
        public CityState? Current { get; private set; }

        /// <summary>
        /// Predicts again if a whole second has passed since the last prediction, otherwise does nothing, so calling it every frame costs a
        /// clock read and allocates nothing.
        /// </summary>
        /// <returns>True when <see cref="Current"/> was replaced.</returns>
        public bool Refresh()
        {
            if (Current == null || _predictionFailed)
            {
                return false;
            }

            long elapsed = _clock.ElapsedSeconds;
            if (elapsed == _computedElapsedSeconds)
            {
                return false;
            }

            Predict(elapsed);
            return true;
        }

        /// <summary>The store took a new snapshot: the prediction starts over from it, whatever it showed before.</summary>
        private void OnSnapshotChanged()
        {
            _predictionFailed = false;
            Predict(_clock.ElapsedSeconds);
        }

        /// <summary>
        /// Replaces <see cref="Current"/> with a fresh copy of the snapshot advanced by <paramref name="elapsed"/> seconds. When the engine refuses
        /// the city (it names something this content doesn't know, until M0-58 refetches changed content), the display must not take the game
        /// down: it logs the error once and shows the snapshot as it is.
        /// </summary>
        private void Predict(long elapsed)
        {
            CitySnapshot snapshot = _store.Snapshot!;
            CityState city = snapshot.CopyCity();
            try
            {
                Simulator.Advance(city, _content, elapsed);
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException || exception is OverflowException)
            {
                Debug.LogError($"DisplayCity: cannot predict state version {snapshot.StateVersion} (content {snapshot.ContentVersion}): {exception.Message}");
                _predictionFailed = true;
                city = snapshot.CopyCity();
            }

            Current = city;
            _computedElapsedSeconds = elapsed;
            Changed?.Invoke();
        }
    }
}
