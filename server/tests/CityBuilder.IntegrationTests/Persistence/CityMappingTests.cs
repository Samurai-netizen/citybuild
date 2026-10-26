using CityBuilder.Application.Cities;
using CityBuilder.Infrastructure.Content;
using CityBuilder.Infrastructure.Persistence.Mapping;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.IntegrationTests.Content;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;
using static CityBuilder.IntegrationTests.Persistence.SampleCities;

namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>
/// <see cref="CityMapping"/> without a database: a city mapped to rows and back is the same city, the nullable columns follow the phase,
/// and rows that break a rule are rejected on load.
/// </summary>
public sealed class CityMappingTests
{
    /// <summary>
    /// The full sample city (every phase, a frozen cycle, a cycle started at clock 0, completed and running research, era 2) and a new empty
    /// city come back from their rows with every value equal.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToAggregate_OfToRows_GivesTheSameCity(bool full)
    {
        CityAggregate city = full ? Full() : Empty();

        CityAggregate loaded = CityMapping.ToAggregate(CityMapping.ToRows(city));

        Assert.Equal(Describe(city), Describe(loaded));
    }

    /// <summary>The <c>cities</c> row carries every stored value of the city, its running research included, and leaves the wall times to the repository.</summary>
    [Fact]
    public void ToRows_FullCity_FillsTheCityRow()
    {
        CityRow row = CityMapping.ToRows(Full()).City;

        Assert.Equal(CityId, row.Id);
        Assert.Equal(PlayerId, row.PlayerId);
        Assert.Equal(2, row.Era);
        Assert.Equal(Clock, row.ClockSeconds);
        Assert.Equal(1_790_000_000, row.LastSettledUnix);
        Assert.Equal(9, row.NextBuildingId);
        Assert.Equal("forestry", row.ResearchId);
        Assert.Equal(300, row.ResearchStartedAt);
        Assert.Equal(86_700, row.ResearchEndsAt);
        Assert.Equal(7, row.StateVersion);
        Assert.Equal("content-1", row.ContentVersion);
        Assert.Equal(default, row.CreatedAt);
        Assert.Equal(default, row.UpdatedAt);
    }

    /// <summary>A city with no running research has its three research columns null.</summary>
    [Fact]
    public void ToRows_NoRunningResearch_LeavesTheResearchColumnsNull()
    {
        CityRow row = CityMapping.ToRows(Empty()).City;

        Assert.Null(row.ResearchId);
        Assert.Null(row.ResearchStartedAt);
        Assert.Null(row.ResearchEndsAt);
    }

    /// <summary>Inventory and completed research become one row each, for the city; the completed research carries its completion clock.</summary>
    [Fact]
    public void ToRows_FullCity_WritesInventoryAndCompletedResearch()
    {
        CityRows rows = CityMapping.ToRows(Full());

        Assert.Equal(
            [(CityId, "coins", 250L), (CityId, "logs", 7L)],
            rows.Inventory.Select(row => (row.CityId, row.ItemId, row.Quantity)));
        Assert.Equal([(CityId, "masonry", 120L)], rows.Research.Select(row => (row.CityId, row.ResearchId, row.CompletedAt)));
    }

    /// <summary>
    /// Each phase's row: a column is null exactly when the phase doesn't use it, and a cycle started at clock 0 stores 0, not null. Columns:
    /// phase, start, end, target level, cycle labor, labor remaining, segment start.
    /// </summary>
    [Theory]
    [InlineData(ConstructingId, "Constructing", 400L, 700L, null, null, null, null)]
    [InlineData(UpgradingId, "Upgrading", 200L, 500L, (short)2, null, null, null)]
    [InlineData(IdleId, "Idle", null, null, null, null, null, null)]
    [InlineData(ProducingId, "Producing", 300L, 440L, null, 120L, 80L, 400L)]
    [InlineData(FrozenId, "Producing", 350L, null, null, 60L, 60L, 350L)]
    [InlineData(StartedAtZeroId, "Producing", 0L, 600L, null, 600L, 600L, 0L)]
    public void ToRows_EachPhase_SetsExactlyTheColumnsThePhaseUses(
        int buildingId, string phase, long? startedAt, long? endsAt, short? targetLevel, long? cycleLabor, long? laborRemaining, long? segmentStartedAt)
    {
        CityBuildingRow row = CityMapping.ToRows(Full()).Buildings.Single(building => building.BuildingId == buildingId);

        Assert.Equal(
            (phase, startedAt, endsAt, targetLevel, cycleLabor, laborRemaining, segmentStartedAt),
            (row.Phase, row.PhaseStartedAt, row.PhaseEndsAt, row.TargetLevel, row.CycleLabor, row.LaborRemaining, row.SegmentStartedAt));
    }

