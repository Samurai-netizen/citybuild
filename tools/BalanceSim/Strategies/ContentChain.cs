using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;

namespace CityBuilder.BalanceSim.Strategies;

/// <summary>
/// What the strategies read from the content instead of naming ids (core-instructions/02-principles.md, rule 8): the coin chain (the
/// producers whose recipe makes the currency, and, step by step, the producers of their inputs), and the house, science building and
/// courier station types. Recipes are read at level 1; in M0 a level only adds worker slots (core-instructions/23-mvp-content.md → Recipes).
/// </summary>
public sealed class ContentChain
{
    /// <summary>Creates the chain; only <see cref="Of"/> does.</summary>
    /// <param name="chainTypes">The chain's producer types, raw first.</param>
    /// <param name="coinProducers">The types whose recipe outputs the currency.</param>
    /// <param name="house">The first house type, or null.</param>
    /// <param name="science">The science building type, or null.</param>
    /// <param name="station">The first courier station type, or null.</param>
    private ContentChain(
        IReadOnlyList<BuildingDefinition> chainTypes,
        IReadOnlyList<BuildingDefinition> coinProducers,
        BuildingDefinition? house,
        BuildingDefinition? science,
        BuildingDefinition? station)
    {
        ChainTypes = chainTypes;
        CoinProducers = coinProducers;
        House = house;
        Science = science;
        Station = station;
    }

    /// <summary>
    /// The coin chain's producer types, raw goods first: deepest step first (the producers furthest from the currency), then content
    /// order. For M0: Woodcutter's Hut, Sawmill, Trading Post.
    /// </summary>
    public IReadOnlyList<BuildingDefinition> ChainTypes { get; }

    /// <summary>The producer types whose recipe outputs the currency (M0: the Trading Post).</summary>
    public IReadOnlyList<BuildingDefinition> CoinProducers { get; }

    /// <summary>The first house type in content order, or null.</summary>
    public BuildingDefinition? House { get; }

    /// <summary>The science building type, or null.</summary>
    public BuildingDefinition? Science { get; }

    /// <summary>The first courier station type in content order, or null.</summary>
    public BuildingDefinition? Station { get; }

    /// <summary>Reads the chain from a content.</summary>
    /// <param name="content">The game content.</param>
    /// <returns>The chain.</returns>
    public static ContentChain Of(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        BuildingDefinition[] producers = content.Buildings.Where(building => building.Kind == BuildingKind.Producer).ToArray();
        BuildingDefinition[] coinProducers = producers.Where(building => Outputs(building).Contains(content.Currency)).ToArray();

        // Breadth-first from the coin producers: a type's depth is the fewest recipe steps between it and the currency.
        var depth = new Dictionary<BuildingTypeId, int>();
        foreach (BuildingDefinition producer in coinProducers)
        {
            depth[producer.Id] = 0;
        }

        var frontier = new List<BuildingDefinition>(coinProducers);
        for (int step = 1; frontier.Count > 0; step++)
        {
            var next = new List<BuildingDefinition>();
            foreach (ItemId input in frontier.SelectMany(Inputs).Distinct())
            {
                foreach (BuildingDefinition producer in producers.Where(candidate => Outputs(candidate).Contains(input)))
                {
                    if (depth.TryAdd(producer.Id, step))
                    {
                        next.Add(producer);
                    }
                }
            }

            frontier = next;
        }

        // Content order breaks ties; OrderByDescending is a stable sort.
        BuildingDefinition[] chain = producers.Where(producer => depth.ContainsKey(producer.Id))
            .OrderByDescending(producer => depth[producer.Id]).ToArray();
        return new ContentChain(
            chain,
            coinProducers,
            content.Buildings.FirstOrDefault(building => building.Kind == BuildingKind.House),
            content.Buildings.FirstOrDefault(building => building.Kind == BuildingKind.Science),
            content.Buildings.FirstOrDefault(building => building.Kind == BuildingKind.CourierStation));
    }

    /// <summary>A type's place in <see cref="ChainTypes"/>, or the chain's length for a type outside it (so it sorts after the chain).</summary>
    /// <param name="type">The type.</param>
    /// <returns>The index.</returns>
    public int IndexOf(BuildingTypeId type)
    {
        for (int index = 0; index < ChainTypes.Count; index++)
        {
            if (ChainTypes[index].Id == type)
            {
                return index;
            }
        }

        return ChainTypes.Count;
    }

    /// <summary>Whether a type is in the coin chain.</summary>
    /// <param name="type">The type.</param>
    /// <returns>True for a chain type.</returns>
    public bool IsChainType(BuildingTypeId type) => IndexOf(type) < ChainTypes.Count;

    /// <summary>
    /// Whether a research node helps the coin chain: it unlocks a chain type or reduces a chain type's labor. GreedyCoins researches only these.
    /// </summary>
    /// <param name="research">The node.</param>
    /// <returns>True when it helps.</returns>
    public bool HelpsCoins(ResearchDefinition research) => research.Effects.Any(effect => effect switch
    {
        UnlockBuildingEffect unlock => IsChainType(unlock.Building),
        LaborReductionEffect labor => IsChainType(labor.Building),
        _ => false,
    });

    /// <summary>The inputs of a type's level-1 recipe.</summary>
    /// <param name="building">The type.</param>
    /// <returns>The input items, in recipe order; none without a recipe.</returns>
    public static IEnumerable<ItemId> Inputs(BuildingDefinition building) =>
        building.GetLevel(1).Recipe?.Inputs.Select(input => input.Item) ?? [];

    /// <summary>The outputs of a type's level-1 recipe.</summary>
    /// <param name="building">The type.</param>
    /// <returns>The output items, in recipe order; none without a recipe.</returns>
    public static IEnumerable<ItemId> Outputs(BuildingDefinition building) =>
        building.GetLevel(1).Recipe?.Outputs.Select(output => output.Item) ?? [];
}
