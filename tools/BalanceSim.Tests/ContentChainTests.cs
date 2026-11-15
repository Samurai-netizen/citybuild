using CityBuilder.BalanceSim.Strategies;
using CityBuilder.Simulation.Content;

namespace CityBuilder.BalanceSim.Tests;

/// <summary>What the strategies read from the real content: the coin chain and the special building types.</summary>
public sealed class ContentChainTests
{
    /// <summary>
    /// The chain ends with the coin producers, and every earlier type's output is an input of a later one: raw goods first. Checked as rules,
    /// not as copied ids, so a content change that keeps the shape keeps the test green.
    /// </summary>
    [Fact]
    public void Chain_RunsFromRawGoodsToTheCurrency()
    {
        GameContent content = TestContent.Mvp;
        ContentChain chain = ContentChain.Of(content);

        Assert.NotEmpty(chain.CoinProducers);
        Assert.Contains(content.Currency, ContentChain.Outputs(chain.ChainTypes[^1]));
        for (int index = 0; index < chain.ChainTypes.Count - 1; index++)
        {
            BuildingDefinition type = chain.ChainTypes[index];
            Assert.True(
                chain.ChainTypes.Skip(index + 1).Any(later => ContentChain.Inputs(later).Intersect(ContentChain.Outputs(type)).Any()),
                $"{type.Id} feeds no later chain type.");
        }
    }

    /// <summary>The house, science and courier station types are found by kind.</summary>
    [Fact]
    public void SpecialTypes_AreFoundByKind()
    {
        ContentChain chain = ContentChain.Of(TestContent.Mvp);

        Assert.Equal(BuildingKind.House, chain.House?.Kind);
        Assert.Equal(BuildingKind.Science, chain.Science?.Kind);
        Assert.Equal(BuildingKind.CourierStation, chain.Station?.Kind);
    }

    /// <summary>A research node helps coins when it unlocks or speeds up a chain type; the chain needs at least one such node in M0 (Trade Charter).</summary>
    [Fact]
    public void HelpsCoins_FindsTheUnlockOfTheCoinProducer()
    {
        GameContent content = TestContent.Mvp;
        ContentChain chain = ContentChain.Of(content);

        Assert.Contains(content.Research, node => chain.HelpsCoins(node)
            && node.Effects.OfType<UnlockBuildingEffect>().Any(unlock => chain.CoinProducers.Any(type => type.Id == unlock.Building)));
    }
}
