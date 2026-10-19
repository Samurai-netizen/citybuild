#nullable enable
namespace CityBuilder.Simulation.Settlement
{
    /// <summary>
    /// The time one settlement covers (<c>core-instructions/04-simulation-model.md</c>, section 1): from the server second a city was last
    /// settled to the server second now, both whole seconds since the Unix epoch. The server passes <see cref="ElapsedSeconds"/> to
    /// <see cref="Simulator.Advance"/> and stores <see cref="NewLastSettledUnixSec"/>. Because each window starts where the last one ended,
    /// any number of settlements adds up to the time from the first to the last, whatever the fractions of a second between them.
    /// </summary>
    public readonly struct SettlementWindow
    {
        private SettlementWindow(long elapsedSeconds, long newLastSettledUnixSec, bool isClockBehind)
        {
            ElapsedSeconds = elapsedSeconds;
            NewLastSettledUnixSec = newLastSettledUnixSec;
            IsClockBehind = isClockBehind;
        }

        /// <summary>The seconds to advance the city by: now minus the last settled second, or 0 when the clock is behind. Never negative.</summary>
        public long ElapsedSeconds { get; }

        /// <summary>The last settled second after this settlement: now, or the old value when the clock is behind, so it never decreases.</summary>
        public long NewLastSettledUnixSec { get; }

        /// <summary>
        /// The server clock is behind the last settled second (it was set back). Nothing elapses, and the seconds it repeats are not
        /// counted again; the server logs a warning (<c>04</c> section 1).
        /// </summary>
        public bool IsClockBehind { get; }

        /// <summary>Computes the window from the last settled second to now.</summary>
        /// <param name="lastSettledUnixSec">The server second the city was last settled at.</param>
        /// <param name="nowUnixSec">The server second now, floored to whole seconds.</param>
        /// <returns>The window; with a clock behind the last settled second, nothing elapses and the last settled second stays.</returns>
        /// <exception cref="System.OverflowException">The difference doesn't fit in a <see cref="long"/>.</exception>
        public static SettlementWindow Compute(long lastSettledUnixSec, long nowUnixSec)
        {
            long elapsed = checked(nowUnixSec - lastSettledUnixSec);
            return elapsed < 0
                ? new SettlementWindow(0, lastSettledUnixSec, isClockBehind: true)
                : new SettlementWindow(elapsed, nowUnixSec, isClockBehind: false);
        }
    }
}
