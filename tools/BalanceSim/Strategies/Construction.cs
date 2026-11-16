using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.BalanceSim.Strategies;

/// <summary>
/// The building choices the strategies share (docs/modules/BalanceSim.md → Strategies): what to place next, where, and what to upgrade.
/// Every choice comes from the content (the chain, recipe rates, worker slots), never from ids.
/// </summary>
public static class Construction
{
    /// <summary>The rate scale: rates are goods per hour × 1000, so integer rates keep three decimals.</summary>
    private const long _rateScale = 3_600_000;

    /// <summary>
    /// The next building of the basic city, or null when it stands: the science building (when research is wanted), then one of every unlocked
    /// chain type (raw first), then a courier station once every chain type has one (so the coin producer comes first, even while research
    /// still has to unlock it). A house comes first when the workers would not
    /// cover it (<see cref="WithHouseIfShort"/>).
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <param name="wantScience">Whether the strategy wants research (and so the science building).</param>
    /// <returns>The type to place, or null.</returns>
    public static BuildingDefinition? NextBasic(Turn turn, bool wantScience)
    {
        BuildingDefinition? next = null;
        ContentChain chain = turn.Chain;
        if (wantScience && chain.Science is { } science && Count(turn, science) == 0 && IsUnlocked(turn, science))
        {
            next = science;
        }

        next ??= chain.ChainTypes.FirstOrDefault(type => IsUnlocked(turn, type) && Count(turn, type) == 0);
        if (next is null && chain.Station is { } station && Count(turn, station) == 0 && IsUnlocked(turn, station)
            && chain.ChainTypes.All(type => Count(turn, type) > 0))
        {
            next = station;
        }

        return next is null ? null : WithHouseIfShort(turn, next);
    }

    /// <summary>
    /// The next building that grows the chain: another courier station when the routes can't hold one more chain building and no station
    /// can be upgraded (<see cref="IsRouteCapacityShort"/>), else the producer of the first good (raw first) whose demand at full staffing is
    /// above its supply, else another coin producer. A house first when the workers would not cover it.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <returns>The type to place, or null when no chain type is unlocked.</returns>
    public static BuildingDefinition? NextExpansion(Turn turn)
    {
        BuildingDefinition? station = turn.Chain.Station;
        BuildingDefinition? next = IsRouteCapacityShort(turn) && !turn.City.Buildings.Any(building => building.TypeId == station!.Id && CanUpgrade(turn, building))
                && IsUnlocked(turn, station!) && BelowLimit(turn, station!)
            ? station
            : DeficitProducer(turn) ?? turn.Chain.CoinProducers.FirstOrDefault(type => IsUnlocked(turn, type) && BelowLimit(turn, type));
        return next is null ? null : WithHouseIfShort(turn, next);
    }

    /// <summary>
    /// The courier station to upgrade because the routes can't hold one more chain building (<see cref="IsRouteCapacityShort"/>), or null.
    /// Every strategy orders it before growing the chain.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <returns>The station, or null.</returns>
    public static Building? NextStationUpgrade(Turn turn) => IsRouteCapacityShort(turn)
        ? turn.City.Buildings.Where(building => turn.DefinitionOf(building).Kind == BuildingKind.CourierStation && CanUpgrade(turn, building))
            .OrderBy(building => building.Level).ThenBy(building => building.Id).FirstOrDefault()
        : null;

    /// <summary>
    /// Whether one more chain building would not fit on the routes: there is a courier station, and the routes the chain would need with one
    /// more building per good (<see cref="Routes.NeededWithOneMore"/>) are more than the stations' couriers at full staffing, but one, can run.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <returns>True when couriers, not producers, are the next need.</returns>
    public static bool IsRouteCapacityShort(Turn turn)
    {
        Building[] stations = [.. turn.City.Buildings.Where(building => turn.DefinitionOf(building).Kind == BuildingKind.CourierStation)];
        if (stations.Length == 0)
        {
            return false;
        }

        long couriers = stations.Sum(station => (long)turn.DefinitionOf(station).GetLevel(station.Level).MaxWorkers);
        return Routes.NeededWithOneMore(turn) > couriers - 1;
    }

