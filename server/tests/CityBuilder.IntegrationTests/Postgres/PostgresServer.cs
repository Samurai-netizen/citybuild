using CityBuilder.Infrastructure.Persistence;
using CityBuilder.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgresServer))]

namespace CityBuilder.IntegrationTests.Postgres;

/// <summary>
/// The one PostgreSQL server of a test run: a <c>postgres:18.6</c> container (Testcontainers), shared by every integration test through
/// xUnit's assembly fixture. It starts on the first database a test asks for, so a run without integration tests (the fast suite) never
/// needs Docker. The committed migrations are applied once, to a template database; every test then gets a database of its own, copied
/// from the template or empty (docs/testing/README.md).
/// </summary>
public sealed class PostgresServer : IAsyncDisposable
{
    /// <summary>The image of the container: the PostgreSQL version of the stack (core-instructions/06-data-model.md), as in docker-compose.yml.</summary>
    public const string Image = "postgres:18.6";

    /// <summary>The migrated database every test database is copied from; nothing connects to it after the migration.</summary>
    private const string _templateName = "citybuilder_template";

    /// <summary>Lets one caller at a time start the container or create a database.</summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>The running container; <see langword="null"/> until the first database is asked for.</summary>
    private PostgreSqlContainer? _container;

    /// <summary>
    /// Creates a new database for one test, starting the container and migrating the template first if this is the run's first database.
    /// </summary>
    /// <param name="migrated"><see langword="true"/> for a copy of the migrated template (the schema of <c>06</c>, no rows);
    /// <see langword="false"/> for an empty database, for the tests that migrate one themselves.</param>
    /// <returns>The database; the test disposes it, which drops it.</returns>
    public async Task<TestDatabase> CreateDatabaseAsync(bool migrated)
    {
        string name = "test_" + Guid.NewGuid().ToString("N");
        await _gate.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            PostgreSqlContainer container = _container ?? await StartAsync();
            string template = migrated ? _templateName : "template0";
            await ExecuteAsync(container, $"CREATE DATABASE \"{name}\" TEMPLATE \"{template}\"");
            return new TestDatabase(this, name, ConnectionString(container, name, pooling: true));
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Drops a test's database, ending any connection still open to it.</summary>
    /// <param name="name">The database's name.</param>
    internal async Task DropDatabaseAsync(string name)
    {
        PostgreSqlContainer container = _container ?? throw new InvalidOperationException("The PostgreSQL container isn't running.");
        await ExecuteAsync(container, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
    }

    /// <summary>Stops and removes the container, if it was started.</summary>
    /// <returns>A task that completes when the container is gone.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }

        _gate.Dispose();
    }

    /// <summary>Starts the container and migrates the template database; called once, under the gate.</summary>
    /// <returns>The running container.</returns>
    private async Task<PostgreSqlContainer> StartAsync()
    {
        // Without Docker, Build throws Testcontainers' DockerUnavailableException, which says so.
        PostgreSqlContainer container = new PostgreSqlBuilder(Image).Build();
        await container.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            await ExecuteAsync(container, $"CREATE DATABASE \"{_templateName}\"");
            // Without pooling the migration's connection closes with the context; CREATE DATABASE … TEMPLATE fails while one is open.
            var options = new DbContextOptionsBuilder<CityBuilderDbContext>()
                .UseNpgsql(ConnectionString(container, _templateName, pooling: false))
                .Options;
            await using var context = new CityBuilderDbContext(options);
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }
        catch
        {
            // A failed migration leaves no half-made template behind: the next test starts a new container.
            await container.DisposeAsync();
            throw;
        }

        _container = container;
        return container;
    }

    /// <summary>Runs one statement on the server's maintenance database (<c>postgres</c>).</summary>
    /// <param name="container">The running container.</param>
    /// <param name="sql">The statement; only names this class makes go into it.</param>
    private static async Task ExecuteAsync(PostgreSqlContainer container, string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString(container, "postgres", pooling: false));
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The connection string of one database on the container.</summary>
    /// <param name="container">The running container.</param>
    /// <param name="database">The database's name.</param>
    /// <param name="pooling">Whether Npgsql keeps closed connections open in its pool.</param>
    /// <returns>The connection string.</returns>
    private static string ConnectionString(PostgreSqlContainer container, string database, bool pooling) =>
        new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = database, Pooling = pooling }.ConnectionString;
}
