namespace CityBuilder.Api.Persistence;

/// <summary>The log messages about the database (source-generated, decision 029 in core-instructions/12-decisions.md).</summary>
internal static partial class DatabaseLog
{
    /// <summary>
    /// Logs which migrations the startup migration applied (Development only; event id 2000; ids 2000–2999 belong to database
    /// messages). The source generator writes the method's body.
    /// </summary>
    /// <param name="logger">The logger of the class that applied the migrations.</param>
    /// <param name="appliedCount">How many migrations were applied; 0 when the database was up to date.</param>
    /// <param name="appliedMigrations">Their ids joined by commas, or <c>none</c>.</param>
    [LoggerMessage(EventId = 2000, Level = LogLevel.Information,
        Message = "Database migrated at startup: {AppliedCount} migrations applied ({AppliedMigrations})")]
    public static partial void MigrationsApplied(ILogger logger, int appliedCount, string appliedMigrations);
}
