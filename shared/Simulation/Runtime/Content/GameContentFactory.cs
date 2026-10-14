#nullable enable
using System;
using CityBuilder.Simulation.Content.Data;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// The only way to make a <see cref="GameContent"/>: checks content data and builds the immutable definitions from it. Bad data is
    /// never an exception: the result lists every error instead. The checks run in two phases. The shape phase reports values that are
    /// missing, malformed or not allowed where they are; when it finds any, the factory stops, because the definitions can't be built
    /// reliably. Otherwise the rule phase runs every rule and reports every rule error (<c>core-instructions/04-simulation-model.md</c>,
    /// content rules).
    /// </summary>
    public static class GameContentFactory
    {
        /// <summary>
        /// The largest number content may hold in a quantity, cost, cap, duration or labor value (10^12). Sums of content numbers, such as a
        /// cap plus every bonus or the city clock plus a duration, then stay far below <see cref="long.MaxValue"/>, so the engine's
        /// <c>checked</c> arithmetic never overflows because of content.
        /// </summary>
        public const long MaxQuantity = 1_000_000_000_000;

        /// <summary>Checks the data and builds the content.</summary>
        /// <param name="data">The content as read from a file or a response. It is only read, never changed.</param>
        /// <returns>The content, or every error found.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="data"/> is null (a programmer error, unlike bad values inside it).</exception>
        public static GameContentResult Create(GameContentData data)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var errors = new ContentErrorList();
            ContentDraft? draft = new ContentReader(errors).Read(data);
            if (draft is null)
            {
                return GameContentResult.Invalid(errors.ToReadOnlyList());
            }

            return GameContentResult.Valid(draft.ToContent());
        }
    }
}
