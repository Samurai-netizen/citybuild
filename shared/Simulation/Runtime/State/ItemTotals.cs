#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace CityBuilder.Simulation.State
{
    /// <summary>
    /// Quantities per item, sorted by item id (ordinal) and holding only items above 0: a city's stock and a settlement's totals. Reading and
    /// changing a quantity allocates nothing, so <see cref="Settlement.Simulator.Advance"/> can use it at every event: a
    /// <see cref="SortedDictionary{TKey,TValue}"/> boxes a struct key on every lookup on .NET (build log of M0-12). Enumeration runs in ordinal
    /// item order. Only the engine changes it; everything else reads it as an <see cref="IReadOnlyDictionary{TKey,TValue}"/>.
    /// </summary>
    internal sealed class ItemTotals : IReadOnlyDictionary<ItemId, long>
    {
        /// <summary>The items, sorted by id in ordinal order, so lookups are binary searches; <c>_quantities[i]</c> belongs to <c>_items[i]</c>.</summary>
        private readonly List<ItemId> _items = new List<ItemId>();
        /// <summary>The quantities beside <see cref="_items"/>, every one above 0.</summary>
        private readonly List<long> _quantities = new List<long>();

        /// <inheritdoc/>
        public int Count => _items.Count;

        /// <inheritdoc/>
        public IEnumerable<ItemId> Keys => _items.AsReadOnly();

        /// <inheritdoc/>
        public IEnumerable<long> Values => _quantities.AsReadOnly();

        /// <inheritdoc/>
        public long this[ItemId key] =>
            TryGetValue(key, out long quantity) ? quantity : throw new KeyNotFoundException(FormattableString.Invariant($"Item {key} is not listed."));

        /// <inheritdoc/>
        public bool ContainsKey(ItemId key) => _items.BinarySearch(key) >= 0;

        /// <inheritdoc/>
        public bool TryGetValue(ItemId key, [MaybeNullWhen(false)] out long value)
        {
            int index = _items.BinarySearch(key);
            value = index >= 0 ? _quantities[index] : 0;
            return index >= 0;
        }

        /// <summary>The quantity of an item, 0 when it is not listed.</summary>
        public long Get(ItemId item) => TryGetValue(item, out long quantity) ? quantity : 0;

        /// <summary>Sets the quantity of an item; 0 removes it from the list.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="quantity"/> is below 0.</exception>
        public void Set(ItemId item, long quantity)
        {
            if (quantity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "A quantity must not be below 0.");
            }

            // BinarySearch returns the item's index, or for a missing item the bitwise complement of where it belongs (~index), so an insertion
            // there keeps the list sorted.
            int index = _items.BinarySearch(item);
            if (index < 0)
            {
                if (quantity > 0)
                {
                    _items.Insert(~index, item);
                    _quantities.Insert(~index, quantity);
                }
            }
            else if (quantity > 0)
            {
                _quantities[index] = quantity;
            }
            else
            {
                _items.RemoveAt(index);
                _quantities.RemoveAt(index);
            }
        }

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<ItemId, long>> GetEnumerator()
        {
            for (int index = 0; index < _items.Count; index++)
            {
                yield return new KeyValuePair<ItemId, long>(_items[index], _quantities[index]);
            }
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
