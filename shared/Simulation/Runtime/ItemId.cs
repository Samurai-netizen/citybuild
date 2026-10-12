#nullable enable
using System;

namespace CityBuilder.Simulation
{
    /// <summary>
    /// The identity of an item in the game content, such as <c>coins</c> or <c>logs</c>. Two ids are equal when their text matches
    /// character by character, and they sort by the same ordinal order, so neither depends on the culture of the machine
    /// (<c>core-instructions/04-simulation-model.md</c>, section 9). <c>default(ItemId)</c> is the empty id: it equals only itself
    /// and sorts before every real id.
    /// </summary>
    public readonly struct ItemId : IEquatable<ItemId>, IComparable<ItemId>
    {
        private readonly string? _value;

        /// <summary>Creates the id with the given text. The content loader checks ids and reports bad ones before it creates them.</summary>
        /// <param name="value">The id text, for example <c>planks</c>. It must not be null or empty.</param>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="value"/> is empty.</exception>
        public ItemId(string value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value.Length == 0)
            {
                throw new ArgumentException("An item id must not be empty.", nameof(value));
            }

            _value = value;
        }

        /// <summary>The id text. It is empty for <c>default(ItemId)</c>.</summary>
        public string Value => _value ?? string.Empty;

        /// <summary>Whether the other id has exactly the same text (ordinal comparison).</summary>
        /// <param name="other">The id to compare with.</param>
        /// <returns>True when the texts are equal character by character.</returns>
        public bool Equals(ItemId other) => string.Equals(_value, other._value, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is ItemId other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => _value is null ? 0 : _value.GetHashCode();

        /// <summary>Compares the text of the two ids in ordinal order.</summary>
        /// <param name="other">The id to compare with.</param>
        /// <returns>
        /// A negative number when this id sorts before <paramref name="other"/>, zero when they are equal, and a positive number when
        /// it sorts after.
        /// </returns>
        public int CompareTo(ItemId other) => string.CompareOrdinal(_value, other._value);

        /// <inheritdoc />
        public override string ToString() => Value;

        /// <summary>Whether two ids have the same text.</summary>
        /// <param name="left">The first id.</param>
        /// <param name="right">The second id.</param>
        /// <returns>True when the texts are equal.</returns>
        public static bool operator ==(ItemId left, ItemId right) => left.Equals(right);

        /// <summary>Whether two ids have different text.</summary>
        /// <param name="left">The first id.</param>
        /// <param name="right">The second id.</param>
        /// <returns>True when the texts differ.</returns>
        public static bool operator !=(ItemId left, ItemId right) => !left.Equals(right);

        /// <summary>Whether the first id sorts before the second in ordinal order.</summary>
        /// <param name="left">The first id.</param>
        /// <param name="right">The second id.</param>
        /// <returns>True when <paramref name="left"/> sorts before <paramref name="right"/>.</returns>
        public static bool operator <(ItemId left, ItemId right) => left.CompareTo(right) < 0;

        /// <summary>Whether the first id sorts before the second or is equal to it.</summary>
        /// <param name="left">The first id.</param>
        /// <param name="right">The second id.</param>
        /// <returns>True when <paramref name="left"/> does not sort after <paramref name="right"/>.</returns>
        public static bool operator <=(ItemId left, ItemId right) => left.CompareTo(right) <= 0;

        /// <summary>Whether the first id sorts after the second in ordinal order.</summary>
        /// <param name="left">The first id.</param>
        /// <param name="right">The second id.</param>
        /// <returns>True when <paramref name="left"/> sorts after <paramref name="right"/>.</returns>
        public static bool operator >(ItemId left, ItemId right) => left.CompareTo(right) > 0;

        /// <summary>Whether the first id sorts after the second or is equal to it.</summary>
        /// <param name="left">The first id.</param>
        /// <param name="right">The second id.</param>
        /// <returns>True when <paramref name="left"/> does not sort before <paramref name="right"/>.</returns>
        public static bool operator >=(ItemId left, ItemId right) => left.CompareTo(right) >= 0;
    }
}
