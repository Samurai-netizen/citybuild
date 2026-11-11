#nullable enable
using System;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Map;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Rules
{
    /// <summary>
    /// Deliveries (<c>core-instructions/04-simulation-model.md</c>, sections 8 and 18, CR-0008 amendment 1): the player sends goods of one item
    /// from a building's store to another building. With a built courier station a free courier gets the delivery as lines of at most its
    /// carry limit (its queue, D16 and D17); before any station is built a free worker shuttles it (a <see cref="Trip"/>, D21). Like every
    /// mutation it runs at the city's clock after settling, checks in the order of section 8 and changes nothing when it rejects. The walks
    /// and deliveries happen in later advances (<see cref="Settlement.Simulator"/>, section 16).
    /// </summary>
    public static class TripRules
    {
        /// <summary>
        /// Sends a delivery: counts <c>Q</c>, the amount of the source's stock now, at most the stock. With a built courier station the
        /// engine picks a free courier (<see cref="CourierRules.PickFreeCourier"/>) and appends <c>ceil(Q / c)</c> lines to its queue, <c>c</c>
        /// being its station level's carry limit; nothing leaves the store yet. Otherwise a free worker takes the first load, at most the
        /// content's <see cref="GameRules.TripCarryLimit"/>, out of the store and walks to the target by the path rule (<c>04</c> section 17)
        /// with the city's walk bonus; the trip remembers the rest. Goods only move, so the result spends and refunds nothing.
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
        /// <see cref="MutationErrorCode.ItemNotAccepted"/>, <see cref="MutationErrorCode.NoFreeCourier"/> (with a built station) or
        /// <see cref="MutationErrorCode.InsufficientWorkers"/> (without), <see cref="MutationErrorCode.NothingToCarry"/>,
        /// <see cref="MutationErrorCode.DeliveryQueueFull"/>.
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

            // The walk keeps the time it starts with (04 section 17); a mutation measures few walks, so a finder made here is enough.
            var paths = new PathFinder(content);
            paths.Load(city);
            int bonus = CityQueries.WalkBonusPermille(city, content);
            Courier? courier = null;
            if (CityQueries.HasBuiltCourierStation(city, content))
            {
                courier = CourierRules.PickFreeCourier(city, content, paths, source);
                if (courier is null)
                {
                    return MutationResult.Rejected(MutationErrorCode.NoFreeCourier);
                }
            }
            else if (CityQueries.FreeWorkers(city, content) < 1)
            {
                return MutationResult.Rejected(MutationErrorCode.InsufficientWorkers);
            }

            long stock = source.GetStock(item);
            long quantity = Math.Min(AmountRules.Of(content, amount, stock), stock);
            if (quantity < 1)
            {
                return MutationResult.Rejected(MutationErrorCode.NothingToCarry);
            }

            if (courier is not null)
            {
                return QueueLines(city, content, courier, sourceId, targetId, item, quantity);
            }

            long load = Math.Min(quantity, content.Rules.TripCarryLimit);
            long seconds = paths.Measure(source.X, source.Y, target.X, target.Y).Seconds(bonus);
            source.TakeFromStore(item, load);
            city.AddTrip(sourceId, targetId, item, load, checked(city.Clock + seconds), checked(quantity - load));
            return MutationResult.Ok;
        }

        /// <summary>
        /// Splits <paramref name="quantity"/> into lines of at most the courier's carry limit, each of that limit but the last, and appends them
        /// to its queue; refuses when they don't fit in <see cref="GameRules.MaxDeliveryLines"/> (<c>04</c> section 18).
        /// </summary>
        private static MutationResult QueueLines(CityState city, GameContent content, Courier courier, int sourceId, int targetId, ItemId item, long quantity)
        {
            city.TryGetBuilding(courier.StationId, out Building? station);
            long carry = CityQueries.DefinitionOf(content, station!.TypeId).GetLevel(station.Level).CarryLimit;
            long lines = IntegerMath.CeilDiv(quantity, carry);
            if (checked(city.CountLines(courier.StationId, courier.Slot) + lines) > content.Rules.MaxDeliveryLines)
            {
                return MutationResult.Rejected(MutationErrorCode.DeliveryQueueFull);
            }

            for (long left = quantity; left > 0; left -= carry)
            {
                city.AddLine(courier.StationId, courier.Slot, sourceId, targetId, item, Math.Min(left, carry));
            }

            return MutationResult.Ok;
        }
    }
}
