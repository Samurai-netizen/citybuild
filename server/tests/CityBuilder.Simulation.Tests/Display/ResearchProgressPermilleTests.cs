using CityBuilder.Simulation.Display;
using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Display;

/// <summary><c>ProgressDisplay.ResearchProgressPermille</c>: the bar of the running research (here 100 s, from 900 to 1000).</summary>
public sealed class ResearchProgressPermilleTests
{
    [Theory]
    [InlineData(850L, 0)] // before the start
    [InlineData(900L, 0)]
    [InlineData(950L, 500)]
    [InlineData(999L, 990)]
    [InlineData(1000L, 1000)]
    [InlineData(9000L, 1000)]
    public void ResearchProgressPermille_RunningResearch_CountsSecondsOfItsDuration(long clock, int expected)
    {
        CityState city = City(activeResearch: new ActiveResearch(new ResearchId("carpentry"), 900, 1000));

        Assert.Equal(expected, ProgressDisplay.ResearchProgressPermille(city, clock));
    }

    [Fact]
    public void ResearchProgressPermille_RoundsDown()
    {
        CityState city = City(activeResearch: new ActiveResearch(new ResearchId("carpentry"), 0, 3));

        Assert.Equal(333, ProgressDisplay.ResearchProgressPermille(city, 1));
    }

    [Fact]
    public void ResearchProgressPermille_NoResearchRunning_IsZero()
    {
        Assert.Equal(0, ProgressDisplay.ResearchProgressPermille(City(), 1000));
    }

    [Fact]
    public void ResearchProgressPermille_NullCity_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ProgressDisplay.ResearchProgressPermille(null!, 1000));
    }
}
