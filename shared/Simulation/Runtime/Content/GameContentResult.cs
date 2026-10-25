#nullable enable
using System;
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content
{
    /// <summary>The outcome of <see cref="GameContentFactory.Create"/>: either valid content, or every error that was found, never both.</summary>
    public sealed class GameContentResult
    {
        /// <summary>Holds one outcome; <see cref="Valid"/> and <see cref="Invalid"/> make sure it is content or errors, never both.</summary>
        private GameContentResult(GameContent? content, IReadOnlyList<ContentError> errors)
        {
            Content = content;
            Errors = errors;
        }

        /// <summary>The validated content, or null when the data had errors.</summary>
        public GameContent? Content { get; }

        /// <summary>Every error found, in a fixed order (shape errors, or rule errors rule by rule in file order). Empty when the content is valid.</summary>
        public IReadOnlyList<ContentError> Errors { get; }

        /// <summary>Whether the data was valid, so <see cref="Content"/> is set.</summary>
        public bool IsValid => Content is not null;

        /// <summary>A successful result: the content and an empty error list.</summary>
        internal static GameContentResult Valid(GameContent content) =>
            new GameContentResult(content ?? throw new ArgumentNullException(nameof(content)), Array.AsReadOnly(Array.Empty<ContentError>()));

        /// <summary>A failed result: no content and every error found (at least one).</summary>
        internal static GameContentResult Invalid(IReadOnlyList<ContentError> errors) => new GameContentResult(null, errors);
    }
}
