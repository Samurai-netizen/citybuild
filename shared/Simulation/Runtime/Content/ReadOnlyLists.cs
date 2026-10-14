#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace CityBuilder.Simulation.Content
{
    /// <summary>Builds the read-only lists that content definitions expose. A caller can't cast them back to an array and change them.</summary>
    internal static class ReadOnlyLists
    {
        /// <summary>A read-only copy of the elements, in their order.</summary>
        public static IReadOnlyList<T> From<T>(IEnumerable<T> elements) => Array.AsReadOnly(elements.ToArray());

        /// <summary>
        /// A read-only copy of the elements, sorted with the comparison. The sort is not stable, so elements that compare equal must be equal
        /// values (two equal ids) or impossible in valid content (two eras with one number), which keeps the order fully defined.
        /// </summary>
        public static IReadOnlyList<T> Sorted<T>(IEnumerable<T> elements, Comparison<T> comparison)
        {
            T[] copy = elements.ToArray();
            Array.Sort(copy, comparison);
            return Array.AsReadOnly(copy);
        }
    }
}
