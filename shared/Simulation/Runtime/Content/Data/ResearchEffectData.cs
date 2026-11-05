#nullable enable

namespace CityBuilder.Simulation.Content.Data
{
    /// <summary>
    /// One effect of a research node as read, before any check (see <see cref="ResearchEffect"/>). <see cref="Type"/> says which of the
    /// other values it needs; any other value given is an error.
    /// </summary>
    public sealed class ResearchEffectData
    {
        /// <summary>
        /// Required: <c>unlockBuilding</c> (needs <see cref="Building"/>), <c>laborReduction</c> (<see cref="Building"/>, <see cref="Permille"/>),
        /// <c>storageCapBonus</c> (<see cref="Item"/>, <see cref="Amount"/>), <c>unlockAmountMode</c> (<see cref="Mode"/>) or
        /// <c>walkSpeedBonus</c> (<see cref="Permille"/>).
        /// </summary>
        public string? Type { get; set; }

        /// <summary>The building type the effect is about.</summary>
        public string? Building { get; set; }

        /// <summary>The item the effect is about.</summary>
        public string? Item { get; set; }

        /// <summary>The labor reduction in permille.</summary>
        public int? Permille { get; set; }

        /// <summary>The storage cap bonus, in items.</summary>
        public long? Amount { get; set; }

        /// <summary>The amount mode a research unlocks, <c>percent</c> or <c>exact</c> (<c>unlockAmountMode</c> only).</summary>
        public string? Mode { get; set; }
    }
}
