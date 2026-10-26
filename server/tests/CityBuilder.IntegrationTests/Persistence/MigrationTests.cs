using Microsoft.EntityFrameworkCore;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// The committed migrations (<c>Infrastructure/Persistence/Migrations</c>) match the model, so the schema of
/// <see cref="PersistenceModelTests"/> is the one a database gets. No database and no Docker: EF compares the model with the migrations'
/// snapshot. Applying them to PostgreSQL is an integration test (M0-21).
/// </summary>
public sealed class MigrationTests
{
    /// <summary>
    /// The model has no change that the migrations don't cover. When this fails, someone changed a row or a configuration without
    /// adding a migration (docs/how-to/add-migration.md).
    /// </summary>
    [Fact]
    public void HasPendingModelChanges_CommittedMigrations_False()
    {
        using var context = PersistenceTestContext.Create();

        Assert.False(context.Database.HasPendingModelChanges(),
            "The model has changes no migration covers: add one with dotnet ef migrations add (docs/how-to/add-migration.md).");
    }

    /// <summary>The migrations are known by their ids, in the order they run; the first creates the M0 schema.</summary>
    [Fact]
    public void GetMigrations_Infrastructure_StartWithInitialCreate()
    {
        using var context = PersistenceTestContext.Create();

        Assert.Equal("20261026193554_InitialCreate", context.Database.GetMigrations().First());
    }
}
