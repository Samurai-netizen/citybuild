#nullable enable
using System;

namespace CityBuilder.Simulation.Content
{
    /// <summary>One problem found in content data: what is wrong, where, and a message for the person who edits the content.</summary>
    public sealed class ContentError
    {
        internal ContentError(ContentErrorCode code, string path, string message)
        {
            Code = code;
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        /// <summary>What kind of problem it is. Tests and tools match on this; it never changes for the same problem.</summary>
        public ContentErrorCode Code { get; }

        /// <summary>
        /// Where the problem is, in the names of the JSON fields: for example <c>buildings[sawmill].levels[2].recipe.labor</c> or
        /// <c>research[frame_saw].cost.planks</c>. A list element is named by its id (by its number for levels), or by its position when it has
        /// none (<c>effects[#0]</c>, counting from 0); a map entry is named by its key.
        /// </summary>
        public string Path { get; }

        /// <summary>What is wrong and what is expected, in English. The wording may change; match on <see cref="Code"/> instead.</summary>
        public string Message { get; }

        /// <summary>The error on one line, for example <c>NotPositive at buildings[sawmill].buildSeconds: must be greater than 0, but is 0.</c></summary>
        /// <returns>The code, the path and the message.</returns>
        public override string ToString() => Code + " at " + Path + ": " + Message;
    }
}
