extern alias MetricsTool;

using CityBuilder.IntegrationTests.Postgres;
using MetricsTool::CityBuilder.MetricsReport;

namespace CityBuilder.IntegrationTests.Metrics;

/// <summary>
/// The metrics report's command line on a database it can't read (docs/plan/CR-0009-T2-PLAN.md §3.1, §3.5): a database without
/// <c>player_events</c> (an empty one, never migrated) and no connection string both end with exit code 2 and a message, nothing written.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class MetricsReportDatabaseTests(PostgresServer server) : PostgresTest(server, migrated: false)
{
    /// <summary>An empty database: exit code 2, a message naming the missing table, no page.</summary>
    [Fact]
    public void Run_DatabaseWithoutPlayerEvents_ReturnsTwo()
    {
        string page = Path.Combine(Path.GetTempPath(), $"metrics-{Guid.NewGuid():N}.html");
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = CommandLine.Run(["--out", page], output, error, Database.ConnectionString);

        Assert.Equal(CommandLine.DatabaseError, code);
        Assert.Contains("player_events", error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(page));
    }

    /// <summary>No connection string: exit code 2 and a message naming the environment variable.</summary>
    [Fact]
    public void Run_NoConnectionString_ReturnsTwo()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = CommandLine.Run(["--out", "unused.html"], output, error, connectionString: null);

        Assert.Equal(CommandLine.DatabaseError, code);
        Assert.Contains("ConnectionStrings__CityBuilder", error.ToString(), StringComparison.Ordinal);
    }
}
