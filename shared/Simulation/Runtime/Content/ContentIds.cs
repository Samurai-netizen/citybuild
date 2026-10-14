#nullable enable

namespace CityBuilder.Simulation.Content
{
    /// <summary>The format of every content id: a lowercase ASCII letter, then lowercase letters, digits or <c>_</c>, at most 64 characters.</summary>
    internal static class ContentIds
    {
        /// <summary>The longest id text allowed. Ids are keys in JSON, URLs and database rows; 64 is plenty and keeps them short.</summary>
        public const int MaxLength = 64;

        /// <summary>The rule in words, for error messages.</summary>
        public const string FormatDescription =
            "an id starts with a lowercase letter and has only lowercase letters, digits and '_', at most 64 characters";

        /// <summary>Whether the text has the id format. Only ASCII counts, so the check is the same on every machine.</summary>
        public static bool IsValid(string text)
        {
            if (text.Length == 0 || text.Length > MaxLength || !IsLowercaseLetter(text[0]))
            {
                return false;
            }

            foreach (char character in text)
            {
                if (!IsLowercaseLetter(character) && !(character >= '0' && character <= '9') && character != '_')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsLowercaseLetter(char character) => character >= 'a' && character <= 'z';
    }
}
