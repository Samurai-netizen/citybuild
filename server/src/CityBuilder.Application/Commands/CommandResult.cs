using CityBuilder.Simulation.Rules;

namespace CityBuilder.Application.Commands;

/// <summary>
/// What <see cref="CommandPipeline"/> did with a command: the outcome and what goes with it. A success or a replay carries the response JSON
/// (the same text both times, from the receipt on a replay); a rejection its engine error code; an invalid argument what is wrong. The
/// endpoint maps it to the HTTP response (<c>core-instructions/05-api-contract.md</c>). Immutable.
/// </summary>
public sealed class CommandResult
{
    /// <summary>Holds one outcome; the static members make sure each carries only its own data.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <param name="responseJson">The response, for a success or a replay.</param>
    /// <param name="errorCode">The engine's code, for a rejection.</param>
    /// <param name="detail">What is wrong, for an invalid argument.</param>
    private CommandResult(CommandOutcome outcome, string? responseJson, MutationErrorCode? errorCode, string? detail)
    {
        Outcome = outcome;
        ResponseJson = responseJson;
        ErrorCode = errorCode;
        Detail = detail;
    }

    /// <summary>How the command ended.</summary>
    public CommandOutcome Outcome { get; }

    /// <summary>The response body for <see cref="CommandOutcome.Succeeded"/> and <see cref="CommandOutcome.Replayed"/>; otherwise null.</summary>
    public string? ResponseJson { get; }

    /// <summary>Why the engine refused the command, for <see cref="CommandOutcome.Rejected"/>; otherwise null.</summary>
    public MutationErrorCode? ErrorCode { get; }

    /// <summary>What is wrong with the shape, for <see cref="CommandOutcome.InvalidArgument"/>; otherwise null.</summary>
    public string? Detail { get; }

    /// <summary>The command was applied now; <paramref name="responseJson"/> is stored in its receipt.</summary>
    /// <param name="responseJson">The response; not blank.</param>
    /// <returns>The result.</returns>
    public static CommandResult Succeeded(string responseJson) => new(CommandOutcome.Succeeded, Required(responseJson), null, null);

    /// <summary>The command succeeded before; <paramref name="responseJson"/> is its stored response.</summary>
    /// <param name="responseJson">The stored response; not blank.</param>
    /// <returns>The result.</returns>
    public static CommandResult Replayed(string responseJson) => new(CommandOutcome.Replayed, Required(responseJson), null, null);

    /// <summary>A business rule refused the command.</summary>
    /// <param name="errorCode">The engine's code.</param>
    /// <returns>The result.</returns>
    public static CommandResult Rejected(MutationErrorCode errorCode) => new(CommandOutcome.Rejected, null, errorCode, null);

    /// <summary>The command's shape is wrong.</summary>
    /// <param name="detail">What is wrong; not blank.</param>
    /// <returns>The result.</returns>
    public static CommandResult InvalidArgument(string detail) => new(CommandOutcome.InvalidArgument, null, null, Required(detail));

    /// <summary>The request id was used before with another payload.</summary>
    /// <returns>The result.</returns>
    public static CommandResult IdempotencyConflict() => new(CommandOutcome.IdempotencyConflict, null, null, null);

    /// <inheritdoc />
    public override string ToString() => Outcome + (ErrorCode is { } code ? ": " + code : Detail is null ? string.Empty : ": " + Detail);

    /// <summary>Returns <paramref name="text"/>, refusing a blank one.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentException">The text is null or blank.</exception>
    private static string Required(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return text;
    }
}
