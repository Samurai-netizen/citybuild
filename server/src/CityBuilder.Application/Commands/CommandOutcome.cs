namespace CityBuilder.Application.Commands;

/// <summary>How <see cref="CommandPipeline"/> ended a command (<c>core-instructions/03-architecture.md</c>, request lifecycle).</summary>
public enum CommandOutcome
{
    /// <summary>Applied now: the city, its ledger rows and the receipt are committed; the response is new.</summary>
    Succeeded,

    /// <summary>A retry of a command that succeeded before (same request id, same payload): nothing ran; the response is the stored one.</summary>
    Replayed,

    /// <summary>A business rule refused it (<c>04</c> section 8): only the settlement is committed, no receipt is stored.</summary>
    Rejected,

    /// <summary>Its shape is wrong (<c>INVALID_ARGUMENT</c>): refused before the database was touched.</summary>
    InvalidArgument,

    /// <summary>The request id was used before with another payload (<c>IDEMPOTENCY_CONFLICT</c>): nothing ran or was committed.</summary>
    IdempotencyConflict,
}
