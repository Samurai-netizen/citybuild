using CityBuilder.Simulation.State;
using static CityBuilder.Simulation.Tests.State.CityFixture;

namespace CityBuilder.Simulation.Tests.State;

/// <summary>A new city is exactly the content's starting city.</summary>
public sealed class CityFactoryTests
{
    /// <summary>A new city has the content's starting era and stock.</summary>
    [Fact]
    public void CreateStarterCity_ValidContent_HasTheStartingEraAndStock()
    {
        CityState city = CityFactory.CreateStarterCity(TestContent);

        Assert.Equal(1, city.Era);
        Assert.Equal([("gold", 100L), ("wood", 5L)], city.Inventory.Select(stock => (stock.Key.Value, stock.Value)));
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
