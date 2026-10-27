using Npgsql;

namespace CityBuilder.Infrastructure.Persistence;

/// <summary>Recognizes PostgreSQL's lock timeout in what EF Core and Npgsql throw, so it can become a concurrency conflict.</summary>
internal static class LockTimeouts
{
    /// <summary>
    /// Whether the exception, or one it wraps, is PostgreSQL's <c>lock_not_available</c> (SQLSTATE <c>55P03</c>): a lock wasn't granted
    /// within the transaction's <c>lock_timeout</c>.
    /// </summary>
    /// <param name="exception">What a query or a write threw.</param>
    /// <returns><see langword="true"/> for a lock timeout.</returns>
    public static bool IsLockTimeout(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.LockNotAvailable })
            {
                return true;
            }
        }

        return false;
    }
}
