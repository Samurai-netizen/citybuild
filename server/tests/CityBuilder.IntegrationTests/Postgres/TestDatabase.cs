using CityBuilder.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CityBuilder.IntegrationTests.Postgres;

/// <summary>One test's own database on the run's <see cref="PostgresServer"/>; disposing it drops the database.</summary>
public sealed class TestDatabase : IAsyncDisposable
{
    /// <summary>The server the database lives on; it drops the database.</summary>
    private readonly PostgresServer _server;

    /// <summary>Wraps a database the server has just created.</summary>
    /// <param name="server">The server the database lives on.</param>
    /// <param name="name">The database's name.</param>
    /// <param name="connectionString">The connection string of the database.</param>
    internal TestDatabase(PostgresServer server, string name, string connectionString)
    {
        _server = server;
        Name = name;
        ConnectionString = connectionString;
    }

    /// <summary>The database's name, unique in the run.</summary>
    public string Name { get; }

    /// <summary>The connection string of the database, for the server under test or a context of the test's own.</summary>
    public string ConnectionString { get; }

    /// <summary>A new context on the database, with the provider the server uses and no state shared with any other context.</summary>
    /// <returns>The context; the caller disposes it.</returns>
    public CityBuilderDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CityBuilderDbContext>().UseNpgsql(ConnectionString).Options);

    /// <summary>Closes the pooled connections to the database and drops it.</summary>
    /// <returns>A task that completes when the database is gone.</returns>
    public async ValueTask DisposeAsync()
    {
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            NpgsqlConnection.ClearPool(connection);
        }

        await _server.DropDatabaseAsync(Name);
    }
}
