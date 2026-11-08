#nullable enable
using System;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// An amount preset of the content (<c>core-instructions/04-simulation-model.md</c>, section 19): a share of the base that every city may
    /// use for a trip or a route action without research, such as All (100 %), Half (50 %) or Quarter (25 %).
    /// </summary>
    public sealed class AmountPresetDefinition
    {
        /// <summary>Creates the preset as read; the rules check its percentage afterwards.</summary>
        internal AmountPresetDefinition(AmountPresetId id, string name, int percent)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Percent = percent;
        }

        /// <summary>The preset's id; the API's amount string names it (<c>05</c>).</summary>
        public AmountPresetId Id { get; }

        /// <summary>The display name.</summary>
        public string Name { get; }

        /// <summary>The share of the base it gives, 1 to 100 percent.</summary>
        public int Percent { get; }
    }
}
