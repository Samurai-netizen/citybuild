using Npgsql;

namespace CityBuilder.Infrastructure.Persistence;

/// <summary>
/// Recognizes PostgreSQL's errors in what EF Core and Npgsql throw, so they can become the Application's exceptions: a lock timeout becomes a
/// concurrency conflict, a unique violation a duplicate key.
/// </summary>
internal static class PostgresErrors
{
    /// <summary>
    /// Whether the exception, or one it wraps, is PostgreSQL's <c>lock_not_available</c> (SQLSTATE <c>55P03</c>): a lock wasn't granted
    /// within the transaction's <c>lock_timeout</c>.
    /// </summary>
    /// <param name="exception">What a query or a write threw.</param>
    /// <returns><see langword="true"/> for a lock timeout.</returns>
    public static bool IsLockTimeout(Exception exception) => Find(exception, PostgresErrorCodes.LockNotAvailable) is not null;

    /// <summary>
    /// Whether the exception, or one it wraps, is PostgreSQL's <c>unique_violation</c> (SQLSTATE <c>23505</c>): an insert or update would have
    /// stored a key that a committed row already has.
    /// </summary>
    /// <param name="exception">What a write threw.</param>
    /// <param name="constraintName">The violated constraint's name (for example <c>ux_players_dev_name</c>), or <see langword="null"/>
    /// when it isn't one.</param>
    /// <returns><see langword="true"/> for a unique violation.</returns>
    public static bool IsUniqueViolation(Exception exception, out string? constraintName)
    {
        PostgresException? violation = Find(exception, PostgresErrorCodes.UniqueViolation);
        constraintName = violation?.ConstraintName;
        return violation is not null;
    }

    /// <summary>The PostgreSQL error with the SQLSTATE in the exception or the ones it wraps (EF Core wraps them, sometimes twice).</summary>
    /// <param name="exception">What a query or a write threw.</param>
    /// <param name="sqlState">The SQLSTATE to look for.</param>
    /// <returns>The error, or <see langword="null"/> when there is none.</returns>
    private static PostgresException? Find(Exception exception, string sqlState)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres && postgres.SqlState == sqlState)
            {
                return postgres;
            }
        }

        return null;
    }
}
