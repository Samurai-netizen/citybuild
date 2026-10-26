using CityBuilder.Application.Cities;
using CityBuilder.Infrastructure.Persistence.Rows;
using CityBuilder.Simulation;
using CityBuilder.Simulation.State;

namespace CityBuilder.Infrastructure.Persistence.Mapping;

/// <summary>
/// Maps a <see cref="CityAggregate"/> to its rows and back, explicitly and without a database (<c>core-instructions/06-data-model.md</c>,
/// EF Core conventions). Only the engine's own state is stored; reserved outputs, stall reasons, workers provided and free, builders busy,
/// effective caps and labor are derived from it after load. A building's nullable columns are null exactly when its phase doesn't use them
/// (<see cref="ToBuildingRow"/>). Loading rows that break a rule of the engine or of this mapping throws, so a broken row is never
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
    /// times (<c>created_at</c>, <c>updated_at</c>) are left at their default for the repository to set. Inventory rows exist only for the
    /// items the city holds (the engine drops an item at 0).
    /// </summary>
    /// <param name="city">The city.</param>
    /// <returns>The rows, children in the engine's order (items and research by id, buildings by id).</returns>
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
            ResearchId = active?.Id.Value,
            ResearchStartedAt = active?.StartedAt,
            ResearchEndsAt = active?.EndsAt,
            StateVersion = city.StateVersion,
            ContentVersion = city.ContentVersion,
        };

        var inventory = state.Inventory
            .Select(stock => new CityInventoryRow { CityId = city.CityId, ItemId = stock.Key.Value, Quantity = stock.Value })
            .ToList();
        var buildings = state.Buildings.Select(building => ToBuildingRow(city.CityId, building)).ToList();
        var research = state.CompletedResearch
            .Select(id => new CityResearchRow { CityId = city.CityId, ResearchId = id.Value, CompletedAt = CompletedAt(city, id) })
            .ToList();

        return new CityRows(cityRow, inventory, buildings, research);
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
            var state = new CityState(
                city.ClockSeconds,
                city.Era,
                city.NextBuildingId,
                rows.Inventory.Select(row => new KeyValuePair<ItemId, long>(new ItemId(row.ItemId), row.Quantity)),
                rows.Buildings.Select(ToBuilding),
                rows.Research.Select(row => new ResearchId(row.ResearchId)),
                ToActiveResearch(city));
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
    /// uses the start and the end; Upgrading the start, the end and the target level; Idle none; Producing the start, the cycle labor, the
    /// labor remaining and the segment start, and the end unless the cycle is frozen (no workers). A cycle can start at clock 0, so 0 is a
    /// real value, never a stand-in for null.
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
            PhaseStartedAt = phase == BuildingPhase.Idle ? null : building.PhaseStartedAt,
            PhaseEndsAt = building.PhaseEndsAt,
            TargetLevel = phase == BuildingPhase.Upgrading ? checked((short)building.TargetLevel) : null,
            CycleLabor = producing ? building.CycleLabor : null,
            LaborRemaining = producing ? building.LaborRemaining : null,
            SegmentStartedAt = producing ? building.SegmentStartedAt : null,
        };
    }

    /// <summary>
    /// The building of a row, after checking that each nullable column is null exactly when the phase doesn't use it
    /// (<see cref="ToBuildingRow"/>); the engine's constructor checks the rest.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The building.</returns>
    /// <exception cref="ArgumentException">The row breaks a rule; the message names the building.</exception>
    private static Building ToBuilding(CityBuildingRow row)
    {
        BuildingPhase phase = ParsePhase(row);
        bool producing = phase == BuildingPhase.Producing;
        bool endRequired = phase is BuildingPhase.Constructing or BuildingPhase.Upgrading;
        bool columnsMatchPhase =
            row.PhaseStartedAt.HasValue == (phase != BuildingPhase.Idle)
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
            row.SegmentStartedAt ?? 0);
    }

    /// <summary>The running research of the <c>cities</c> row: its three columns are all set or all null.</summary>
    /// <param name="city">The row.</param>
    /// <returns>The running research, or <see langword="null"/> when the research slot is free.</returns>
    /// <exception cref="ArgumentException">Only some of the three columns are set, or the engine rejects the values.</exception>
    private static ActiveResearch? ToActiveResearch(CityRow city)
    {
        if (city.ResearchId is null && city.ResearchStartedAt is null && city.ResearchEndsAt is null)
        {
            return null;
        }

        if (city.ResearchId is null || city.ResearchStartedAt is null || city.ResearchEndsAt is null)
        {
            throw new ArgumentException("The running research needs its id, start and end together.");
        }

        return new ActiveResearch(new ResearchId(city.ResearchId), city.ResearchStartedAt.Value, city.ResearchEndsAt.Value);
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

    /// <summary>Checks that every child row belongs to the city row.</summary>
    /// <param name="rows">The city's rows.</param>
    /// <exception cref="InvalidOperationException">A child row names another city.</exception>
    private static void RequireOwnRows(CityRows rows)
    {
        Guid cityId = rows.City.Id;
        bool allOwn = rows.Inventory.All(row => row.CityId == cityId)
            && rows.Buildings.All(row => row.CityId == cityId)
            && rows.Research.All(row => row.CityId == cityId);
        if (!allOwn)
        {
            throw new InvalidOperationException($"The rows of city {cityId} include a row of another city.");
        }
    }
}
