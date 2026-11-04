using CityBuilder.Application.Cities;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.Simulation;
using CityBuilder.Simulation.State;

namespace CityBuilder.Infrastructure.Persistence.Mapping;

/// <summary>
/// Maps a <see cref="CityAggregate"/> to its rows and back, explicitly and without a database (<c>core-instructions/06-data-model.md</c>,
/// EF Core conventions). Only the engine's own state is stored; reserved outputs, stall reasons, workers provided and free, builders busy,
/// effective limits, totals and labor are derived from it after load. A building's nullable columns are null exactly when its phase doesn't
/// use them (<see cref="ToBuildingRow"/>); a construction site and a research waiting for goods have no times and need rows (CR-0008). Loading rows that break a rule of the engine or of this mapping throws, so a broken row is never
/// settled.
/// </summary>
public static class CityMapping
{
    /// <summary>The <c>phase</c> text of <see cref="BuildingPhase.Constructing"/>, as the API spells it (<c>05</c>).</summary>
    private const string _constructing = "Constructing";

    /// <summary>The <c>phase</c> text of <see cref="BuildingPhase.Upgrading"/>.</summary>
    private const string _upgrading = "Upgrading";

    /// <summary>The <c>phase</c> text of <see cref="BuildingPhase.Idle"/>.</summary>
    private const string _idle = "Idle";

    /// <summary>The <c>phase</c> text of <see cref="BuildingPhase.Producing"/>.</summary>
    private const string _producing = "Producing";

    /// <summary>
    /// The city's rows as they are now. The <c>cities</c> row carries the aggregate's <see cref="CityAggregate.StateVersion"/>; its wall
    /// times (<c>created_at</c>, <c>updated_at</c>) are left at their default for the repository to set. Store rows exist only for the goods
    /// a store holds now (the engine drops a good at 0; the repository keeps an emptied row at 0), need rows only for what is still missing,
    /// road rows only for the tiles that have a road, trip rows only for the trips under way.
    /// </summary>
    /// <param name="city">The city.</param>
    /// <returns>The rows, children in the engine's order (buildings, trips and research by id, goods by id, roads by column, then row).</returns>
    /// <exception cref="InvalidOperationException">A completed research has no completion clock (the city was advanced around
    /// <see cref="CityAggregate.Advance"/>).</exception>
    /// <exception cref="OverflowException">A value doesn't fit its column (<c>smallint</c>).</exception>
    public static CityRows ToRows(CityAggregate city)
    {
        ArgumentNullException.ThrowIfNull(city);

        CityState state = city.State;
        ActiveResearch? active = state.ActiveResearch;
        var cityRow = new CityRow
        {
            Id = city.CityId,
            PlayerId = city.PlayerId,
            Era = checked((short)state.Era),
            ClockSeconds = state.Clock,
            LastSettledUnix = city.LastSettledUnixSec,
            NextBuildingId = state.NextBuildingId,
            Purse = state.Purse,
            NextTripId = state.NextTripId,
            ResearchId = active?.Id.Value,
            ResearchStartedAt = active?.StartedAt,
            ResearchEndsAt = active?.EndsAt,
            StateVersion = city.StateVersion,
            ContentVersion = city.ContentVersion,
        };

        var buildings = state.Buildings.Select(building => ToBuildingRow(city.CityId, building)).ToList();
        var stores = state.Buildings
            .SelectMany(building => building.Store.Select(stock => new CityStoreRow
            {
                CityId = city.CityId,
                BuildingId = building.Id,
                ItemId = stock.Key.Value,
                Quantity = stock.Value,
            }))
            .ToList();
        var projectNeeds = state.Buildings
            .SelectMany(building => building.Needs.Select(need => new CityProjectNeedRow
            {
                CityId = city.CityId,
                BuildingId = building.Id,
                ItemId = need.Key.Value,
                Quantity = need.Value,
            }))
            .ToList();
        var researchNeeds = (active?.Needs ?? new Dictionary<ItemId, long>())
            .Select(need => new CityResearchNeedRow { CityId = city.CityId, ItemId = need.Key.Value, Quantity = need.Value })
            .ToList();
        var trips = state.Trips
            .Select(trip => new CityTripRow
            {
                CityId = city.CityId,
                TripId = trip.Id,
                SourceId = trip.SourceId,
                TargetId = trip.TargetId,
                ItemId = trip.Item.Value,
                Quantity = trip.Quantity,
                DepartedAt = trip.DepartedAt,
                ArrivesAt = trip.ArrivesAt,
            })
            .ToList();
        var research = state.CompletedResearch
            .Select(id => new CityResearchRow { CityId = city.CityId, ResearchId = id.Value, CompletedAt = CompletedAt(city, id) })
            .ToList();
        var roads = state.Roads
            .Select(road => new CityRoadRow
            {
                CityId = city.CityId,
                X = checked((short)road.X),
                Y = checked((short)road.Y),
                Level = checked((short)road.Level),
            })
            .ToList();

        return new CityRows(cityRow, buildings, stores, projectNeeds, research, researchNeeds, roads, trips);
    }

