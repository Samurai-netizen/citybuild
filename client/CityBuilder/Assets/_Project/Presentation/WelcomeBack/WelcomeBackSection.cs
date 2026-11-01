#nullable enable

using System;
using System.Collections.Generic;

namespace CityBuilder.Client.Presentation.WelcomeBack
{
    /// <summary>One block of the welcome-back report (M0-53): a heading such as <c>Produced</c> and the lines under it. Immutable.</summary>
    public sealed class WelcomeBackSection
    {
        /// <summary>Makes the section; the lines are copied.</summary>
        /// <param name="heading">The heading.</param>
        /// <param name="lines">The lines, at least one: a section with nothing to say is left out by <see cref="WelcomeBackModel"/>.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="lines"/> is empty.</exception>
        public WelcomeBackSection(string heading, IReadOnlyList<string> lines)
        {
            Heading = heading ?? throw new ArgumentNullException(nameof(heading));
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            if (lines.Count == 0)
            {
                throw new ArgumentException("A section has at least one line.", nameof(lines));
            }

            var copy = new string[lines.Count];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = lines[index];
            }

            Lines = copy;
        }

        /// <summary>The heading, such as <c>Produced</c>.</summary>
        public string Heading { get; }

        /// <summary>The lines under the heading, in the order to show.</summary>
        public IReadOnlyList<string> Lines { get; }
    }
}
