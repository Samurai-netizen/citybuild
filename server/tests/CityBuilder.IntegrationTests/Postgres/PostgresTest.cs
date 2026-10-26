namespace CityBuilder.IntegrationTests.Postgres;

/// <summary>
/// The base of a test class that needs PostgreSQL: before each test it creates the test's own <see cref="Database"/> on the run's server,
/// and after it disposes the <see cref="Factory"/> (if the test used it) and drops the database, so no test sees another's rows.
/// </summary>
/// <param name="server">The run's PostgreSQL server (assembly fixture).</param>
/// <param name="migrated"><see langword="true"/> (the default) for a migrated database; <see langword="false"/> for an empty one.</param>
public abstract class PostgresTest(PostgresServer server, bool migrated = true) : IAsyncLifetime
{
    /// <summary>The in-memory server on the database; created on first use.</summary>
    private PostgresApiFactory? _factory;

    /// <summary>The test's database; set before the test runs.</summary>
    protected TestDatabase Database { get; private set; } = null!;

    /// <summary>The in-memory server on <see cref="Database"/>, created the first time a test uses it.</summary>
    protected PostgresApiFactory Factory => _factory ??= new PostgresApiFactory(Database);

    /// <summary>Creates the test's database.</summary>
    /// <returns>A task that completes when the database exists.</returns>
    public async ValueTask InitializeAsync() => Database = await server.CreateDatabaseAsync(migrated);

    /// <summary>Stops the in-memory server, if the test started it, and drops the database.</summary>
    /// <returns>A task that completes when both are gone.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (Database is not null)
        {
            await Database.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
