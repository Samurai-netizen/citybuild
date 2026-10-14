#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// Reads single values of content data and reports shape errors: missing values, bad ids, numbers above
    /// <see cref="GameContentFactory.MaxQuantity"/>. A value that has an error comes back as 0, empty or null; the factory stops after the
    /// shape phase when there are errors, so these stand-ins never reach a <see cref="GameContent"/>.
    /// </summary>
    internal sealed class FieldReader
    {
        private readonly ContentErrorList _errors;

        public FieldReader(ContentErrorList errors)
        {
            _errors = errors;
        }

        public string RequireText(string? value, string path)
        {
            if (value is null || value.Trim().Length == 0)
            {
                _errors.Add(ContentErrorCode.MissingValue, path, "A text is required here.");
                return string.Empty;
            }

            return value;
        }

        public int RequireInt(int? value, string path)
        {
            if (value is null)
            {
                Missing(path);
                return 0;
            }

            return value.Value;
        }

        public long RequireLong(long? value, string path)
        {
            if (value is null)
            {
                Missing(path);
                return 0;
            }

            return CheckLimit(value.Value, path);
        }

        public bool RequireBool(bool? value, string path)
        {
            if (value is null)
            {
                Missing(path);
                return false;
            }

            return value.Value;
        }

        /// <summary>Reads an id; null when the text is missing or has the wrong format (both reported).</summary>
        public TId? RequireId<TId>(string? text, string path, Func<string, TId> create)
            where TId : struct
        {
            if (text is null)
            {
                Missing(path);
                return null;
            }

            if (!ContentIds.IsValid(text))
            {
                _errors.Add(ContentErrorCode.InvalidId, path, $"'{text}' is not a valid id: {ContentIds.FormatDescription}.");
                return null;
            }

            return create(text);
        }

        /// <summary>
        /// Reads a map from item id to amount (a cost, recipe side, cap list or inventory), sorted by item id. Keys are visited in ordinal
        /// order, so errors come in a fixed order whatever order the dictionary holds them in.
        /// </summary>
        public IReadOnlyList<ItemQuantity> ReadItemAmounts(Dictionary<string, long?>? map, string path, bool required)
        {
            if (map is null)
            {
                if (required)
                {
                    _errors.Add(ContentErrorCode.MissingValue, path, "A map of item ids to amounts is required here (it may be empty).");
                }

                return ReadOnlyLists.From(Array.Empty<ItemQuantity>());
            }

            var amounts = new List<ItemQuantity>();
            foreach (string key in map.Keys.OrderBy(key => key, StringComparer.Ordinal))
            {
                string entryPath = path + "." + key;
                ItemId? item = RequireId(key, entryPath, text => new ItemId(text));
                long quantity = RequireLong(map[key], entryPath);
                if (item.HasValue)
                {
                    amounts.Add(new ItemQuantity(item.Value, quantity));
                }
            }

            return ReadOnlyLists.From(amounts);
        }

        /// <summary>
        /// Reads every element of a list with <paramref name="read"/>. A null element is reported; an element is named in paths by its
        /// <paramref name="name"/> when that gives a text, otherwise by its position (<c>items[#2]</c>).
        /// </summary>
        public List<TDefinition> ReadList<TData, TDefinition>(
            List<TData?>? list, string path, bool required, Func<TData, string?> name, Func<TData, string, TDefinition?> read)
            where TData : class
            where TDefinition : class
        {
            var definitions = new List<TDefinition>();
            if (list is null)
            {
                if (required)
                {
                    _errors.Add(ContentErrorCode.MissingValue, path, "A list is required here (it may be empty).");
                }

                return definitions;
            }

            for (int index = 0; index < list.Count; index++)
            {
                TData? element = list[index];
                if (element is null)
                {
                    Missing(path + "[#" + index + "]");
                    continue;
                }

                string? elementName = name(element);
                string elementPath = path + "[" + (string.IsNullOrEmpty(elementName) ? "#" + index : elementName) + "]";
                TDefinition? definition = read(element, elementPath);
                if (definition is not null)
                {
                    definitions.Add(definition);
                }
            }

            return definitions;
        }

        /// <summary>Reports a value that must not be given here.</summary>
        public void Unexpected(bool isGiven, string path, string reason)
        {
            if (isGiven)
            {
                _errors.Add(ContentErrorCode.UnexpectedValue, path, reason);
            }
        }

        /// <summary>Reports a text that is not one of the allowed values.</summary>
        public void InvalidValue(string path, string message) => _errors.Add(ContentErrorCode.InvalidValue, path, message);

        public void Missing(string path) => _errors.Add(ContentErrorCode.MissingValue, path, "A value is required here.");

        private long CheckLimit(long value, string path)
        {
            if (value > GameContentFactory.MaxQuantity)
            {
                _errors.Add(ContentErrorCode.ValueTooLarge, path, $"Must be at most {GameContentFactory.MaxQuantity}, but is {value}.");
            }

            return value;
        }
    }
}
