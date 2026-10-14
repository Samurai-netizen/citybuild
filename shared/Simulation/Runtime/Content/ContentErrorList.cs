#nullable enable
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content
{
    /// <summary>Collects the errors of one <see cref="GameContentFactory.Create"/> call in the order they are found.</summary>
    internal sealed class ContentErrorList
    {
        private readonly List<ContentError> _errors = new List<ContentError>();

        /// <summary>How many errors were found so far.</summary>
        public int Count => _errors.Count;

        /// <summary>Records an error.</summary>
        public void Add(ContentErrorCode code, string path, string message) => _errors.Add(new ContentError(code, path, message));

        /// <summary>A read-only copy of the errors found so far.</summary>
        public IReadOnlyList<ContentError> ToReadOnlyList() => ReadOnlyLists.From(_errors);
    }
}