    /// <summary>
    /// The next upgrade, or null: a chain building (those of the type in deficit first, then by lowest level, chain order and id), else a
    /// courier station, a house, any other building. When the chain building's extra worker slots would not be covered, a house upgrade
    /// instead (or null when none can be upgraded: the placement rules then add a house).
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <returns>The building to upgrade, or null.</returns>
    public static Building? NextUpgrade(Turn turn)
    {
        Building[] upgradable = turn.City.Buildings.Where(building => CanUpgrade(turn, building)).ToArray();
        BuildingDefinition? deficit = DeficitProducer(turn);
        Building? chainCandidate = upgradable
            .Where(building => turn.Chain.IsChainType(building.TypeId))
            .OrderBy(building => deficit is not null && building.TypeId == deficit.Id ? 0 : 1)
            .ThenBy(building => building.Level)
            .ThenBy(building => turn.Chain.IndexOf(building.TypeId))
            .ThenBy(building => building.Id)
            .FirstOrDefault();
        Building? house = upgradable.Where(building => turn.DefinitionOf(building).Kind == BuildingKind.House)
            .OrderBy(building => building.Level).ThenBy(building => building.Id).FirstOrDefault();
        if (chainCandidate is not null)
        {
            BuildingDefinition definition = turn.DefinitionOf(chainCandidate);
            long added = definition.GetLevel(chainCandidate.Level + 1).MaxWorkers - definition.GetLevel(chainCandidate.Level).MaxWorkers;
            return added > ProjectedFreeWorkers(turn) ? house : chainCandidate;
        }

        return upgradable.Where(building => turn.DefinitionOf(building).Kind == BuildingKind.CourierStation)
                .OrderBy(building => building.Level).ThenBy(building => building.Id).FirstOrDefault()
            ?? house
            ?? upgradable.OrderBy(building => building.Level).ThenBy(building => building.Id).FirstOrDefault();
    }

    /// <summary>
    /// The coins kept back for the basic city: the coins of every chain type the city has none of yet, locked ones included (M0: the Trading
    /// Post while Trade Charter runs). Growth, upgrades and research that doesn't unlock one of them spend only above it, so the first coin
    /// producer is never starved.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <returns>The coins.</returns>
    public static long BasicReserve(Turn turn) =>
        turn.Chain.ChainTypes.Where(type => Count(turn, type) == 0).Sum(type => turn.CoinsOf(type.Cost));

    /// <summary>
    /// Places a building of a type on the first free tile, when a builder is free and the purse covers its coins above a reserve. A cost with
    /// goods waits while another project waits for goods, so the builder slots are never all blocked by deliveries.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <param name="type">The type.</param>
    /// <param name="reserve">Coins the strategy keeps back.</param>
    /// <returns>Whether it was placed.</returns>
    public static bool TryPlace(Turn turn, BuildingDefinition type, long reserve)
    {
        if (!turn.HasFreeBuilder() || turn.City.Purse - reserve < turn.CoinsOf(type.Cost) || FreeTile(turn) is not { } tile
            || (HasGoods(turn, type.Cost) && IsAnyProjectWaiting(turn)))
        {
            return false;
        }

        return turn.Actions.Place(type.Id, tile.X, tile.Y);
    }

    /// <summary>
    /// Upgrades a building, when a builder is free and the purse covers the upgrade's coins above a reserve; a cost with goods waits as in
    /// <see cref="TryPlace"/>, and also while a chain type is still missing, so the basic city gets the goods as well as the coins (M0-A).
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <param name="building">The building.</param>
    /// <param name="reserve">Coins the strategy keeps back.</param>
    /// <returns>Whether the upgrade was ordered.</returns>
    public static bool TryUpgrade(Turn turn, Building building, long reserve)
    {
        BuildingLevelDefinition next = turn.DefinitionOf(building).GetLevel(building.Level + 1);
        bool needsGoods = HasGoods(turn, next.UpgradeCost);
        if (!turn.HasFreeBuilder() || turn.City.Purse - reserve < turn.CoinsOf(next.UpgradeCost)
            || (needsGoods && (IsAnyProjectWaiting(turn) || turn.Chain.ChainTypes.Any(type => Count(turn, type) == 0))))
        {
            return false;
        }

        return turn.Actions.Upgrade(building.Id);
    }

