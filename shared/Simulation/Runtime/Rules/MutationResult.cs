#nullable enable

namespace CityBuilder.Simulation.Rules
{
    /// <summary>
    /// The outcome of a mutation: <see cref="Ok"/>, or rejected with an error code. A rejected mutation changed nothing. Business-rule failures
    /// are results, never exceptions (<c>core-instructions/14-coding-standards.md</c>). Immutable.
    /// </summary>
    public sealed class MutationResult
    {
        private MutationResult(MutationErrorCode? errorCode)
        {
            ErrorCode = errorCode;
        }

        /// <summary>The mutation was applied.</summary>
        public static MutationResult Ok { get; } = new MutationResult(null);

        /// <summary>Whether the mutation was applied.</summary>
        public bool IsOk => ErrorCode is null;

        /// <summary>Why the mutation was rejected; null when it was applied.</summary>
        public MutationErrorCode? ErrorCode { get; }

        /// <summary>A rejection with the given code.</summary>
        /// <param name="errorCode">Why the mutation was rejected.</param>
        /// <returns>The rejected result.</returns>
        public static MutationResult Rejected(MutationErrorCode errorCode) => new MutationResult(errorCode);

        /// <inheritdoc />
        public override string ToString() => ErrorCode is null ? "Ok" : "Rejected: " + ErrorCode.Value;
    }
}
