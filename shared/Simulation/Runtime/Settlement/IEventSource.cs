#nullable enable
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Settlement
{
    /// <summary>
    /// One kind of timed event that <see cref="Simulator.Advance"/> processes: the research timer, the building timers, and later the mailbox
    /// deliveries (<c>core-instructions/04-simulation-model.md</c>, section 13). The simulator asks every source for its next due time, moves to
    /// the earliest, and lets each source complete what is due then, in the order of its source list. Sources hold no state.
    /// </summary>
    internal interface IEventSource
    {
        /// <summary>The earliest time at which the source has an event due, or null when it has none.</summary>
        long? NextDueAt(CityState city);

        /// <summary>
        /// Completes every event of this source that is due at <paramref name="time"/>: the clock at the start of the advance (usually nothing
        /// is due then), or the earliest due time of all sources. Afterwards none of its events may be due at or before <paramref name="time"/>,
        /// or the advance would not move on.
        /// </summary>
        void CompleteDue(CityState city, GameContent content, long time, SettlementReportBuilder report);
    }
}
