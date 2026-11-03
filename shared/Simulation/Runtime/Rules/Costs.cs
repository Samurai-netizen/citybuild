#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Simulation.Rules
{
    /// <summary>
    /// A content cost split into its two parts (<c>core-instructions/04-simulation-model.md</c>, section 16; CR-0008 D1): the coins, paid from
    /// the purse when the work is ordered, and the goods, never taken from a store but delivered to the project by walkers.
    /// </summary>
    internal static class Costs
    {
        /// <summary>The coins of a cost: the quantity of the currency, 0 when the cost has none.</summary>
        public static long CoinsOf(GameContent content, IReadOnlyList<ItemQuantity> cost)
        {
            long coins = 0;
            for (int index = 0; index < cost.Count; index++)
            {
                if (cost[index].Item == content.Currency)
                {
                    coins = checked(coins + cost[index].Quantity);
                }
            }

            return coins;
        }

        /// <summary>The goods of a cost (every amount but the currency's, 0s left out), in the cost's (ordinal item) order: a project's needs.</summary>
        public static IReadOnlyList<ItemQuantity> GoodsOf(GameContent content, IReadOnlyList<ItemQuantity> cost)
        {
            var goods = new List<ItemQuantity>(cost.Count);
            for (int index = 0; index < cost.Count; index++)
            {
                if (cost[index].Item != content.Currency && cost[index].Quantity > 0)
                {
                    goods.Add(cost[index]);
                }
            }

            return goods.AsReadOnly();
        }

        /// <summary>Coins as a list of amounts, for <see cref="MutationResult.Spent"/>: one amount of the currency, or none for 0.</summary>
        public static IReadOnlyList<ItemQuantity> AsSpent(GameContent content, long coins) =>
            coins > 0
                ? Array.AsReadOnly(new[] { new ItemQuantity(content.Currency, coins) })
                : Array.AsReadOnly(Array.Empty<ItemQuantity>());
    }
}
