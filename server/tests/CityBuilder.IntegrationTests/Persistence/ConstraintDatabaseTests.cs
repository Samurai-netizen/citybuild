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
        Assert.Equal(7, await db.CityBuildings.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(3, await db.CityStores.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(3, await db.CityRoads.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.CityTrips.CountAsync(TestContext.Current.CancellationToken));
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

    /// <summary>A negative stock in a store breaks <c>quantity &gt;= 0</c> (CR-0008).</summary>
    [Fact]
    public async Task SaveChangesAsync_NegativeStoreQuantity_ViolatesTheQuantityCheck()
    {
        PostgresException error = await InsertFullCityFailsAsync(rows => rows.Stores[0].Quantity = -1);

        Assert.Equal((_checkViolation, "ck_city_stores_quantity"), (error.SqlState, error.ConstraintName));
    }

    /// <summary>A need of 0 (a filled need's row must go) breaks <c>quantity &gt; 0</c> (CR-0008).</summary>
    [Fact]
    public async Task SaveChangesAsync_ZeroNeed_ViolatesTheNeedCheck()
    {
        PostgresException error = await InsertFullCityFailsAsync(rows => rows.ProjectNeeds[0].Quantity = 0);

        Assert.Equal((_checkViolation, "ck_city_project_needs_quantity"), (error.SqlState, error.ConstraintName));
    }

    /// <summary>A negative purse breaks <c>purse &gt;= 0</c> (CR-0008).</summary>
    [Fact]
    public async Task SaveChangesAsync_NegativePurse_ViolatesThePurseCheck()
    {
        PostgresException error = await InsertFullCityFailsAsync(rows => rows.City.Purse = -1);

        Assert.Equal((_checkViolation, "ck_cities_purse"), (error.SqlState, error.ConstraintName));
    }

    /// <summary>A trip that arrives when it leaves breaks <c>arrives_at &gt; departed_at</c> (CR-0008).</summary>
    [Fact]
    public async Task SaveChangesAsync_TripArrivingWhenItLeaves_ViolatesTheArrivalCheck()
    {
        PostgresException error = await InsertFullCityFailsAsync(rows => rows.Trips[0].ArrivesAt = rows.Trips[0].DepartedAt);

        Assert.Equal((_checkViolation, "ck_city_trips_arrives_at"), (error.SqlState, error.ConstraintName));
    }

    /// <summary>A negative worker count breaks <c>assigned_workers &gt;= 0</c>.</summary>
    [Fact]
    public async Task SaveChangesAsync_NegativeAssignedWorkers_ViolatesTheWorkersCheck()
    {
        PostgresException error = await InsertFullCityFailsAsync(rows => rows.Buildings[0].AssignedWorkers = -1);

        Assert.Equal((_checkViolation, "ck_city_buildings_assigned_workers"), (error.SqlState, error.ConstraintName));
    }

    /// <summary>A road at level 0 breaks <c>level &gt;= 1</c> of <c>city_roads</c> (CR-0008).</summary>
    [Fact]
    public async Task SaveChangesAsync_RoadBelowLevelOne_ViolatesTheRoadLevelCheck()
    {
        PostgresException error = await InsertFullCityFailsAsync(rows => rows.Roads[0].Level = 0);

        Assert.Equal((_checkViolation, "ck_city_roads_level"), (error.SqlState, error.ConstraintName));
    }

    /// <summary>A road at a negative column breaks <c>x &gt;= 0</c> of <c>city_roads</c>.</summary>
    [Fact]
    public async Task SaveChangesAsync_RoadAtNegativeColumn_ViolatesTheRoadColumnCheck()
    {
        PostgresException error = await InsertFullCityFailsAsync(rows => rows.Roads[0].X = -1);

        Assert.Equal((_checkViolation, "ck_city_roads_x"), (error.SqlState, error.ConstraintName));
    }

    /// <summary>A road at a negative row breaks <c>y &gt;= 0</c> of <c>city_roads</c>.</summary>
    [Fact]
    public async Task SaveChangesAsync_RoadAtNegativeRow_ViolatesTheRoadRowCheck()
    {
        PostgresException error = await InsertFullCityFailsAsync(rows => rows.Roads[0].Y = -1);

        Assert.Equal((_checkViolation, "ck_city_roads_y"), (error.SqlState, error.ConstraintName));
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
        db.CityBuildings.AddRange(rows.Buildings);
        db.CityStores.AddRange(rows.Stores);
        db.CityProjectNeeds.AddRange(rows.ProjectNeeds);
        db.CityResearch.AddRange(rows.Research);
        db.CityResearchNeeds.AddRange(rows.ResearchNeeds);
        db.CityRoads.AddRange(rows.Roads);
        db.CityTrips.AddRange(rows.Trips);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