    /// <summary>The first free tile in row-major order (row 0 first, then by column), or null when the grid is full.</summary>
    /// <param name="turn">The turn.</param>
    /// <returns>The tile, or null.</returns>
    public static (int X, int Y)? FreeTile(Turn turn)
    {
        for (int y = 0; y < turn.Content.Rules.GridHeight; y++)
        {
            for (int x = 0; x < turn.Content.Rules.GridWidth; x++)
            {
                if (CityQueries.TileAt(turn.City, x, y) == TileContent.Ground)
                {
                    return (x, y);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The workers still uncovered when every producer and courier station is fully staffed: the workers of every house (a constructing one
    /// at level 1), minus every producer's and station's worker slots, minus one worker kept free for hand deliveries until a courier station
    /// is built (<see cref="Workforce"/>).
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <returns>The workers; below 0 when the slots outnumber them.</returns>
    public static long ProjectedFreeWorkers(Turn turn)
    {
        long free = 0;
        foreach (Building building in turn.City.Buildings)
        {
            BuildingLevelDefinition level = turn.DefinitionOf(building).GetLevel(building.Level);
            free += level.WorkersProvided;
            if (turn.DefinitionOf(building).Kind is BuildingKind.Producer or BuildingKind.CourierStation)
            {
                free -= level.MaxWorkers;
            }
        }

        return free - Workforce.HandDeliveryReserve(turn);
    }

    /// <summary>A house instead of <paramref name="type"/> when the workers would not cover its level-1 slots and a house can be placed.</summary>
    /// <param name="turn">The turn.</param>
    /// <param name="type">The type the strategy wants.</param>
    /// <returns>The house type or <paramref name="type"/>.</returns>
    public static BuildingDefinition WithHouseIfShort(Turn turn, BuildingDefinition type)
    {
        BuildingDefinition? house = turn.Chain.House;
        bool isShort = type.GetLevel(1).MaxWorkers > ProjectedFreeWorkers(turn);
        return isShort && house is not null && type.Kind != BuildingKind.House && IsUnlocked(turn, house) && BelowLimit(turn, house) ? house : type;
    }

    /// <summary>
    /// The chain producer of the first good, raw first, whose demand (the inputs of its consumers at full staffing) is above its supply (the
    /// outputs of its producers at full staffing), or null when no unlocked producer can fix a deficit.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <returns>The producer type, or null.</returns>
    public static BuildingDefinition? DeficitProducer(Turn turn)
    {
        foreach (ItemId item in turn.Chain.ChainTypes.SelectMany(ContentChain.Outputs).Where(item => item != turn.Content.Currency).Distinct())
        {
            if (Demand(turn, item) > Supply(turn, item))
            {
                BuildingDefinition? producer = turn.Chain.ChainTypes.FirstOrDefault(
                    type => ContentChain.Outputs(type).Contains(item) && IsUnlocked(turn, type) && BelowLimit(turn, type));
                if (producer is not null)
                {
                    return producer;
                }
            }
        }

        return null;
    }

    /// <summary>A good's supply: every producer's output rate at full staffing at its level (×1000 per hour).</summary>
    /// <param name="turn">The turn.</param>
    /// <param name="item">The good.</param>
    /// <returns>The rate.</returns>
    public static long Supply(Turn turn, ItemId item) => Rate(turn, item, recipe => recipe.Outputs);

    /// <summary>A good's demand: every consumer's input rate at full staffing at its level (×1000 per hour).</summary>
    /// <param name="turn">The turn.</param>
    /// <param name="item">The good.</param>
    /// <returns>The rate.</returns>
    public static long Demand(Turn turn, ItemId item) => Rate(turn, item, recipe => recipe.Inputs);

    /// <summary>
    /// Whether a building can be upgraded now: idle or producing, no upgrade waiting, below its type's max level, and every good of the
    /// upgrade's cost made or held by another building (the only way it can arrive).
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <param name="building">The building.</param>
    /// <returns>True when an upgrade may be ordered (builders and coins aside).</returns>
    public static bool CanUpgrade(Turn turn, Building building)
    {
        BuildingDefinition definition = turn.DefinitionOf(building);
        if (building.Phase is not (BuildingPhase.Idle or BuildingPhase.Producing) || building.HasUpgradeWaiting || building.Level >= definition.MaxLevel)
        {
            return false;
        }

        // A building can't deliver to itself, and while it waits it gets no workers: every good of the upgrade must be able to reach it
        // without this building (M0-A: the hut's planks come from the Sawmill, whose logs come from the hut).
        return definition.GetLevel(building.Level + 1).UpgradeCost.Where(part => part.Item != turn.Content.Currency)
            .All(part => CanSupplyWithout(turn, part.Item, part.Quantity, building.Id, []));
    }

    /// <summary>
    /// Whether an amount of a good can reach a project without one building: what the other buildings hold, plus what one of them makes
    /// from inputs that can each be supplied the same way (its level-1 recipe, whole cycles). A good already being followed counts as no
    /// supply, so a cycle in the chain ends the search.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <param name="item">The good.</param>
    /// <param name="amount">How many are needed.</param>
    /// <param name="excluded">The building id left out (the one that would wait for the good).</param>
    /// <param name="following">The goods on the current path.</param>
    /// <returns>True when the amount can come from elsewhere.</returns>
    private static bool CanSupplyWithout(Turn turn, ItemId item, long amount, int excluded, HashSet<ItemId> following)
    {
        Building[] others = [.. turn.City.Buildings.Where(other => other.Id != excluded)];
        long missing = amount - others.Sum(other => other.GetStock(item));
        if (missing <= 0)
        {
            return true;
        }

        if (!following.Add(item))
        {
            return false;
        }

        bool supplied = others.Any(other => turn.DefinitionOf(other).GetLevel(1).Recipe is { } recipe
            && recipe.Outputs.FirstOrDefault(output => output.Item == item) is { } made
            && recipe.Inputs.All(input => CanSupplyWithout(
                turn, input.Item, checked(IntegerMath.CeilDiv(missing, made.Quantity) * input.Quantity), excluded, following)));
        following.Remove(item);
        return supplied;
    }

    /// <summary>Whether the city may place a type now (its era and research).</summary>
    /// <param name="turn">The turn.</param>
    /// <param name="type">The type.</param>
    /// <returns>True when unlocked.</returns>
    public static bool IsUnlocked(Turn turn, BuildingDefinition type) => CityQueries.IsUnlocked(turn.City, turn.Content, type.Id);

    /// <summary>How many buildings of a type the city has, in any phase.</summary>
    /// <param name="turn">The turn.</param>
    /// <param name="type">The type.</param>
    /// <returns>The count.</returns>
    public static int Count(Turn turn, BuildingDefinition type) => CityQueries.CountOfType(turn.City, type.Id);

    /// <summary>Whether a cost has goods besides coins.</summary>
    /// <param name="turn">The turn.</param>
    /// <param name="cost">The cost.</param>
    /// <returns>True when it has.</returns>
    private static bool HasGoods(Turn turn, IReadOnlyList<ItemQuantity> cost) => cost.Any(part => part.Item != turn.Content.Currency);

    /// <summary>Whether a construction site or a waiting upgrade is waiting for goods.</summary>
    /// <param name="turn">The turn.</param>
    /// <returns>True when one is.</returns>
    private static bool IsAnyProjectWaiting(Turn turn) =>
        turn.City.Buildings.Any(building => building.IsConstructionSite || building.HasUpgradeWaiting);

    /// <summary>Whether one more of a type stays within its <c>maxPerCity</c>.</summary>
    /// <param name="turn">The turn.</param>
    /// <param name="type">The type.</param>
    /// <returns>True when another may be placed.</returns>
    private static bool BelowLimit(Turn turn, BuildingDefinition type) => !type.MaxPerCity.HasValue || Count(turn, type) < type.MaxPerCity.Value;


    /// <summary>
    /// The sum over every producer of a recipe side's quantity of a good × its level's worker slots / its effective labor (research applied),
    /// scaled by <see cref="_rateScale"/>: the goods per hour × 1000 at full staffing.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <param name="item">The good.</param>
    /// <param name="side">The recipe side: inputs or outputs.</param>
    /// <returns>The rate.</returns>
    private static long Rate(Turn turn, ItemId item, Func<RecipeDefinition, IReadOnlyList<ItemQuantity>> side)
    {
        long rate = 0;
        foreach (Building building in turn.City.Buildings)
        {
            BuildingLevelDefinition level = turn.DefinitionOf(building).GetLevel(building.Level);
            if (level.Recipe is not { } recipe)
            {
                continue;
            }

            long quantity = side(recipe).Where(part => part.Item == item).Sum(part => part.Quantity);
            long labor = CityQueries.EffectiveLabor(turn.City, turn.Content, building.TypeId, building.Level);
            if (quantity > 0 && labor > 0)
            {
                rate = checked(rate + (quantity * level.MaxWorkers * _rateScale / labor));
            }
        }

        return rate;
    }
}
