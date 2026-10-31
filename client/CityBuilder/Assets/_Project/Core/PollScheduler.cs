#nullable enable

using System;

namespace CityBuilder.Client.Core
{
    /// <summary>
    /// Decides when the client asks the server for the city again (M0-41, <c>09</c> Runtime structure): once every
    /// <see cref="DefaultIntervalSeconds"/> while the app is in the foreground, at once when it comes back from the background or when
    /// something asks for it, never while paused, and never two at a time. It only answers "is a poll due now?"; whoever polls calls
    /// <see cref="TryBegin"/> and <see cref="End"/>. It reads a clock it is given, so a test moves time by hand.
    /// </summary>
    public sealed class PollScheduler
    {
        /// <summary>The seconds between two polls in the foreground (<c>09</c>).</summary>
        public const double DefaultIntervalSeconds = 120;

        /// <summary>Reads a clock in seconds that only moves forward.</summary>
        private readonly Func<double> _realtimeSeconds;

        /// <summary>The seconds that must pass after a poll before the next one is due.</summary>
        private readonly double _intervalSeconds;

        /// <summary>When the last poll ended, or when the city was last fetched elsewhere.</summary>
        private double _lastPolledAt;

        /// <summary>Whether someone asked for a poll that has not started yet.</summary>
        private bool _requested;

        /// <summary>Whether the app is in the background.</summary>
        private bool _paused;

        /// <summary>Whether a poll has begun and not ended.</summary>
        private bool _polling;

        /// <summary>
        /// Creates a scheduler that counts from now: the city was fetched a moment ago (the start flow's <c>GET /city</c> is the poll on start),
        /// so the first poll is due one interval later.
        /// </summary>
        /// <param name="realtimeSeconds">Reads a clock in seconds that only moves forward and keeps running while the app is paused; a test passes its own.</param>
        /// <param name="intervalSeconds">The seconds between two polls; above 0.</param>
        /// <exception cref="ArgumentNullException"><paramref name="realtimeSeconds"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="intervalSeconds"/> is not above 0.</exception>
        public PollScheduler(Func<double> realtimeSeconds, double intervalSeconds)
        {
            _realtimeSeconds = realtimeSeconds ?? throw new ArgumentNullException(nameof(realtimeSeconds));
            if (!(intervalSeconds > 0))
            {
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "The interval must be above 0.");
            }

            _intervalSeconds = intervalSeconds;
            _lastPolledAt = _realtimeSeconds();
        }

        /// <summary>Whether the app is in the background, where nothing is due.</summary>
        public bool IsPaused => _paused;

        /// <summary>Whether a poll has begun and not ended.</summary>
        public bool IsPolling => _polling;

        /// <summary>
        /// Whether a poll should start now: the app is in the foreground, no poll is running, and either something asked for one or a whole
        /// interval has passed since the last.
        /// </summary>
        public bool IsDue
        {
            get
            {
                if (_paused || _polling)
                {
                    return false;
                }

                return _requested || _realtimeSeconds() - _lastPolledAt >= _intervalSeconds;
            }
        }

        /// <summary>The app went to the background: nothing is due until <see cref="Resume"/>. A poll already running is left to finish.</summary>
        public void Pause()
        {
            _paused = true;
        }

        /// <summary>The app came back to the foreground: a poll is due at once, however recent the last was. Does nothing if the app was not paused.</summary>
        public void Resume()
        {
            if (!_paused)
            {
                return;
            }

            _paused = false;
            _requested = true;
        }

        /// <summary>Asks for a poll as soon as none is running and the app is in the foreground (after a reconnect, or when a login was refused).</summary>
        public void RequestPoll()
        {
            _requested = true;
        }

        /// <summary>Tells the scheduler the city was just fetched by someone else, so the interval starts again from now.</summary>
        public void MarkFetched()
        {
            _lastPolledAt = _realtimeSeconds();
        }

        /// <summary>Starts a poll if one is due. A request made while it runs stays, so another poll follows it.</summary>
        /// <returns>True when the caller must now poll and call <see cref="End"/>; false when nothing is due.</returns>
        public bool TryBegin()
        {
            if (!IsDue)
            {
                return false;
            }

            _polling = true;
            _requested = false;
            return true;
        }

        /// <summary>The poll that <see cref="TryBegin"/> started has ended, whatever its result: the interval counts from now.</summary>
        public void End()
        {
            _polling = false;
            _lastPolledAt = _realtimeSeconds();
        }
    }
}
