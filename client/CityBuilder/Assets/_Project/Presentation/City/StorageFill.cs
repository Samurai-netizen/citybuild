#nullable enable

using System;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// How full a building's store looks (visual v2, CR-0008-T2): its fill model has one to three steps per item, and a store shows the step of
    /// its share of the limit, one step per equal part: with three steps, anything up to a third shows step 1, from a third step 2, from two
    /// thirds step 3, and an empty store none. Pure integer maths. Display only: the amounts come from the city as the server sent it.
    /// </summary>
    public static class StorageFill
    {
        /// <summary>The most steps a fill model gives one item.</summary>
        public const int MaxSteps = 3;

        /// <summary>The step a store of <paramref name="amount"/> out of <paramref name="limit"/> shows, out of <paramref name="steps"/>.</summary>
        /// <param name="amount">What the store holds; at or below 0 shows nothing.</param>
        /// <param name="limit">The store's limit for the item; at or below 0 shows nothing (an item without a limit).</param>
        /// <param name="steps">How many steps the model has for the item, 1 to <see cref="MaxSteps"/>.</param>
        /// <returns>0 for an empty store, else 1 to <paramref name="steps"/>; a store at or above its limit shows every step.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is not 1 to <see cref="MaxSteps"/>.</exception>
        public static int StepFor(long amount, long limit, int steps)
        {
            if (steps < 1 || steps > MaxSteps)
            {
                throw new ArgumentOutOfRangeException(nameof(steps), steps, "A fill model has 1 to " + MaxSteps + " steps.");
            }

            if (amount <= 0 || limit <= 0)
            {
                return 0;
            }

            if (amount >= limit)
            {
                return steps;
            }

            // Step k + 1 starts at ceil(k * limit / steps), written so that nothing overflows even for a limit near long.MaxValue:
            // k * (limit / steps) is below limit, and k * (limit % steps) is at most 4.
            int step = 1;
            for (int k = 1; k < steps; k++)
            {
                long threshold = k * (limit / steps) + (k * (limit % steps) + steps - 1) / steps;
                if (amount >= threshold)
                {
                    step = k + 1;
                }
            }

            return step;
        }
    }
}
