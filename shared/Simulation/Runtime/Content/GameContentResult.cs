#nullable enable
using System;
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content
{
    /// <summary>The outcome of <see cref="GameContentFactory.Create"/>: either valid content, or every error that was found, never both.</summary>
    public sealed class GameContentResult
    {
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

        internal static GameContentResult Valid(GameContent content) =>
            new GameContentResult(content ?? throw new ArgumentNullException(nameof(content)), Array.AsReadOnly(Array.Empty<ContentError>()));

        internal static GameContentResult Invalid(IReadOnlyList<ContentError> errors) => new GameContentResult(null, errors);
    }
}