    /// <summary>A building's fixed values (city, type, tile, level, workers) are stored as they are.</summary>
    [Fact]
    public void ToRows_Building_KeepsItsFixedValues()
    {
        CityBuildingRow row = CityMapping.ToRows(Full()).Buildings.Single(building => building.BuildingId == ProducingId);

        Assert.Equal((CityId, "woodcutter", (short)3, (short)0, (short)1, (short)2), (row.CityId, row.TypeId, row.X, row.Y, row.Level, row.AssignedWorkers));
    }

    /// <summary>An inventory row at 0 (an item the city used up) loads as no stock of that item.</summary>
    [Fact]
    public void ToAggregate_InventoryRowAtZero_LoadsAsNoStock()
    {
        CityRows rows = CityMapping.ToRows(Full());
        rows.Inventory[1].Quantity = 0;

        CityAggregate loaded = CityMapping.ToAggregate(rows);

        Assert.Equal([new ItemId("coins")], loaded.State.Inventory.Keys);
    }

    /// <summary>A completed research the aggregate has no completion clock for can't be mapped: it was advanced around <see cref="CityAggregate.Advance"/>.</summary>
    [Fact]
    public void ToRows_ResearchCompletedAroundTheAggregate_Throws()
    {
        var state = new CityState(100, 1, 1, [], [], [], new ActiveResearch(new ResearchId("sharp_axes"), 0, 100));
        var city = new CityAggregate(CityId, PlayerId, state, [], 0, 0, "content-1");
        Simulator.Advance(state, ContentFileLoader.Load(ContentFiles.RealMvpContentPath), 0);
        Assert.True(state.HasCompletedResearch(new ResearchId("sharp_axes")));

        Assert.Throws<InvalidOperationException>(() => CityMapping.ToRows(city));
    }

    /// <summary>
    /// Rows that break a rule are rejected on load with <see cref="InvalidOperationException"/>: a column set or null against its building's
    /// phase, an unknown phase, a partial running research, a research both completed and running, and a row of another city.
    /// </summary>
    [Theory]
    [InlineData("idle with a start")]
    [InlineData("idle with an end")]
    [InlineData("idle with cycle labor 0")]
    [InlineData("constructing without an end")]
    [InlineData("constructing with a target level")]
    [InlineData("upgrading without a target level")]
    [InlineData("producing without cycle labor")]
    [InlineData("producing without a segment start")]
    [InlineData("unknown phase")]
    [InlineData("lower-case phase")]
    [InlineData("research without an end")]
    [InlineData("research completed and running")]
    [InlineData("building of another city")]
    [InlineData("research row of another city")]
    public void ToAggregate_BrokenRow_Throws(string breakage)
    {
        CityRows rows = CityMapping.ToRows(Full());
        // The row of the building with the id, to break it.
        CityBuildingRow Building(int id) => rows.Buildings.Single(building => building.BuildingId == id);
        switch (breakage)
        {
            case "idle with a start": Building(IdleId).PhaseStartedAt = 0; break;
            case "idle with an end": Building(IdleId).PhaseEndsAt = 10; break;
            case "idle with cycle labor 0": Building(IdleId).CycleLabor = 0; break;
            case "constructing without an end": Building(ConstructingId).PhaseEndsAt = null; break;
            case "constructing with a target level": Building(ConstructingId).TargetLevel = 2; break;
            case "upgrading without a target level": Building(UpgradingId).TargetLevel = null; break;
            case "producing without cycle labor": Building(ProducingId).CycleLabor = null; break;
            case "producing without a segment start": Building(StartedAtZeroId).SegmentStartedAt = null; break;
            case "unknown phase": Building(IdleId).Phase = "Demolished"; break;
            case "lower-case phase": Building(IdleId).Phase = "idle"; break;
            case "research without an end": rows.City.ResearchEndsAt = null; break;
            case "research completed and running": rows.City.ResearchId = "masonry"; break;
            case "building of another city": Building(IdleId).CityId = Guid.NewGuid(); break;
            case "research row of another city": rows.Research[0].CityId = Guid.NewGuid(); break;
            default: throw new ArgumentOutOfRangeException(nameof(breakage), breakage, "Not a breakage of this test.");
        }

        Assert.Throws<InvalidOperationException>(() => CityMapping.ToAggregate(rows));
    }
}
