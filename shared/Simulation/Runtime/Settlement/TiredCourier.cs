#nullable enable

namespace CityBuilder.Simulation.Settlement
{
    /// <summary>
    /// A courier whose stamina ran out during an advance (<c>core-instructions/04-simulation-model.md</c>, sections 2 and 11): it did that
    /// stop's actions, then walked home to rest. In <see cref="SettlementReport.TiredCouriers"/>.
    /// </summary>
    public sealed class TiredCourier
    {
        /// <summary>Creates the entry.</summary>
        /// <param name="stationId">The courier station's building id.</param>
        /// <param name="slot">The courier's slot.</param>
        /// <param name="at">The city-clock second its stamina reached 0: the arrival that spent the last of it.</param>
        public TiredCourier(int stationId, int slot, long at)
        {
            StationId = stationId;
            Slot = slot;
            At = at;
        }

        /// <summary>The courier station's building id.</summary>
        public int StationId { get; }

        /// <summary>The courier's slot in its station.</summary>
        public int Slot { get; }

        /// <summary>The city-clock second its stamina reached 0.</summary>
        public long At { get; }
    }
}
