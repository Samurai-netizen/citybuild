#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// The outcome of <see cref="ContentService.LoadAsync"/>, one of three: the validated content, a failed request
    /// (<see cref="ApiError"/>), or an answer that arrived but is not valid content (<see cref="ContentErrors"/>). The third is the one a
    /// player cannot fix by retrying, which is why it is not folded into the second: the client shows a fatal error for it.
    /// </summary>
    public sealed class ContentLoadResult
    {
        /// <summary>Creates a result; use the static members.</summary>
        private ContentLoadResult(GameContent? content, ApiError? apiError, IReadOnlyList<ContentError> contentErrors)
        {
            Content = content;
            ApiError = apiError;
            ContentErrors = contentErrors;
        }

        /// <summary>The validated content, or null unless <see cref="IsSuccess"/>.</summary>
        public GameContent? Content { get; }

        /// <summary>Why the request failed, or null: the answer did not arrive, was refused, or was not the contract's.</summary>
        public ApiError? ApiError { get; }

        /// <summary>Every error the engine's factory found in an answer that did arrive; empty unless <see cref="IsInvalidContent"/>.</summary>
        public IReadOnlyList<ContentError> ContentErrors { get; }

        /// <summary>Whether <see cref="Content"/> is set.</summary>
        public bool IsSuccess => Content != null;

        /// <summary>Whether the answer arrived and decoded but the factory refused it.</summary>
        public bool IsInvalidContent => ContentErrors.Count > 0;

        /// <summary>A valid result.</summary>
        public static ContentLoadResult Success(GameContent content)
        {
            return new ContentLoadResult(content, null, Array.Empty<ContentError>());
        }

        /// <summary>A failed request.</summary>
        public static ContentLoadResult RequestFailed(ApiError error)
        {
            return new ContentLoadResult(null, error, Array.Empty<ContentError>());
        }

        /// <summary>An answer the factory refused; <paramref name="errors"/> is never empty.</summary>
        public static ContentLoadResult Invalid(IReadOnlyList<ContentError> errors)
        {
            return new ContentLoadResult(null, null, errors);
        }
    }
}
