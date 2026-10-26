#nullable enable
using System;

namespace CityBuilder.Simulation
{
    /// <summary>
    /// The text identity of an era in the game content, such as <c>early_medieval</c>: a stable name for labels and art. Everything else
    /// names an era by its number (1 is the first), and eras are ordered by that number only, so this id has equality but no order:
    /// text order would put <c>era_10</c> before <c>era_2</c> (decision 022 in <c>core-instructions/12-decisions.md</c>). Two ids are
    /// equal when their text matches character by character, so equality never depends on the culture of the machine.
    /// <c>default(EraId)</c> is the empty id: it equals only itself.
    /// </summary>
    public readonly struct EraId : IEquatable<EraId>
    {
        /// <summary>The id text; null only in <c>default(EraId)</c>, the empty id, which every member treats as an empty text.</summary>
        private readonly string? _value;

        /// <summary>Creates the id with the given text. The content factory checks the text of every id and reports bad ones before it creates them.</summary>
        /// <param name="value">The id text, for example <c>early_medieval</c>. It must not be null or empty.</param>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="value"/> is empty.</exception>
        public EraId(string value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value.Length == 0)
            {
                throw new ArgumentException("An era id must not be empty.", nameof(value));
            }

            _value = value;
        }

        /// <summary>The id text. It is empty for <c>default(EraId)</c>.</summary>
        public string Value => _value ?? string.Empty;

        /// <summary>Whether the other id has exactly the same text (ordinal comparison).</summary>
        /// <param name="other">The id to compare with.</param>
        /// <returns>True when the texts are equal character by character.</returns>
        public bool Equals(EraId other) => string.Equals(_value, other._value, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is EraId other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => _value is null ? 0 : _value.GetHashCode();

        /// <inheritdoc />
        public override string ToString() => Value;

        /// <summary>Whether two ids have the same text.</summary>
        /// <param name="left">The first id.</param>
        /// <param name="right">The second id.</param>
        /// <returns>True when the texts are equal.</returns>
        public static bool operator ==(EraId left, EraId right) => left.Equals(right);

        /// <summary>Whether two ids have different text.</summary>
        /// <param name="left">The first id.</param>
        /// <param name="right">The second id.</param>
        /// <returns>True when the texts differ.</returns>
        public static bool operator !=(EraId left, EraId right) => !left.Equals(right);
    }
}
