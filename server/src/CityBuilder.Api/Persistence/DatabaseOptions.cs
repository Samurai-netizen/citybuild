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
}
