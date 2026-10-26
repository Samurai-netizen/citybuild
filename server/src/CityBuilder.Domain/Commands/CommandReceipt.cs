namespace CityBuilder.Domain.Commands;

/// <summary>
/// The stored answer to one successful command (<c>core-instructions/06-data-model.md</c>, <c>command_receipts</c>), so a retry with the
/// same <c>requestId</c> gets the same response and never applies twice (<c>core-instructions/05-api-contract.md</c>, idempotency). The
/// player it belongs to and the wall time are set when it is stored. Immutable.
/// </summary>
public sealed class CommandReceipt
{
    /// <summary>The length of a SHA-256 hash in bytes.</summary>
    public const int PayloadHashLength = 32;

    /// <summary>The payload hash, copied so the caller's array can't change it.</summary>
    private readonly byte[] _payloadHash;

    /// <summary>Creates the receipt.</summary>
    /// <param name="requestId">The client-generated id of the command; not <see cref="Guid.Empty"/>.</param>
    /// <param name="commandType">Which command it was; not blank.</param>
    /// <param name="payloadHash">The SHA-256 hash of the command's canonical payload, <see cref="PayloadHashLength"/> bytes.</param>
    /// <param name="responseJson">The response the command returned, as JSON; not blank.</param>
    /// <exception cref="ArgumentException">A value breaks one of the rules above.</exception>
    /// <exception cref="ArgumentNullException">A reference is null.</exception>
    public CommandReceipt(Guid requestId, string commandType, ReadOnlySpan<byte> payloadHash, string responseJson)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("A request id must not be empty.", nameof(requestId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(commandType);
        ArgumentException.ThrowIfNullOrWhiteSpace(responseJson);
        if (payloadHash.Length != PayloadHashLength)
        {
            throw new ArgumentException($"A payload hash is {PayloadHashLength} bytes (SHA-256).", nameof(payloadHash));
        }

        RequestId = requestId;
        CommandType = commandType;
        _payloadHash = payloadHash.ToArray();
        ResponseJson = responseJson;
    }

    /// <summary>The client-generated id of the command.</summary>
    public Guid RequestId { get; }

    /// <summary>Which command it was.</summary>
    public string CommandType { get; }

    /// <summary>The SHA-256 hash of the command's canonical payload; a retry with another payload is a conflict.</summary>
    public ReadOnlySpan<byte> PayloadHash => _payloadHash;

    /// <summary>The response the command returned, as JSON.</summary>
    public string ResponseJson { get; }
}
