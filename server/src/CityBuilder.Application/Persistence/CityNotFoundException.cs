namespace CityBuilder.Application.Persistence;

/// <summary>
/// The player named by a verified access token has no city: the token was issued before the player was deleted, for example by a reset of the
/// development database (<c>docs/getting-started.md</c> → Database). The token can't be used for anything, and the client can do something about it,
/// so the API answers <c>AUTHENTICATION_REQUIRED</c> (<c>core-instructions/05-api-contract.md</c>) and the client logs in again. Nothing of the request
/// was committed.
/// </summary>
public sealed class CityNotFoundException : Exception
{
    /// <summary>Creates the exception with a general message.</summary>
    public CityNotFoundException()
        : base("The player has no city.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">Which player has no city.</param>
    public CityNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the exception that reported it.</summary>
    /// <param name="message">Which player has no city.</param>
    /// <param name="innerException">The exception that reported it.</param>
    public CityNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
