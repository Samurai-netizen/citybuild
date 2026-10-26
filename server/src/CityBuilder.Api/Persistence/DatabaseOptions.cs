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
}
