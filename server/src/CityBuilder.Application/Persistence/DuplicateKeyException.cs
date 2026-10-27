namespace CityBuilder.Application.Persistence;

/// <summary>
/// A write would have stored a key that another request had committed first: a unique constraint refused it (<c>core-instructions/06-data-model.md</c>).
/// Two requests raced to create the same thing (two first logins of one name); the loser's transaction is rolled back, and it reads what
/// the winner stored instead.
/// </summary>
public sealed class DuplicateKeyException : Exception
{
    /// <summary>Creates the exception with a general message.</summary>
    public DuplicateKeyException()
        : base("Another request stored the same key first; nothing of this write was committed.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">Which key was taken.</param>
    public DuplicateKeyException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the database's exception that reported the violation.</summary>
    /// <param name="message">Which key was taken.</param>
    /// <param name="innerException">The database's exception.</param>
    public DuplicateKeyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
