#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Simulation.Rules
{
    /// <summary>
    /// The outcome of a mutation: <see cref="Ok"/>, or rejected with an error code. A rejected mutation changed nothing. Business-rule failures
    /// are results, never exceptions (<c>core-instructions/14-coding-standards.md</c>). An applied mutation names the items it took from the
    /// stock and gave back, so the caller can write them to the ledger (<c>core-instructions/06-data-model.md</c>). Immutable.
    /// </summary>
    public sealed class MutationResult
    {
        /// <summary>The one shared empty item list of every result that spent or refunded nothing, so a rejection allocates nothing.</summary>
        private static readonly IReadOnlyList<ItemQuantity> _nothing = Array.AsReadOnly(Array.Empty<ItemQuantity>());

        /// <summary>Holds one outcome; the static members make sure a rejection carries no items.</summary>
        private MutationResult(MutationErrorCode? errorCode, IReadOnlyList<ItemQuantity> spent, IReadOnlyList<ItemQuantity> refunded)
        {
            ErrorCode = errorCode;
            Spent = spent;
            Refunded = refunded;
        }

        /// <summary>The mutation was applied and moved no items.</summary>
        public static MutationResult Ok { get; } = new MutationResult(null, _nothing, _nothing);

        /// <summary>Whether the mutation was applied.</summary>
        public bool IsOk => ErrorCode is null;

        /// <summary>Why the mutation was rejected; null when it was applied.</summary>
        public MutationErrorCode? ErrorCode { get; }

        /// <summary>
        /// The items the mutation took from the stock: the cost it paid, line by line as the content lists it (a line may be 0). Empty when it
        /// paid nothing or was rejected.
        /// </summary>
        public IReadOnlyList<ItemQuantity> Spent { get; }

        /// <summary>
        /// The items the mutation gave back to the stock, even above a storage cap: the inputs of a cycle an upgrade interrupted. Empty when it
        /// gave nothing back or was rejected.
        /// </summary>
        public IReadOnlyList<ItemQuantity> Refunded { get; }

        /// <summary>A rejection with the given code.</summary>
        /// <param name="errorCode">Why the mutation was rejected.</param>
        /// <returns>The rejected result.</returns>
        public static MutationResult Rejected(MutationErrorCode errorCode) => new MutationResult(errorCode, _nothing, _nothing);

        /// <summary>An applied mutation that paid <paramref name="spent"/> and gave back <paramref name="refunded"/> (content lists, immutable).</summary>
        internal static MutationResult Applied(IReadOnlyList<ItemQuantity> spent, IReadOnlyList<ItemQuantity>? refunded = null) =>
            new MutationResult(null, spent, refunded ?? _nothing);

        /// <inheritdoc />
        public override string ToString() => ErrorCode is null ? "Ok" : "Rejected: " + ErrorCode.Value;
    }
}
