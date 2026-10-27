using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Infrastructure.Persistence.Mapping;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static CityBuilder.IntegrationTests.Persistence.SampleCities;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// The database itself refuses rows that break <c>06</c>, whatever the code above it does: the rows of the full sample city are written
/// straight through a context, with one value broken, and PostgreSQL rejects them naming the constraint.
/// </summary>
/// <param name="server">The run's PostgreSQL server.</param>
[Trait("Category", "Integration")]
public sealed class ConstraintDatabaseTests(PostgresServer server) : PostgresTest(server)
{
    /// <summary>PostgreSQL's error code for a unique violation.</summary>
    private const string _uniqueViolation = "23505";

    /// <summary>PostgreSQL's error code for a check violation.</summary>
    private const string _checkViolation = "23514";

    /// <summary>The unbroken rows are accepted, so each test below fails only because of the value it breaks.</summary>
    [Fact]
    public async Task SaveChangesAsync_ValidRows_AreWritten()
    {
        await InsertFullCityAsync(_ => { });

        await using CityBuilderDbContext db = Database.CreateContext();
        Assert.Equal(6, await db.CityBuildings.CountAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Two buildings of one city on the same tile break the unique index on <c>(city_id, x, y)</c>.</summary>
    [Fact]
    public async Task SaveChangesAsync_TwoBuildingsOnOneTile_ViolatesTheUniqueTileIndex()
    {
        PostgresException error = await InsertFullCityFailsAsync(rows =>
        {
            CityBuildingRow first = rows.Buildings[0];
            rows.Buildings[1].X = first.X;
            rows.Buildings[1].Y = first.Y;
        });

        Assert.Equal((_uniqueViolation, "ux_city_buildings_city_id_x_y"), (error.SqlState, error.ConstraintName));
    }

    /// <summary>A negative stock breaks <c>quantity &gt;= 0</c>.</summary>
    [Fact]
    public async Task SaveChangesAsync_NegativeQuantity_ViolatesTheQuantityCheck()
    {
        PostgresException error = await InsertFullCityFailsAsync(rows => rows.Inventory[0].Quantity = -1);

        Assert.Equal((_checkViolation, "ck_city_inventory_quantity"), (error.SqlState, error.ConstraintName));
    }

    /// <summary>A negative worker count breaks <c>assigned_workers &gt;= 0</c>.</summary>
    [Fact]
    public async Task SaveChangesAsync_NegativeAssignedWorkers_ViolatesTheWorkersCheck()
    {
        PostgresException error = await InsertFullCityFailsAsync(rows => rows.Buildings[0].AssignedWorkers = -1);

        Assert.Equal((_checkViolation, "ck_city_buildings_assigned_workers"), (error.SqlState, error.ConstraintName));
    }

    /// <summary>Writes the full sample city's rows changed by <paramref name="breakRows"/> and expects PostgreSQL to refuse them.</summary>
    /// <param name="breakRows">Breaks one value of the rows.</param>
    /// <returns>PostgreSQL's error.</returns>
    private async Task<PostgresException> InsertFullCityFailsAsync(Action<CityRows> breakRows)
    {
        DbUpdateException error = await Assert.ThrowsAsync<DbUpdateException>(() => InsertFullCityAsync(breakRows));
        return Assert.IsType<PostgresException>(error.InnerException);
    }

    /// <summary>
    /// Writes a player and the full sample city's rows, changed by <paramref name="change"/>, in one save of a new context, without the
    /// repository.
    /// </summary>
    /// <param name="change">Changes the rows before they are written.</param>
    private async Task InsertFullCityAsync(Action<CityRows> change)
    {
        CityRows rows = CityMapping.ToRows(Full());
        rows.City.CreatedAt = PostgresApiFactory.Start;
        rows.City.UpdatedAt = PostgresApiFactory.Start;
        change(rows);
        await using CityBuilderDbContext db = Database.CreateContext();
        db.Players.Add(new PlayerRow { Id = PlayerId, DevName = "dev1", CreatedAt = PostgresApiFactory.Start });
        db.Cities.Add(rows.City);
        db.CityInventory.AddRange(rows.Inventory);
        db.CityBuildings.AddRange(rows.Buildings);
        db.CityResearch.AddRange(rows.Research);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
