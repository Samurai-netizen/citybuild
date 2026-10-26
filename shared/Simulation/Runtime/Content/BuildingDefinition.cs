#nullable enable
using System;
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content
{
    /// <summary>A building type of the game content: what placing it costs, how it is unlocked, and its levels.</summary>
    public sealed class BuildingDefinition
    {
        /// <summary>
        /// Creates the definition from values the shape phase has read (<see cref="ContentReader"/>); it becomes part of a
        /// <see cref="GameContent"/> only after every content rule passed. Each value is described on its property.
        /// </summary>
        internal BuildingDefinition(
            BuildingTypeId id,
            string name,
            BuildingKind kind,
            int era,
            IReadOnlyList<ItemQuantity> cost,
            long buildSeconds,
            bool isAvailableAtStart,
            ResearchId? unlockedByResearch,
            IReadOnlyList<BuildingLevelDefinition> levels)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Kind = kind;
            Era = era;
            Cost = cost ?? throw new ArgumentNullException(nameof(cost));
            BuildSeconds = buildSeconds;
            IsAvailableAtStart = isAvailableAtStart;
            UnlockedByResearch = unlockedByResearch;
            Levels = levels ?? throw new ArgumentNullException(nameof(levels));
        }

        /// <summary>The building type's id.</summary>
        public BuildingTypeId Id { get; }

        /// <summary>The display name, for example <c>Sawmill</c>.</summary>
        public string Name { get; }

        /// <summary>Whether it is a house (provides workers) or a producer (runs a recipe).</summary>
        public BuildingKind Kind { get; }

        /// <summary>The number of the era the building type belongs to.</summary>
        public int Era { get; }

        /// <summary>What placing the building costs, sorted by item id. May be empty.</summary>
        public IReadOnlyList<ItemQuantity> Cost { get; }

        /// <summary>How long construction takes, in seconds (greater than zero).</summary>
        public long BuildSeconds { get; }

        /// <summary>Whether the type can be placed from the start; otherwise <see cref="UnlockedByResearch"/> names the research that unlocks it.</summary>
        public bool IsAvailableAtStart { get; }

        /// <summary>
        /// The one research node whose <see cref="UnlockBuildingEffect"/> unlocks this type, or null when it is available at the start. The
        /// unlock is stated only in the research effect; this is filled from it, for the build menu ("needs Trade Charter").
        /// </summary>
        public ResearchId? UnlockedByResearch { get; }

        /// <summary>The levels in order: <c>Levels[0]</c> is level 1, and the last one is the max level.</summary>
        public IReadOnlyList<BuildingLevelDefinition> Levels { get; }

        /// <summary>The highest level a building of this type can reach.</summary>
        public int MaxLevel => Levels.Count;

        /// <summary>The definition of one level.</summary>
        /// <param name="level">The level number, from 1 to <see cref="MaxLevel"/>.</param>
        /// <returns>The level's definition.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="level"/> is below 1 or above <see cref="MaxLevel"/>. That is a programmer error: a building's level always comes from
        /// this definition, and an upgrade checks <see cref="MaxLevel"/> first.
        /// </exception>
        public BuildingLevelDefinition GetLevel(int level)
        {
            if (level < 1 || level > MaxLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, $"The level must be between 1 and {MaxLevel}.");
            }

            return Levels[level - 1];
        }
    }
}
