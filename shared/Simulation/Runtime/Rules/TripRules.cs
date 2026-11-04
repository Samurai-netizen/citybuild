#nullable enable
using System;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Map;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Rules
{
    /// <summary>
    /// Manual trips (<c>core-instructions/04-simulation-model.md</c>, sections 8 and 18): the player sends one free worker with goods of one
    /// item from a building's store to another building. Like every mutation it runs at the city's clock after settling, checks in the order of
    /// section 8 and changes nothing when it rejects. The trip arrives in a later advance (<see cref="Settlement.Simulator"/>), which delivers
    /// the goods (section 16).
    /// </summary>
    public static class TripRules
    {
        /// <summary>
        /// Starts a trip: takes <c>q</c> of the item out of the source's store (the amount of its stock, at most the stock and the content's
        /// <see cref="GameRules.TripCarryLimit"/>) and gives it to a free worker, who walks to the target by the path rule (<c>04</c> section 17)
        /// with the city's walk bonus. The trip gets the next trip id; it arrives at the clock plus the walk's seconds. Goods only move, so the
        /// result spends and refunds nothing.
        /// </summary>
        /// <param name="city">The city, settled to now.</param>
        /// <param name="content">The game content.</param>
        /// <param name="sourceId">The building whose store the goods come from.</param>
        /// <param name="targetId">The building they go to.</param>
        /// <param name="item">The good.</param>
        /// <param name="amount">How much of the source's stock.</param>
        /// <returns>
        /// Ok, or the first failed check: <see cref="MutationErrorCode.InvalidArgument"/> (the source is the target), then in the order of
        /// <c>04</c> section 8 <see cref="MutationErrorCode.BuildingNotFound"/>, <see cref="MutationErrorCode.UnknownItem"/>,
        /// <see cref="MutationErrorCode.InvalidArgument"/> (the currency, an unknown preset), <see cref="MutationErrorCode.AmountModeLocked"/>,
        /// <see cref="MutationErrorCode.ItemNotAccepted"/>, <see cref="MutationErrorCode.InsufficientWorkers"/>,
        /// <see cref="MutationErrorCode.NothingToCarry"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="city"/>, <paramref name="content"/> or <paramref name="amount"/> is null.</exception>
        public static MutationResult StartTrip(CityState city, GameContent content, int sourceId, int targetId, ItemId item, Amount amount)
        {
            if (city is null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (amount is null)
            {
                throw new ArgumentNullException(nameof(amount));
            }

            // The server refuses a trip to its own source before the lock (05); the engine checks it too, as the shape of its input.
            if (sourceId == targetId)
            {
                return MutationResult.Rejected(MutationErrorCode.InvalidArgument);
            }

            if (!city.TryGetBuilding(sourceId, out Building? source) || !city.TryGetBuilding(targetId, out Building? target))
            {
                return MutationResult.Rejected(MutationErrorCode.BuildingNotFound);
            }

            if (!content.TryGetItem(item, out _))
            {
                return MutationResult.Rejected(MutationErrorCode.UnknownItem);
            }

            if (item == content.Currency)
            {
                return MutationResult.Rejected(MutationErrorCode.InvalidArgument);
            }

            MutationErrorCode? amountError = AmountRules.Check(city, content, amount);
            if (amountError.HasValue)
            {
                return MutationResult.Rejected(amountError.Value);
            }

            if (!CityQueries.Accepts(city, content, target, item))
            {
                return MutationResult.Rejected(MutationErrorCode.ItemNotAccepted);
            }

            if (CityQueries.FreeWorkers(city, content) < 1)
            {
                return MutationResult.Rejected(MutationErrorCode.InsufficientWorkers);
            }

            long stock = source.GetStock(item);
            long quantity = Math.Min(Math.Min(AmountRules.Of(content, amount, stock), stock), content.Rules.TripCarryLimit);
            if (quantity < 1)
            {
                return MutationResult.Rejected(MutationErrorCode.NothingToCarry);
            }

            // The walk keeps the time it starts with (04 section 17); a mutation measures one walk, so a finder made here is enough.
            var paths = new PathFinder(content);
            paths.Load(city);
            long seconds = paths.Measure(source.X, source.Y, target.X, target.Y).Seconds(CityQueries.WalkBonusPermille(city, content));
            source.TakeFromStore(item, quantity);
            city.AddTrip(sourceId, targetId, item, quantity, checked(city.Clock + seconds));
            return MutationResult.Ok;
        }
    }
}
