using CityBuilder.Simulation.Display;
using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.Display;

/// <summary><c>ProgressDisplay.ResearchProgressPermille</c>: the bar of the running research (here 100 s, from 900 to 1000).</summary>
public sealed class ResearchProgressPermilleTests
{
    /// <summary>The research bar counts seconds of its 100 s duration: 0 before the start, 500 halfway, full at and after the end.</summary>
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

    /// <summary>The research bar rounds down: 1 of 3 s is 333.</summary>
    [Fact]
    public void ResearchProgressPermille_RoundsDown()
    {
        CityState city = City(activeResearch: new ActiveResearch(new ResearchId("carpentry"), 0, 3));

        Assert.Equal(333, ProgressDisplay.ResearchProgressPermille(city, 1));
    }

    /// <summary>With no research running the bar is 0.</summary>
    [Fact]
    public void ResearchProgressPermille_NoResearchRunning_IsZero()
    {
        Assert.Equal(0, ProgressDisplay.ResearchProgressPermille(City(), 1000));
    }

    /// <summary>A null city throws.</summary>
    [Fact]
    public void ResearchProgressPermille_NullCity_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ProgressDisplay.ResearchProgressPermille(null!, 1000));
    }
}
