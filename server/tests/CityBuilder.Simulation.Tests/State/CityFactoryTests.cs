using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.State;

/// <summary>A new city is exactly the content's starting city.</summary>
public sealed class CityFactoryTests
{
    /// <summary>A new city has the content's starting era and purse, and no goods (CR-0008 D2).</summary>
    [Fact]
    public void CreateStarterCity_ValidContent_HasTheStartingEraAndPurse()
    {
        CityState city = CityFactory.CreateStarterCity(TestContent);

        Assert.Equal((1, 100L), (city.Era, city.Purse));
        Assert.Equal(0, CityQueries.Total(city, TestContent, new ItemId("wood")));
    }

    /// <summary>A new city starts at clock 0 with next id 1, no buildings, no research.</summary>
    [Fact]
    public void CreateStarterCity_ValidContent_StartsAtClockZeroWithNothingBuiltOrResearched()
    {
        CityState city = CityFactory.CreateStarterCity(TestContent);

        Assert.Equal((0L, 1), (city.Clock, city.NextBuildingId));
        Assert.Empty(city.Buildings);
        Assert.Empty(city.CompletedResearch);
        Assert.Null(city.ActiveResearch);
    }

    /// <summary>Null content is a programmer error and throws.</summary>
    [Fact]
    public void CreateStarterCity_NullContent_Throws()
    {
        // The null is deliberate: missing content is a programmer error.
        Assert.Throws<ArgumentNullException>(() => CityFactory.CreateStarterCity(null!));
    }
}
