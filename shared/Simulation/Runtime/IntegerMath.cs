#nullable enable
using System;

namespace CityBuilder.Simulation
{
    /// <summary>
    /// The integer helpers that game logic uses for every division. Durations round up, so a cycle never finishes early, display
    /// estimates round down, and no game value is divided in any other way (<c>core-instructions/04-simulation-model.md</c>, section 9).
    /// </summary>
    public static class IntegerMath
    {
        /// <summary>One whole as permille: 1000 permille is 100 %. It is the unit of every modifier, such as a labor reduction.</summary>
        public const int PermilleScale = 1000;

        /// <summary>
        /// Divides and rounds up: the smallest whole number <c>q</c> with <c>q × divisor ≥ dividend</c>.
        /// Durations use it, for example the seconds a cycle takes: <c>CeilDiv(labor, workers)</c>.
        /// </summary>
        /// <param name="dividend">The amount to divide, for example labor in worker-seconds. It must not be negative.</param>
        /// <param name="divisor">The amount to divide by, for example the number of workers. It must be greater than zero.</param>
        /// <returns>The quotient rounded up. It never overflows, because it is at most <paramref name="dividend"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="dividend"/> is negative, or <paramref name="divisor"/> is zero or negative. If both are out of range,
        /// the exception names the dividend.
        /// </exception>
        public static long CeilDiv(long dividend, long divisor)
        {
            if (dividend < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dividend), dividend, "The dividend must not be negative.");
            }

            if (divisor <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(divisor), divisor, "The divisor must be greater than zero.");
            }

            long quotient = dividend / divisor;

            // Adding one cannot overflow: a remainder means the divisor is at least 2, so the quotient is at most half of long.MaxValue.
            return dividend % divisor == 0 ? quotient : checked(quotient + 1);
        }

        /// <summary>
        /// Divides and rounds down: the largest whole number <c>q</c> with <c>q × divisor ≤ dividend</c>. Only display helpers use it
        /// (rates and progress shown to players), where a value may be a little low but never promises more than will happen. Authority
        /// code rounds durations up with <see cref="CeilDiv"/>.
        /// </summary>
        /// <param name="dividend">The amount to divide. It must not be negative.</param>
        /// <param name="divisor">The amount to divide by. It must be greater than zero.</param>
        /// <returns>The quotient rounded down. It never overflows, because it is at most <paramref name="dividend"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="dividend"/> is negative, or <paramref name="divisor"/> is zero or negative. If both are out of range,
        /// the exception names the dividend.
        /// </exception>
        public static long FloorDiv(long dividend, long divisor)
        {
            if (dividend < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dividend), dividend, "The dividend must not be negative.");
            }

            if (divisor <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(divisor), divisor, "The divisor must be greater than zero.");
            }

            return dividend / divisor;
        }

        /// <summary>
        /// Takes a reduction in permille off an amount of labor and rounds up: <c>ceil(labor × (1000 − permille) / 1000)</c>.
        /// The result is exact for every non-negative <paramref name="labor"/>, because the full product is never formed.
        /// </summary>
        /// <param name="labor">Labor in worker-seconds. It must not be negative.</param>
        /// <param name="permille">
        /// The reduction in permille, from 0 (none) to <see cref="PermilleScale"/> (all of it). Content validation (<see cref="Content.GameContentFactory"/>) caps
        /// the sum of the reductions of one building type at the rule <c>MaxLaborReductionPermille</c> (500 in the M0 content).
        /// </param>
        /// <returns>
        /// The reduced labor in worker-seconds. It is never more than <paramref name="labor"/>, and it is zero only when
        /// <paramref name="labor"/> is zero or <paramref name="permille"/> is <see cref="PermilleScale"/>: positive labor with a smaller
        /// reduction stays at least 1, so a cycle never becomes free by rounding.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="labor"/> is negative, or <paramref name="permille"/> is outside 0 to <see cref="PermilleScale"/>. If both are
        /// out of range, the exception names the labor.
        /// </exception>
        public static long ApplyPermilleReduction(long labor, int permille)
        {
            if (labor < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(labor), labor, "The labor must not be negative.");
            }

            if (permille < 0 || permille > PermilleScale)
            {
                throw new ArgumentOutOfRangeException(nameof(permille), permille, $"The reduction must be between 0 and {PermilleScale} permille.");
            }

            long remainingPermille = PermilleScale - permille;

            // labor = wholeThousands × 1000 + rest. The whole thousands scale to a whole number already, so only the small rest
            // needs rounding up. The sum is exactly ceil(labor × remainingPermille / 1000), which is at most labor, so it fits in a long:
            // labor = long.MaxValue with no reduction ends exactly on long.MaxValue. Both products are smaller than that sum or tiny.
            long wholeThousands = labor / PermilleScale;
            long rest = labor % PermilleScale;
            return checked((wholeThousands * remainingPermille) + CeilDiv(rest * remainingPermille, PermilleScale));
        }
    }
}
