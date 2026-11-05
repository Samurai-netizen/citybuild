#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Settlement;

namespace CityBuilder.Client.Core
{
    /// <summary>
    /// What the settlement of one request did, in the engine's terms (<c>core-instructions/05-api-contract.md</c>, "Snapshot", <c>settlement</c>):
    /// the time it covered, what was produced and consumed per item, the couriers' stamina spent and who got tired, and what completed. It rides on the <see cref="CitySnapshot"/> of the same
    /// answer, so the welcome-back report (M0-53) reads it from there. Immutable; Core cannot name the DTOs (<c>03</c>), so the network layer
    /// builds it. The lists keep the order the server sent, which is sorted.
    /// </summary>
    public sealed class SettlementSummary
    {
        /// <summary>Creates the summary and copies the lists, so the caller's lists can change afterwards.</summary>
        /// <param name="elapsedSeconds">The seconds the city clock moved.</param>
        /// <param name="produced">What the cycles that completed made, per item; only items above 0.</param>
        /// <param name="consumed">What the cycles that started took, per item; only items above 0.</param>
        /// <param name="courierSeconds">The most stamina one courier spent, in seconds; 0 while the player was present.</param>
        /// <param name="tiredCouriers">The couriers whose stamina ran out, by station, then slot.</param>
        /// <param name="completedBuildings">The ids of the buildings whose construction or upgrade completed.</param>
        /// <param name="completedResearch">The research that completed.</param>
        /// <param name="eraChanged">Whether the city entered a new era.</param>
        /// <exception cref="ArgumentNullException">A list is null.</exception>
        /// <exception cref="ArgumentException">A number is below 0: no settlement does that.</exception>
        public SettlementSummary(
            long elapsedSeconds,
            IReadOnlyList<KeyValuePair<ItemId, long>> produced,
            IReadOnlyList<KeyValuePair<ItemId, long>> consumed,
            long courierSeconds,
            IReadOnlyList<TiredCourier> tiredCouriers,
            IReadOnlyList<int> completedBuildings,
            IReadOnlyList<ResearchId> completedResearch,
            bool eraChanged)
        {
            if (elapsedSeconds < 0 || courierSeconds < 0)
            {
                throw new ArgumentException("A settlement covers 0 or more seconds and spends 0 or more courier seconds.");
            }

            ElapsedSeconds = elapsedSeconds;
            Produced = CopyQuantities(produced ?? throw new ArgumentNullException(nameof(produced)));
            Consumed = CopyQuantities(consumed ?? throw new ArgumentNullException(nameof(consumed)));
            CourierSeconds = courierSeconds;
            TiredCouriers = Copy(tiredCouriers ?? throw new ArgumentNullException(nameof(tiredCouriers)));
            CompletedBuildings = Copy(completedBuildings ?? throw new ArgumentNullException(nameof(completedBuildings)));
            CompletedResearch = Copy(completedResearch ?? throw new ArgumentNullException(nameof(completedResearch)));
            EraChanged = eraChanged;
        }

        /// <summary>The seconds the city clock moved.</summary>
        public long ElapsedSeconds { get; }

        /// <summary>What the cycles that completed made, per item.</summary>
        public IReadOnlyList<KeyValuePair<ItemId, long>> Produced { get; }

        /// <summary>What the cycles that started took, per item.</summary>
        public IReadOnlyList<KeyValuePair<ItemId, long>> Consumed { get; }

        /// <summary>The most stamina one courier spent, in seconds (<c>04</c> section 11); 0 while the player was present.</summary>
        public long CourierSeconds { get; }

        /// <summary>The couriers whose stamina ran out, by station, then slot, each with the city second it did.</summary>
        public IReadOnlyList<TiredCourier> TiredCouriers { get; }

        /// <summary>The ids of the buildings whose construction or upgrade completed.</summary>
        public IReadOnlyList<int> CompletedBuildings { get; }

        /// <summary>The research that completed.</summary>
        public IReadOnlyList<ResearchId> CompletedResearch { get; }

        /// <summary>Whether the city entered a new era.</summary>
        public bool EraChanged { get; }

        /// <summary>A copy of the list as an array, so the summary stays as it was made.</summary>
        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            var copy = new T[source.Count];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = source[index];
            }

            return copy;
        }

        /// <summary>A copy of the quantities; a quantity below 0 is no settlement.</summary>
        /// <exception cref="ArgumentException">A quantity is below 0.</exception>
        private static KeyValuePair<ItemId, long>[] CopyQuantities(IReadOnlyList<KeyValuePair<ItemId, long>> source)
        {
            KeyValuePair<ItemId, long>[] copy = Copy(source);
            foreach (KeyValuePair<ItemId, long> line in copy)
            {
                if (line.Value < 0)
                {
                    throw new ArgumentException("A settlement quantity is below 0: " + line.Key + ".");
                }
            }

            return copy;
        }
    }
}
