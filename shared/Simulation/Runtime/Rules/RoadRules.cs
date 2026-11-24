#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Rules
{
    /// <summary>
    /// The player's road mutations (<c>core-instructions/04-simulation-model.md</c>, sections 8 and 17; CR-0008 D6): a list of tiles is placed,
    /// upgraded or removed all at once, instantly, without a builder slot, for coins only. Each runs at the city's clock after the caller has
    /// settled the city; the caller then runs a zero-length advance. Each checks in the order of section 8 over the whole list and returns the
    /// first failure; a rejected mutation changes nothing. An applied one names what it paid (<see cref="MutationResult.Spent"/>).
    /// </summary>
    public static class RoadRules
    {
        /// <summary>
        /// Builds a level-1 road on every tile of the list and pays level 1's cost once per tile; on a water tile the road is a bridge and
        /// costs the bridge's cost (M1-03). The checks run over the whole list: every tile in the grid, then none on forest or rock, then
        /// every tile on open land, then the bridge unlocked if a tile is water, then every tile empty, then the total cost.
        /// </summary>
        /// <param name="city">The city, settled to now.</param>
        /// <param name="content">The game content: the grid and the road levels.</param>
        /// <param name="tiles">The tiles, each once, at least one.</param>
        /// <returns>
        /// Ok, or the first failed check: <see cref="MutationErrorCode.InvalidArgument"/> (no tile, or a tile twice),
        /// <see cref="MutationErrorCode.OutOfBounds"/>, <see cref="MutationErrorCode.TerrainBlocked"/>, <see cref="MutationErrorCode.LandClosed"/>,
        /// <see cref="MutationErrorCode.BridgeLocked"/>, <see cref="MutationErrorCode.TileOccupied"/> (a building or a road),
        /// <see cref="MutationErrorCode.InsufficientResource"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static MutationResult PlaceRoads(CityState city, GameContent content, IReadOnlyList<(int X, int Y)> tiles)
        {
            RequireArguments(city, content, tiles);
            if (!IsAListOfDistinctTiles(tiles))
            {
                return MutationResult.Rejected(MutationErrorCode.InvalidArgument);
            }

            for (int index = 0; index < tiles.Count; index++)
            {
                (int x, int y) = tiles[index];
                if (x < 0 || y < 0 || x >= content.Rules.GridWidth || y >= content.Rules.GridHeight)
                {
                    return MutationResult.Rejected(MutationErrorCode.OutOfBounds);
                }
            }

            // The map's rules (M1-03), each over the whole list before the next: the terrain never changes (OD-13 may refuse it before the
            // lock), the land opens with research, and the bridge is unlocked by research.
            for (int index = 0; index < tiles.Count; index++)
            {
                Terrain terrain = content.TerrainAt(tiles[index].X, tiles[index].Y);
                if (terrain == Terrain.Forest || terrain == Terrain.Rock)
                {
                    return MutationResult.Rejected(MutationErrorCode.TerrainBlocked);
                }
            }

            for (int index = 0; index < tiles.Count; index++)
            {
                if (!CityQueries.IsLandOpen(city, content, tiles[index].X, tiles[index].Y))
                {
                    return MutationResult.Rejected(MutationErrorCode.LandClosed);
                }
            }

            for (int index = 0; index < tiles.Count; index++)
            {
                if (content.TerrainAt(tiles[index].X, tiles[index].Y) == Terrain.Water && !CityQueries.IsBridgeUnlocked(city, content))
                {
                    return MutationResult.Rejected(MutationErrorCode.BridgeLocked);
                }
            }

            for (int index = 0; index < tiles.Count; index++)
            {
                if (CityQueries.TileAt(city, tiles[index].X, tiles[index].Y) != TileContent.Ground)
                {
                    return MutationResult.Rejected(MutationErrorCode.TileOccupied);
                }
            }

            var cost = new ItemTotals();
            for (int index = 0; index < tiles.Count; index++)
            {
                // A bridge is unlocked only where the content has one (content rule 18), so a water tile here has the bridge's cost.
                bool bridge = content.TerrainAt(tiles[index].X, tiles[index].Y) == Terrain.Water;
                AddAmounts(cost, bridge ? content.Bridge!.Cost : content.GetRoadLevel(1).Cost);
            }

            // Content rule 11 makes every road cost the currency only, so the cost is coins, paid from the purse.
            long coins = Costs.CoinsOf(content, cost.ToQuantities());
            if (city.Purse < coins)
            {
                return MutationResult.Rejected(MutationErrorCode.InsufficientResource);
            }

            city.PayFromPurse(coins);
            for (int index = 0; index < tiles.Count; index++)
            {
                city.PlaceRoad(tiles[index].X, tiles[index].Y, content.TerrainAt(tiles[index].X, tiles[index].Y) == Terrain.Water);
            }

            return MutationResult.Applied(Costs.AsSpent(content, coins));
        }

        /// <summary>
        /// Raises the road on every tile of the list one level and pays each tile's next level's cost. The checks run over the whole list:
        /// every tile has a road, then every road is below the last level, then the total cost.
        /// </summary>
        /// <param name="city">The city, settled to now.</param>
        /// <param name="content">The game content: the road levels.</param>
        /// <param name="tiles">The tiles, each once, at least one.</param>
        /// <returns>
        /// Ok, or the first failed check: <see cref="MutationErrorCode.InvalidArgument"/> (no tile, or a tile twice),
        /// <see cref="MutationErrorCode.RoadNotFound"/> (also for a tile outside the grid), <see cref="MutationErrorCode.MaxLevel"/> (also a
        /// bridge, which has one level), <see cref="MutationErrorCode.InsufficientResource"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static MutationResult UpgradeRoads(CityState city, GameContent content, IReadOnlyList<(int X, int Y)> tiles)
        {
            RequireArguments(city, content, tiles);
            if (!IsAListOfDistinctTiles(tiles))
            {
                return MutationResult.Rejected(MutationErrorCode.InvalidArgument);
            }

            if (!EveryTileHasARoad(city, tiles))
            {
                return MutationResult.Rejected(MutationErrorCode.RoadNotFound);
            }

            var cost = new ItemTotals();
            for (int index = 0; index < tiles.Count; index++)
            {
                city.TryGetRoad(tiles[index].X, tiles[index].Y, out RoadTile road);
                if (road.IsBridge || road.Level >= content.MaxRoadLevel)
                {
                    return MutationResult.Rejected(MutationErrorCode.MaxLevel);
                }

                AddAmounts(cost, content.GetRoadLevel(road.Level + 1).Cost);
            }

            long coins = Costs.CoinsOf(content, cost.ToQuantities());
            if (city.Purse < coins)
            {
                return MutationResult.Rejected(MutationErrorCode.InsufficientResource);
            }

            city.PayFromPurse(coins);
            for (int index = 0; index < tiles.Count; index++)
            {
                city.TryGetRoad(tiles[index].X, tiles[index].Y, out RoadTile road);
                city.SetRoadLevel(road.X, road.Y, road.Level + 1);
            }

            return MutationResult.Applied(Costs.AsSpent(content, coins));
        }

        /// <summary>Takes the road off every tile of the list. Nothing is refunded (CR-0008 D6).</summary>
        /// <param name="city">The city, settled to now.</param>
        /// <param name="content">The game content.</param>
        /// <param name="tiles">The tiles, each once, at least one.</param>
        /// <returns>
        /// Ok, or the first failed check: <see cref="MutationErrorCode.InvalidArgument"/> (no tile, or a tile twice),
        /// <see cref="MutationErrorCode.RoadNotFound"/> (also for a tile outside the grid).
        /// </returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static MutationResult RemoveRoads(CityState city, GameContent content, IReadOnlyList<(int X, int Y)> tiles)
        {
            RequireArguments(city, content, tiles);
            if (!IsAListOfDistinctTiles(tiles))
            {
                return MutationResult.Rejected(MutationErrorCode.InvalidArgument);
            }

            if (!EveryTileHasARoad(city, tiles))
            {
                return MutationResult.Rejected(MutationErrorCode.RoadNotFound);
            }

            for (int index = 0; index < tiles.Count; index++)
            {
                city.RemoveRoad(tiles[index].X, tiles[index].Y);
            }

            return MutationResult.Ok;
        }

        /// <summary>Throws for a null argument: a programmer error, unlike the business rules, which return a rejection.</summary>
        private static void RequireArguments(CityState city, GameContent content, IReadOnlyList<(int X, int Y)> tiles)
        {
            if (city is null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (tiles is null)
            {
                throw new ArgumentNullException(nameof(tiles));
            }
        }

        /// <summary>Whether the list names at least one tile and every tile once (the server refuses anything else before the lock, OD-13).</summary>
        private static bool IsAListOfDistinctTiles(IReadOnlyList<(int X, int Y)> tiles)
        {
            if (tiles.Count == 0)
            {
                return false;
            }

            var seen = new HashSet<(int X, int Y)>();
            for (int index = 0; index < tiles.Count; index++)
            {
                if (!seen.Add(tiles[index]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Whether every tile of the list has a road.</summary>
        private static bool EveryTileHasARoad(CityState city, IReadOnlyList<(int X, int Y)> tiles)
        {
            for (int index = 0; index < tiles.Count; index++)
            {
                if (!city.TryGetRoad(tiles[index].X, tiles[index].Y, out _))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Adds a cost to a running total, item by item, <c>checked</c>.</summary>
        private static void AddAmounts(ItemTotals total, IReadOnlyList<ItemQuantity> amounts)
        {
            for (int index = 0; index < amounts.Count; index++)
            {
                ItemQuantity amount = amounts[index];
                total.Set(amount.Item, checked(total.Get(amount.Item) + amount.Quantity));
            }
        }
    }
}
