#nullable enable

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// What a new city starts with: its era and the coins in its purse. It has no buildings, no goods, no research and a city clock of 0
    /// (CR-0008 D2: goods live in buildings, and there is none yet).
    /// </summary>
    public sealed class StartingCityDefinition
    {
        /// <summary>Creates the starting city from values the shape phase has read (<see cref="ContentReader"/>); each is described on its property.</summary>
        internal StartingCityDefinition(int era, long purse)
        {
            Era = era;
            Purse = purse;
        }

        /// <summary>The number of the era a new city starts in.</summary>
        public int Era { get; }

        /// <summary>The coins a new city has in its purse, 0 or more.</summary>
        public long Purse { get; }
    }
}
