#nullable enable
using System;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// An era of the game (<c>core-instructions/25-eras-and-research.md</c>). Content and city state name an era by its <see cref="Number"/>,
    /// and eras are ordered by it; <see cref="Id"/> is a stable text name for labels and art.
    /// </summary>
    public sealed class EraDefinition
    {
        internal EraDefinition(int number, EraId id, string name)
        {
            Number = number;
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>The era's number: 1 for the first era, and the numbers of all eras are 1, 2, … without gaps.</summary>
        public int Number { get; }

        /// <summary>The era's text id, for example <c>early_medieval</c>.</summary>
        public EraId Id { get; }

        /// <summary>The display name, for example <c>Early Middle Ages</c>.</summary>
        public string Name { get; }
    }
}
