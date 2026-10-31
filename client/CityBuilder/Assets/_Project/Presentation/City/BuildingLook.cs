#nullable enable

using System;
using System.Collections.Generic;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// What one building type looks like in one era: a name for logs and tests, and the primitives it is built from. Plain data, so
    /// the looks can be tested without a scene (<see cref="BuildingLooks"/> holds them).
    /// </summary>
    public sealed class BuildingLook
    {
        /// <summary>Creates a look.</summary>
        /// <param name="name">A short name, for logs and tests.</param>
        /// <param name="parts">The primitives, at least one.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">The name is empty or there is no part.</exception>
        public BuildingLook(string name, IReadOnlyList<BuildingPart> parts)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Length == 0)
            {
                throw new ArgumentException("A look needs a name.", nameof(name));
            }

            Parts = parts ?? throw new ArgumentNullException(nameof(parts));
            if (parts.Count == 0)
            {
                throw new ArgumentException("A look needs at least one part.", nameof(parts));
            }

            Name = name;
        }

        /// <summary>A short name, for logs and tests.</summary>
        public string Name { get; }

        /// <summary>The primitives the building is built from.</summary>
        public IReadOnlyList<BuildingPart> Parts { get; }
    }
}