    /// <summary>The city the rows describe, with its engine state built from them.</summary>
    /// <param name="rows">The city's rows; every child row belongs to the city row.</param>
    /// <returns>The city.</returns>
    /// <exception cref="InvalidOperationException">A row breaks a rule of the engine or of this mapping; the message names the row.</exception>
    public static CityAggregate ToAggregate(CityRows rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        CityRow city = rows.City;
        RequireOwnRows(rows);
        try
        {
            ILookup<int, CityStoreRow> stores = rows.Stores.ToLookup(row => row.BuildingId);
            ILookup<int, CityProjectNeedRow> needs = rows.ProjectNeeds.ToLookup(row => row.BuildingId);
            RequireKnownBuildings(rows);
            var state = new CityState(
                city.ClockSeconds,
                city.Era,
                city.NextBuildingId,
                city.Purse,
                rows.Buildings.Select(row => ToBuilding(row, stores[row.BuildingId], needs[row.BuildingId])),
                rows.Research.Select(row => new ResearchId(row.ResearchId)),
                ToActiveResearch(city, rows.ResearchNeeds),
                rows.Roads.Select(row => new RoadTile(row.X, row.Y, row.Level)),
                rows.Trips.Select(row => new Trip(
                    row.TripId, row.SourceId, row.TargetId, new ItemId(row.ItemId), row.Quantity, row.DepartedAt, row.ArrivesAt)),
                city.NextTripId);
            IEnumerable<KeyValuePair<ResearchId, long>> completedAt =
                rows.Research.Select(row => new KeyValuePair<ResearchId, long>(new ResearchId(row.ResearchId), row.CompletedAt));

            return new CityAggregate(city.Id, city.PlayerId, state, completedAt, city.LastSettledUnix, city.StateVersion, city.ContentVersion);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException($"The stored city {city.Id} is not valid: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// The building's row. Its nullable columns are null exactly when the phase doesn't use them, where the engine keeps 0: Constructing
    /// uses the start and the end, except a construction site (no timer yet, CR-0008), which uses neither; Upgrading the start, the end and the
    /// target level; Idle none; Producing the start, the cycle labor, the labor remaining and the segment start, and the end unless the cycle
    /// is frozen (no workers). A cycle can start at clock 0, so 0 is a real value, never a stand-in for null. Its store and needs are rows of
    /// their own.
    /// </summary>
    /// <param name="cityId">The city's id.</param>
    /// <param name="building">The building.</param>
    /// <returns>The row.</returns>
    private static CityBuildingRow ToBuildingRow(Guid cityId, Building building)
    {
        BuildingPhase phase = building.Phase;
        bool producing = phase == BuildingPhase.Producing;
        return new CityBuildingRow
        {
            CityId = cityId,
            BuildingId = building.Id,
            TypeId = building.TypeId.Value,
            X = checked((short)building.X),
            Y = checked((short)building.Y),
            Level = checked((short)building.Level),
            AssignedWorkers = checked((short)building.AssignedWorkers),
            Phase = PhaseText(phase),
            PhaseStartedAt = phase == BuildingPhase.Idle || building.IsConstructionSite ? null : building.PhaseStartedAt,
            PhaseEndsAt = building.PhaseEndsAt,
            TargetLevel = phase == BuildingPhase.Upgrading ? checked((short)building.TargetLevel) : null,
            CycleLabor = producing ? building.CycleLabor : null,
            LaborRemaining = producing ? building.LaborRemaining : null,
            SegmentStartedAt = producing ? building.SegmentStartedAt : null,
        };
    }

    /// <summary>
    /// The building of a row with its store and need rows, after checking that each nullable column is null exactly when the phase doesn't
    /// use it (<see cref="ToBuildingRow"/>; a building constructing without a start is a site, which the engine requires to have needs); the
    /// engine's constructor checks the rest.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="store">The building's <c>city_stores</c> rows; a row at 0 is no stock.</param>
    /// <param name="needs">The building's <c>city_project_needs</c> rows.</param>
    /// <returns>The building.</returns>
    /// <exception cref="ArgumentException">The row breaks a rule; the message names the building.</exception>
    private static Building ToBuilding(CityBuildingRow row, IEnumerable<CityStoreRow> store, IEnumerable<CityProjectNeedRow> needs)
    {
        BuildingPhase phase = ParsePhase(row);
        bool producing = phase == BuildingPhase.Producing;
        bool site = phase == BuildingPhase.Constructing && !row.PhaseStartedAt.HasValue && !row.PhaseEndsAt.HasValue;
        bool endRequired = (phase is BuildingPhase.Constructing or BuildingPhase.Upgrading) && !site;
        bool columnsMatchPhase =
            row.PhaseStartedAt.HasValue == (phase != BuildingPhase.Idle && !site)
            && (row.PhaseEndsAt.HasValue || !endRequired)
            && (!row.PhaseEndsAt.HasValue || phase != BuildingPhase.Idle)
            && row.TargetLevel.HasValue == (phase == BuildingPhase.Upgrading)
            && row.CycleLabor.HasValue == producing
            && row.LaborRemaining.HasValue == producing
            && row.SegmentStartedAt.HasValue == producing;
        if (!columnsMatchPhase)
        {
            throw new ArgumentException($"Building {row.BuildingId}: a nullable column is set or null against its phase {row.Phase}.");
        }

        return new Building(
            row.BuildingId,
            new BuildingTypeId(row.TypeId),
            row.X,
            row.Y,
            row.Level,
            row.AssignedWorkers,
            phase,
            row.PhaseStartedAt ?? 0,
            row.PhaseEndsAt,
            row.TargetLevel ?? 0,
            row.CycleLabor ?? 0,
            row.LaborRemaining ?? 0,
            row.SegmentStartedAt ?? 0,
            store.Where(stock => stock.Quantity > 0).Select(stock => new KeyValuePair<ItemId, long>(new ItemId(stock.ItemId), stock.Quantity)),
            needs.Select(need => new KeyValuePair<ItemId, long>(new ItemId(need.ItemId), need.Quantity)));
    }

    /// <summary>
    /// The running research of the <c>cities</c> row and its need rows: no id, times or needs (the slot is free); an id with a start and an
    /// end and no needs (its timer runs); or an id without times and with needs (it waits for goods at the science building, CR-0008).
    /// </summary>
    /// <param name="city">The row.</param>
    /// <param name="needs">The <c>city_research_needs</c> rows.</param>
    /// <returns>The running research, or <see langword="null"/> when the research slot is free.</returns>
    /// <exception cref="ArgumentException">The columns and rows fit none of the three, or the engine rejects the values.</exception>
    private static ActiveResearch? ToActiveResearch(CityRow city, IReadOnlyList<CityResearchNeedRow> needs)
    {
        if (city.ResearchId is null && city.ResearchStartedAt is null && city.ResearchEndsAt is null && needs.Count == 0)
        {
            return null;
        }

        bool timed = city.ResearchStartedAt is not null && city.ResearchEndsAt is not null && needs.Count == 0;
        bool waiting = city.ResearchStartedAt is null && city.ResearchEndsAt is null && needs.Count > 0;
        if (city.ResearchId is null || !(timed || waiting))
        {
            throw new ArgumentException("The running research needs its id with a start and an end, or with need rows and no times.");
        }

        return new ActiveResearch(
            new ResearchId(city.ResearchId),
            city.ResearchStartedAt,
            city.ResearchEndsAt,
            needs.Select(need => new KeyValuePair<ItemId, long>(new ItemId(need.ItemId), need.Quantity)));
    }

    /// <summary>The <c>phase</c> text of a phase.</summary>
    /// <param name="phase">The phase.</param>
    /// <returns>Its text.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The phase is not defined.</exception>
    private static string PhaseText(BuildingPhase phase) => phase switch
    {
        BuildingPhase.Constructing => _constructing,
        BuildingPhase.Upgrading => _upgrading,
        BuildingPhase.Idle => _idle,
        BuildingPhase.Producing => _producing,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Not a defined building phase."),
    };

    /// <summary>The phase a row's <c>phase</c> text names, compared exactly.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The phase.</returns>
    /// <exception cref="ArgumentException">The text names no phase.</exception>
    private static BuildingPhase ParsePhase(CityBuildingRow row) => row.Phase switch
    {
        _constructing => BuildingPhase.Constructing,
        _upgrading => BuildingPhase.Upgrading,
        _idle => BuildingPhase.Idle,
        _producing => BuildingPhase.Producing,
        _ => throw new ArgumentException($"Building {row.BuildingId}: the phase '{row.Phase}' is not one of the defined phases."),
    };

    /// <summary>The completion clock of a completed research.</summary>
    /// <param name="city">The city.</param>
    /// <param name="id">A research the city completed.</param>
    /// <returns>The city-clock second it completed.</returns>
    /// <exception cref="InvalidOperationException">The aggregate has no clock for it.</exception>
    private static long CompletedAt(CityAggregate city, ResearchId id) =>
        city.ResearchCompletedAt.TryGetValue(id, out long completedAt)
            ? completedAt
            : throw new InvalidOperationException(
                $"City {city.CityId}: research {id} completed without a completion clock; advance a city only through CityAggregate.Advance.");

    /// <summary>Checks that every store and need row belongs to a building of the city (the foreign key's rule, checked again on load).</summary>
    /// <param name="rows">The city's rows.</param>
    /// <exception cref="ArgumentException">A store or need row names a building the city doesn't have.</exception>
    private static void RequireKnownBuildings(CityRows rows)
    {
        var ids = rows.Buildings.Select(row => row.BuildingId).ToHashSet();
        int? unknown = rows.Stores.Select(row => row.BuildingId).Concat(rows.ProjectNeeds.Select(row => row.BuildingId))
            .Where(id => !ids.Contains(id))
            .Select(id => (int?)id)
            .FirstOrDefault();
        if (unknown is not null)
        {
            throw new ArgumentException($"A store or need row names building {unknown}, which the city doesn't have.");
        }
    }

    /// <summary>Checks that every child row belongs to the city row.</summary>
    /// <param name="rows">The city's rows.</param>
    /// <exception cref="InvalidOperationException">A child row names another city.</exception>
    private static void RequireOwnRows(CityRows rows)
    {
        Guid cityId = rows.City.Id;
        bool allOwn = rows.Buildings.All(row => row.CityId == cityId)
            && rows.Stores.All(row => row.CityId == cityId)
            && rows.ProjectNeeds.All(row => row.CityId == cityId)
            && rows.Research.All(row => row.CityId == cityId)
            && rows.ResearchNeeds.All(row => row.CityId == cityId)
            && rows.Roads.All(row => row.CityId == cityId)
            && rows.Trips.All(row => row.CityId == cityId);
        if (!allOwn)
        {
            throw new InvalidOperationException($"The rows of city {cityId} include a row of another city.");
        }
    }
}
