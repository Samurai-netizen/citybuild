using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.State;

/// <summary>The building constructor accepts every phase in its canonical form and rejects fields a phase can't have.</summary>
public sealed class BuildingTests
{
    [Fact]
    public void Constructor_EachPhase_KeepsEveryField()
    {
        Building building = Producing(7, "lumber_camp", 2, 3, level: 2, workers: 3, cycleStartedAt: 100, cycleLabor: 180, laborRemaining: 120,
            segmentStartedAt: 120, endsAt: 160);

        Assert.Equal((7, "lumber_camp", 2, 3, 2, 3), (building.Id, building.TypeId.Value, building.X, building.Y, building.Level, building.AssignedWorkers));
        Assert.Equal((BuildingPhase.Producing, 100L, (long?)160L, 0), (building.Phase, building.PhaseStartedAt, building.PhaseEndsAt, building.TargetLevel));
        Assert.Equal((180L, 120L, 120L), (building.CycleLabor, building.LaborRemaining, building.SegmentStartedAt));
    }

    [Fact]
    public void Constructor_CanonicalPhases_AreAccepted()
    {
        Assert.Equal(BuildingPhase.Constructing, Constructing(1, "hut", 0, 0, startedAt: 10, endsAt: 15).Phase);
        Assert.Equal(BuildingPhase.Upgrading, Upgrading(1, "hut", 0, 0, level: 1, workers: 0, startedAt: 10, endsAt: 18).Phase);
        Assert.Equal(BuildingPhase.Idle, Idle(1, "lumber_camp", 0, 0, workers: 2).Phase);
        Assert.Null(Producing(1, "lumber_camp", 0, 0, 1, workers: 0, cycleStartedAt: 10, cycleLabor: 40, laborRemaining: 40, segmentStartedAt: 10, endsAt: null).PhaseEndsAt);
    }

    private static readonly Dictionary<string, Func<Building>> _invalidBuildings = new()
    {
        ["id 0"] = () => Idle(0, "hut", 0, 0),
        ["negative x"] = () => Idle(1, "hut", -1, 0),
        ["negative y"] = () => Idle(1, "hut", 0, -1),
        ["level 0"] = () => Idle(1, "hut", 0, 0, level: 0),
        ["negative workers"] = () => Idle(1, "lumber_camp", 0, 0, workers: -1),
        ["undefined phase"] = () => new Building(1, new BuildingTypeId("hut"), 0, 0, 1, 0, (BuildingPhase)99, 0, null, 0, 0, 0, 0),
        ["constructing without end"] = () => new Building(1, new BuildingTypeId("hut"), 0, 0, 1, 0, BuildingPhase.Constructing, 10, null, 0, 0, 0, 0),
        ["constructing ends before start"] = () => Constructing(1, "hut", 0, 0, startedAt: 10, endsAt: 9),
        ["constructing with workers"] = () => new Building(1, new BuildingTypeId("lumber_camp"), 0, 0, 1, 1, BuildingPhase.Constructing, 10, 20, 0, 0, 0, 0),
        ["constructing with a cycle"] = () => new Building(1, new BuildingTypeId("lumber_camp"), 0, 0, 1, 0, BuildingPhase.Constructing, 10, 20, 0, 40, 40, 10),
        ["upgrading to the same level"] = () => new Building(1, new BuildingTypeId("hut"), 0, 0, 1, 0, BuildingPhase.Upgrading, 10, 18, 1, 0, 0, 0),
        ["upgrading without end"] = () => new Building(1, new BuildingTypeId("hut"), 0, 0, 1, 0, BuildingPhase.Upgrading, 10, null, 2, 0, 0, 0),
        ["idle with a start"] = () => new Building(1, new BuildingTypeId("hut"), 0, 0, 1, 0, BuildingPhase.Idle, 10, null, 0, 0, 0, 0),
        ["idle with an end"] = () => new Building(1, new BuildingTypeId("hut"), 0, 0, 1, 0, BuildingPhase.Idle, 0, 10, 0, 0, 0, 0),
        ["idle with a target level"] = () => new Building(1, new BuildingTypeId("hut"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 2, 0, 0, 0),
        ["producing without labor"] = () => Producing(1, "lumber_camp", 0, 0, 1, 1, 10, cycleLabor: 0, laborRemaining: 0, segmentStartedAt: 10, endsAt: 10),
        ["producing with more labor left than the cycle"] = () => Producing(1, "lumber_camp", 0, 0, 1, 1, 10, 40, laborRemaining: 41, segmentStartedAt: 10, endsAt: 51),
        ["producing with negative labor left"] = () => Producing(1, "lumber_camp", 0, 0, 1, 1, 10, 40, laborRemaining: -1, segmentStartedAt: 10, endsAt: 10),
        ["producing segment before the cycle"] = () => Producing(1, "lumber_camp", 0, 0, 1, 1, 10, 40, 40, segmentStartedAt: 9, endsAt: 50),
        ["producing ends before the segment"] = () => Producing(1, "lumber_camp", 0, 0, 1, 1, 10, 40, 40, segmentStartedAt: 20, endsAt: 19),
        ["producing with workers but no end"] = () => Producing(1, "lumber_camp", 0, 0, 1, 2, 10, 40, 40, segmentStartedAt: 10, endsAt: null),
        ["producing with no workers but an end"] = () => Producing(1, "lumber_camp", 0, 0, 1, 0, 10, 40, 40, segmentStartedAt: 10, endsAt: 30),
        ["producing with a target level"] = () => new Building(1, new BuildingTypeId("lumber_camp"), 0, 0, 1, 1, BuildingPhase.Producing, 10, 50, 2, 40, 40, 10),
    };

    public static TheoryData<string> InvalidBuildings => new(_invalidBuildings.Keys);

    [Theory]
    [MemberData(nameof(InvalidBuildings))]
    public void Constructor_InvalidFields_Throws(string problem)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(_invalidBuildings[problem]);

        Assert.StartsWith("Building ", error.Message, StringComparison.Ordinal);
    }
}
