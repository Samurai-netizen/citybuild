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
        Assert.Equal(10, row.NextBuildingId);
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

    /// <summary>
    /// The purse and the next trip id go into the <c>cities</c> row; every good of a store, every need of a waiting project and every trip
    /// becomes one row of its table, for the city, in the engine's order; a completed research carries its completion clock (CR-0008).
    /// </summary>
    [Fact]
    public void ToRows_FullCity_WritesThePurseStoresNeedsTripsAndCompletedResearch()
    {
        CityRows rows = CityMapping.ToRows(Full());

        Assert.Equal((250L, 3), (rows.City.Purse, rows.City.NextTripId));
        Assert.Equal(
            [(CityId, UpgradingId, "logs", 3L), (CityId, IdleId, "logs", 2L), (CityId, ProducingId, "logs", 7L)],
            rows.Stores.Select(row => (row.CityId, row.BuildingId, row.ItemId, row.Quantity)));
        Assert.Equal(
            [(CityId, IdleId, "planks", 5L), (CityId, SiteId, "logs", 10L)],
            rows.ProjectNeeds.Select(row => (row.CityId, row.BuildingId, row.ItemId, row.Quantity)));
        Assert.Equal(
            [(CityId, 1, ProducingId, IdleId, "logs", 2L, 410L, 430L)],
            rows.Trips.Select(row => (row.CityId, row.TripId, row.SourceId, row.TargetId, row.ItemId, row.Quantity, row.DepartedAt, row.ArrivesAt)));
        Assert.Empty(rows.ResearchNeeds);
        Assert.Equal([(CityId, "masonry", 120L)], rows.Research.Select(row => (row.CityId, row.ResearchId, row.CompletedAt)));
    }

    /// <summary>A research waiting for goods has no start and no end, and its needs become <c>city_research_needs</c> rows (CR-0008).</summary>
    [Fact]
    public void ToRows_WaitingResearch_HasNoTimesAndNeedRows()
    {
        CityRows rows = CityMapping.ToRows(Later());

        Assert.Equal(("carpentry", (long?)null, (long?)null), (rows.City.ResearchId, rows.City.ResearchStartedAt, rows.City.ResearchEndsAt));
        Assert.Equal([(CityId, "planks", 4L)], rows.ResearchNeeds.Select(row => (row.CityId, row.ItemId, row.Quantity)));
    }

    /// <summary>Each road becomes one <c>city_roads</c> row for the city with its tile and level, in the engine's order (column, then row).</summary>
    [Fact]
    public void ToRows_FullCity_WritesARowPerRoad()
    {
        CityRows rows = CityMapping.ToRows(Full());

        Assert.Equal(
            [(CityId, (short)2, (short)1, (short)1), (CityId, (short)3, (short)1, (short)2), (CityId, (short)4, (short)0, (short)1)],
            rows.Roads.Select(row => (row.CityId, row.X, row.Y, row.Level)));
    }

    /// <summary>
    /// Each phase's row: a column is null exactly when the phase doesn't use it, and a cycle started at clock 0 stores 0, not null; a
    /// construction site has no start and no end (CR-0008). Columns: phase, start, end, target level, cycle labor, labor remaining, segment
    /// start.
    /// </summary>
    [Theory]
    [InlineData(ConstructingId, "Constructing", 400L, 700L, null, null, null, null)]
    [InlineData(UpgradingId, "Upgrading", 200L, 500L, (short)2, null, null, null)]
    [InlineData(IdleId, "Idle", null, null, null, null, null, null)]
    [InlineData(ProducingId, "Producing", 300L, 440L, null, 120L, 80L, 400L)]
    [InlineData(FrozenId, "Producing", 350L, null, null, 60L, 60L, 350L)]
    [InlineData(StartedAtZeroId, "Producing", 0L, 600L, null, 600L, 600L, 0L)]
    [InlineData(SiteId, "Constructing", null, null, null, null, null, null)]
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

    /// <summary>A store row at 0 (a good the store gave away; its row stays) loads as no stock of that good.</summary>
    [Fact]
    public void ToAggregate_StoreRowAtZero_LoadsAsNoStock()
    {
        CityRows rows = CityMapping.ToRows(Full());
        rows.Stores[0].Quantity = 0;

        CityAggregate loaded = CityMapping.ToAggregate(rows);

        Assert.Empty(loaded.State.Buildings.Single(building => building.Id == UpgradingId).Store);
    }

    /// <summary>
    /// Rows that break the mapping's CR-0008 rules fail the load: a running research with need rows, a research without times or needs, and a
    /// store row of a building the city doesn't have.
    /// </summary>
    [Fact]
    public void ToAggregate_BrokenLogisticsRows_Throw()
    {
        CityRows timedWithNeeds = CityMapping.ToRows(Full());
        CityRows waitingWithoutNeeds = CityMapping.ToRows(Later());
        CityRows unknownStore = CityMapping.ToRows(Full());

        CityRows withNeeds = new(
            timedWithNeeds.City, timedWithNeeds.Buildings, timedWithNeeds.Stores, timedWithNeeds.ProjectNeeds, timedWithNeeds.Research,
            [new CityResearchNeedRow { CityId = CityId, ItemId = "planks", Quantity = 1 }], timedWithNeeds.Roads, timedWithNeeds.Trips);
        CityRows withoutNeeds = new(
            waitingWithoutNeeds.City, waitingWithoutNeeds.Buildings, waitingWithoutNeeds.Stores, waitingWithoutNeeds.ProjectNeeds,
            waitingWithoutNeeds.Research, [], waitingWithoutNeeds.Roads, waitingWithoutNeeds.Trips);
        unknownStore.Stores[0].BuildingId = 77;

        Assert.Throws<InvalidOperationException>(() => CityMapping.ToAggregate(withNeeds));
        Assert.Throws<InvalidOperationException>(() => CityMapping.ToAggregate(withoutNeeds));
        Assert.Throws<InvalidOperationException>(() => CityMapping.ToAggregate(unknownStore));
    }

    /// <summary>
    /// Courier and route rows that form no courier or route fail the load (CR-0008-T1 part G): an unknown courier state, a walking courier
    /// without times, a load of a courier the city doesn't have, a route whose stops skip an index, a stop whose actions name two buildings,
    /// an action kind other than take or put, an amount that is no amount, and a route of one stop.
    /// </summary>
    [Theory]
    [InlineData("courier state")]
    [InlineData("walking without times")]
    [InlineData("load of no courier")]
    [InlineData("stop index gap")]
    [InlineData("stop of two buildings")]
    [InlineData("action kind")]
    [InlineData("amount")]
    [InlineData("one stop")]
    public void ToAggregate_BrokenCourierOrRouteRows_Throw(string broken)
    {
        CityRows rows = CityMapping.ToRows(Full());
        CityRouteActionRow first = rows.RouteActions.First(row => row.Slot == 1);
        List<CityRouteActionRow> actions = [.. rows.RouteActions];
        List<CityCourierLoadRow> loads = [.. rows.CourierLoads];
        switch (broken)
        {
            case "courier state":
                rows.Couriers[0].State = "Dancing";
                break;
            case "walking without times":
                rows.Couriers[0].DepartedAt = null;
                rows.Couriers[0].ArrivesAt = null;
                break;
            case "load of no courier":
                loads.Add(new CityCourierLoadRow { CityId = CityId, StationId = StationId, Slot = 5, ItemId = "logs", FromBuildingId = IdleId, Quantity = 1 });
                break;
            case "stop index gap":
                foreach (CityRouteActionRow row in actions.Where(row => row.Slot == 2 && row.StopIndex == 1))
                {
                    row.StopIndex = 2;
                }

                break;
            case "stop of two buildings":
                actions.Single(row => row.Slot == 1 && row.StopIndex == 0 && row.ActionIndex == 1).BuildingId = SiteId;
                break;
            case "action kind":
                first.Kind = "drop";
                break;
            case "amount":
                first.Amount = "1.5%";
                break;
            default:
                actions.RemoveAll(row => row.Slot == 2 && row.StopIndex == 1);
                break;
        }

        var changed = new CityRows(
            rows.City, rows.Buildings, rows.Stores, rows.ProjectNeeds, rows.Research, rows.ResearchNeeds, rows.Roads, rows.Trips, rows.Couriers, loads, actions);

        Assert.Throws<InvalidOperationException>(() => CityMapping.ToAggregate(changed));
    }

    /// <summary>A completed research the aggregate has no completion clock for can't be mapped: it was advanced around <see cref="CityAggregate.Advance"/>.</summary>
    [Fact]
    public void ToRows_ResearchCompletedAroundTheAggregate_Throws()
    {
        var state = new CityState(100, 1, 1, 0, [], [], new ActiveResearch(new ResearchId("sharp_axes"), 0, 100));
        var city = new CityAggregate(CityId, PlayerId, state, [], 0, 0, "content-1");
        Simulator.Advance(state, ContentFileLoader.Load(ContentFiles.RealMvpContentPath), 0);
        Assert.True(state.HasCompletedResearch(new ResearchId("sharp_axes")));

        Assert.Throws<InvalidOperationException>(() => CityMapping.ToRows(city));
    }

    /// <summary>
    /// Rows that break a rule are rejected on load with <see cref="InvalidOperationException"/>: a column set or null against its building's
    /// phase, an unknown phase, a partial running research, a research both completed and running, a row of another city, and a road on a
    /// building's tile, on a tile twice or below level 1.
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
    [InlineData("road row of another city")]
    [InlineData("road on a building's tile")]
    [InlineData("two roads on one tile")]
    [InlineData("road at level 0")]
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
            case "road row of another city": rows.Roads[0].CityId = Guid.NewGuid(); break;
            case "road on a building's tile": rows.Roads[0].X = 0; rows.Roads[0].Y = 0; break;
            case "two roads on one tile": rows.Roads[1].X = rows.Roads[0].X; rows.Roads[1].Y = rows.Roads[0].Y; break;
            case "road at level 0": rows.Roads[0].Level = 0; break;
            default: throw new ArgumentOutOfRangeException(nameof(breakage), breakage, "Not a breakage of this test.");
        }

        Assert.Throws<InvalidOperationException>(() => CityMapping.ToAggregate(rows));
    }
}
