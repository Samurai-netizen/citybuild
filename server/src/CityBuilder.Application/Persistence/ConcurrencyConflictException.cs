namespace CityBuilder.Application.Persistence;

/// <summary>
/// Another request got in the way of this one's city transaction: the city's row lock wasn't granted within the lock timeout, or the stored
/// <c>state_version</c> wasn't the loaded one when the city was written (<c>core-instructions/06-data-model.md</c>, write transaction
/// pattern). Nothing of the request was committed, so it is safe to retry; the API answers <c>CONCURRENCY_CONFLICT</c>
/// (<c>core-instructions/05-api-contract.md</c>), and the client retries with the same <c>requestId</c>.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    /// <summary>Creates the exception with a general message.</summary>
    public ConcurrencyConflictException()
        : base("Another request changed or locked the city; nothing was committed, so the request is safe to retry.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What got in the way.</param>
    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the database's exception that reported the conflict.</summary>
    /// <param name="message">What got in the way.</param>
    /// <param name="innerException">The database's exception.</param>
    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
