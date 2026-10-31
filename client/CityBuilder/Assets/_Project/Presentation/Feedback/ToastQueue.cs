#nullable enable

using System;
using System.Collections.Generic;

namespace CityBuilder.Client.Presentation.Feedback
{
    /// <summary>
    /// The toasts that are on the screen (M0-47), without a screen: <see cref="Show"/> adds one, <see cref="Update"/> takes away those whose
    /// time is up, and <see cref="Visible"/> lists the rest, oldest first. At most <see cref="MaxVisible"/> show at once (the oldest goes when
    /// a newer one needs the room), and a toast with the same kind and words as one already shown does not stack: it renews that one's time,
    /// so a player who taps a refused button five times sees one toast, not five. The queue keeps no clock: the caller passes the time, in
    /// any milliseconds it counts, so a test needs no waiting.
    /// </summary>
    public sealed class ToastQueue
    {
        /// <summary>How many toasts show at once unless the maker says otherwise: UI layout, not a game number.</summary>
        public const int DefaultMaxVisible = 3;

        /// <summary>How long a success toast stays unless the maker says otherwise, in milliseconds.</summary>
        public const long DefaultSuccessMilliseconds = 2500;

        /// <summary>How long an error toast stays unless the maker says otherwise, in milliseconds; longer, because it has more to read and the player may act on it.</summary>
        public const long DefaultErrorMilliseconds = 5000;

        /// <summary>The toasts shown, oldest first.</summary>
        private readonly List<Toast> _visible = new List<Toast>();

        /// <summary>How long a success toast stays.</summary>
        private readonly long _successMilliseconds;

        /// <summary>How long an error toast stays.</summary>
        private readonly long _errorMilliseconds;

        /// <summary>The id the next new toast gets.</summary>
        private int _nextId = 1;

        /// <summary>Creates an empty queue.</summary>
        /// <param name="maxVisible">How many toasts show at once; at least 1.</param>
        /// <param name="successMilliseconds">How long a success toast stays; above 0.</param>
        /// <param name="errorMilliseconds">How long an error toast stays; above 0.</param>
        /// <exception cref="ArgumentOutOfRangeException">An argument is out of range.</exception>
        public ToastQueue(
            int maxVisible = DefaultMaxVisible,
            long successMilliseconds = DefaultSuccessMilliseconds,
            long errorMilliseconds = DefaultErrorMilliseconds)
        {
            if (maxVisible < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxVisible), "At least one toast must be able to show.");
            }

            if (successMilliseconds <= 0 || errorMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(successMilliseconds), "A toast must stay for some time.");
            }

            MaxVisible = maxVisible;
            _successMilliseconds = successMilliseconds;
            _errorMilliseconds = errorMilliseconds;
        }

        /// <summary>Raised after the list of visible toasts changed (a toast was added, renewed in place does not count, or one expired).</summary>
        public event Action? Changed;

        /// <summary>How many toasts show at once.</summary>
        public int MaxVisible { get; }

        /// <summary>The toasts on the screen, oldest first. The list is the queue's own: read it, don't keep it.</summary>
        public IReadOnlyList<Toast> Visible => _visible;

        /// <summary>
        /// Shows a toast, or renews the time of the one that already says the same. A new toast beyond <see cref="MaxVisible"/> pushes the
        /// oldest out.
        /// </summary>
        /// <param name="kind">What it reports.</param>
        /// <param name="text">The sentence; not empty.</param>
        /// <param name="nowMilliseconds">The time now, in the milliseconds the caller counts.</param>
        /// <returns>The toast that shows the sentence.</returns>
        /// <exception cref="ArgumentException"><paramref name="text"/> is null or empty.</exception>
        public Toast Show(ToastKind kind, string text, long nowMilliseconds)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("A toast needs words.", nameof(text));
            }

            long expiresAt = nowMilliseconds + (kind == ToastKind.Error ? _errorMilliseconds : _successMilliseconds);
            for (int index = 0; index < _visible.Count; index++)
            {
                Toast shown = _visible[index];
                if (shown.Kind == kind && shown.Text == text)
                {
                    shown.ExpiresAtMilliseconds = expiresAt;
                    return shown;
                }
            }

            var toast = new Toast(_nextId++, kind, text, expiresAt);
            _visible.Add(toast);
            while (_visible.Count > MaxVisible)
            {
                _visible.RemoveAt(0);
            }

            Changed?.Invoke();
            return toast;
        }

        /// <summary>Takes away the toasts whose time is up. Raises <see cref="Changed"/> only when one went.</summary>
        /// <param name="nowMilliseconds">The time now, in the same milliseconds as <see cref="Show"/>.</param>
        /// <returns>True when a toast went.</returns>
        public bool Update(long nowMilliseconds)
        {
            // A loop, not RemoveAll with a lambda: the host calls this every frame and a closure would allocate each time.
            bool removed = false;
            for (int index = _visible.Count - 1; index >= 0; index--)
            {
                if (_visible[index].ExpiresAtMilliseconds <= nowMilliseconds)
                {
                    _visible.RemoveAt(index);
                    removed = true;
                }
            }

            if (removed)
            {
                Changed?.Invoke();
            }

            return removed;
        }
    }
}
