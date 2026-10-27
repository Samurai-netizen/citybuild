namespace CityBuilder.Infrastructure.Persistence.Rows;

/// <summary>
/// A row of <c>command_receipts</c> (core-instructions/06-data-model.md): the stored answer to one successful command, so a retry with
/// the same <c>requestId</c> gets the same response and never applies twice (core-instructions/05-api-contract.md, idempotency).
/// </summary>
public sealed class CommandReceiptRow
{
    /// <summary><c>player_id</c>: the player (<see cref="PlayerRow"/>); with <see cref="RequestId"/> the primary key.</summary>
    public Guid PlayerId { get; set; }

    /// <summary><c>request_id</c>: the client-generated UUID of the command.</summary>
    public Guid RequestId { get; set; }

    /// <summary><c>command_type</c>: which command it was.</summary>
    public string CommandType { get; set; } = string.Empty;

    /// <summary><c>payload_hash</c>: the SHA-256 hash of the command's payload; a retry with another payload is a conflict.</summary>
    public byte[] PayloadHash { get; set; } = [];

    /// <summary><c>response_json</c> (<c>json</c>, kept as written): the response the command returned.</summary>
    public string ResponseJson { get; set; } = string.Empty;

    /// <summary><c>created_at</c>: when the command succeeded (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
