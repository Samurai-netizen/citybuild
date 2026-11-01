#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Simulation;

namespace CityBuilder.Client.Core
{
    /// <summary>
    /// What the settlement of one request did, in the engine's terms (<c>core-instructions/05-api-contract.md</c>, "Snapshot", <c>settlement</c>):
    /// the time it covered, what was produced and consumed per item, and what completed. It rides on the <see cref="CitySnapshot"/> of the same
    /// answer, so the welcome-back report (M0-53) reads it from there. Immutable; Core cannot name the DTOs (<c>03</c>), so the network layer
    /// builds it. The lists keep the order the server sent, which is sorted.
    /// </summary>
    public sealed class SettlementSummary
    {
        /// <summary>Creates the summary and copies the lists, so the caller's lists can change afterwards.</summary>
        /// <param name="elapsedSeconds">The seconds the city clock moved.</param>
        /// <param name="productionSeconds">The seconds of the window in which cycles could start; at most <paramref name="elapsedSeconds"/>.</param>
        /// <param name="produced">What the cycles that completed made, per item; only items above 0.</param>
        /// <param name="consumed">What the cycles that started took, per item; only items above 0.</param>
        /// <param name="completedBuildings">The ids of the buildings whose construction or upgrade completed.</param>
        /// <param name="completedResearch">The research that completed.</param>
        /// <param name="eraChanged">Whether the city entered a new era.</param>
        /// <exception cref="ArgumentNullException">A list is null.</exception>
        /// <exception cref="ArgumentException">A number is below 0, or <paramref name="productionSeconds"/> is above <paramref name="elapsedSeconds"/>: no settlement does that.</exception>
        public SettlementSummary(
            long elapsedSeconds,
            long productionSeconds,
            IReadOnlyList<KeyValuePair<ItemId, long>> produced,
            IReadOnlyList<KeyValuePair<ItemId, long>> consumed,
            IReadOnlyList<int> completedBuildings,
            IReadOnlyList<ResearchId> completedResearch,
            bool eraChanged)
        {
            if (elapsedSeconds < 0 || productionSeconds < 0 || productionSeconds > elapsedSeconds)
            {
                throw new ArgumentException("A settlement covers 0 or more seconds and counts at most those for production.");
            }

            ElapsedSeconds = elapsedSeconds;
            ProductionSeconds = productionSeconds;
            Produced = CopyQuantities(produced ?? throw new ArgumentNullException(nameof(produced)));
            Consumed = CopyQuantities(consumed ?? throw new ArgumentNullException(nameof(consumed)));
            CompletedBuildings = Copy(completedBuildings ?? throw new ArgumentNullException(nameof(completedBuildings)));
            CompletedResearch = Copy(completedResearch ?? throw new ArgumentNullException(nameof(completedResearch)));
            EraChanged = eraChanged;
        }

        /// <summary>The seconds the city clock moved.</summary>
        public long ElapsedSeconds { get; }

        /// <summary>The seconds of the window in which cycles could start; fewer than <see cref="ElapsedSeconds"/> when the offline production cap was reached.</summary>
        public long ProductionSeconds { get; }

        /// <summary>What the cycles that completed made, per item.</summary>
        public IReadOnlyList<KeyValuePair<ItemId, long>> Produced { get; }

        /// <summary>What the cycles that started took, per item.</summary>
        public IReadOnlyList<KeyValuePair<ItemId, long>> Consumed { get; }

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
