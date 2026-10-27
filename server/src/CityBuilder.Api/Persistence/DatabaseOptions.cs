namespace CityBuilder.Api.Persistence;

/// <summary>
/// The database settings (docs/getting-started.md → Configuration): the connection string <c>ConnectionStrings:CityBuilder</c> and the
/// configuration section <c>Database</c>.
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>The configuration section's name.</summary>
    public const string SectionName = "Database";

    /// <summary>The connection string's name: the key <c>ConnectionStrings:CityBuilder</c>.</summary>
    public const string ConnectionStringName = "CityBuilder";

    /// <summary>
    /// The PostgreSQL connection string, from <c>ConnectionStrings:CityBuilder</c>, required. It holds a password, so it comes from
    /// user-secrets or the environment variable <c>ConnectionStrings__CityBuilder</c>, never from a committed file.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// <c>Database:ApplyMigrationsAtStartup</c>: apply the pending migrations before the server accepts requests. Only allowed in
    /// Development (<c>true</c> in <c>appsettings.Development.json</c>, <c>false</c> in <c>appsettings.json</c>); <c>true</c> in any
    /// other environment stops the start, because there the migrations are applied on purpose, never as a side effect.
    /// </summary>
    public bool ApplyMigrationsAtStartup { get; set; }

    /// <summary>The lowest allowed <see cref="LockTimeoutMilliseconds"/>: 0 would mean "wait forever" to PostgreSQL.</summary>
    public const int MinLockTimeoutMilliseconds = 1;

    /// <summary>The highest allowed <see cref="LockTimeoutMilliseconds"/>: a minute, far above any request's own work.</summary>
    public const int MaxLockTimeoutMilliseconds = 60_000;

    /// <summary>
    /// <c>Database:LockTimeoutMilliseconds</c>: how long a request waits for its city's row lock (PostgreSQL's <c>lock_timeout</c>) before it
    /// fails with <c>CONCURRENCY_CONFLICT</c> and the client retries (core-instructions/06-data-model.md: about 3 s). Default 3000; between
    /// <see cref="MinLockTimeoutMilliseconds"/> and <see cref="MaxLockTimeoutMilliseconds"/>, or the start stops.
    /// </summary>
    public int LockTimeoutMilliseconds { get; set; } = 3000;
}
