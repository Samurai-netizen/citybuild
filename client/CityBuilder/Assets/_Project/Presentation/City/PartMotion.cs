#nullable enable

using System;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// How one named moving part moves (visual v2, CR-0009): its <see cref="MotionKind"/>, how long one cycle takes and where in the cycle
    /// it starts, with the one formula of every kind (<see cref="WeightAt"/>). Plain data, so the motion can be tested without a scene. The
    /// art pipeline (<c>visual-v2/blender/lib.py</c>: <c>MOTIONS</c> and <c>motion_weight</c>) holds the same rows and the same formula, so
    /// its previews show what the client draws. Display only: nothing here is a game number.
    /// </summary>
    public readonly struct PartMotion
    {
        /// <summary>Where a chop's slow rise to the pose ends, as a fraction of its cycle.</summary>
        private const float ChopRiseEnd = 0.62f;

        /// <summary>Where a chop's fast blow back to rest ends; the rest of the cycle it stays at rest.</summary>
        private const float ChopBlowEnd = 0.74f;

        /// <summary>Creates a motion.</summary>
        /// <param name="kind">How the part moves.</param>
        /// <param name="periodSeconds">How long one cycle takes, in seconds; above 0.</param>
        /// <param name="phase">Where in its cycle the part starts, as a fraction of a cycle (0 to 1), so parts of one model can move out of step.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="periodSeconds"/> is not above 0.</exception>
        public PartMotion(MotionKind kind, float periodSeconds, float phase)
        {
            if (!(periodSeconds > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(periodSeconds), periodSeconds, "A cycle must take some time.");
            }

            Kind = kind;
            PeriodSeconds = periodSeconds;
            Phase = phase;
        }

        /// <summary>How the part moves.</summary>
        public MotionKind Kind { get; }

        /// <summary>How long one cycle takes, in seconds.</summary>
        public float PeriodSeconds { get; }

        /// <summary>Where in its cycle the part starts, as a fraction of a cycle.</summary>
        public float Phase { get; }

        /// <summary>
        /// How far along its motion the part is after <paramref name="seconds"/> of motion (<see cref="WeightAt"/> at this part's cycle position).
        /// </summary>
        /// <param name="seconds">The time the part has been moving; any value.</param>
        /// <returns>The weight: 0 is rest, 1 the pose, -1 the mirror of the pose (a wave); for a spin, the turns made.</returns>
        public float Weight(float seconds)
        {
            return WeightAt(Kind, seconds / PeriodSeconds + Phase);
        }

        /// <summary>
        /// The weight of a kind at a cycle position: a spin returns the turns (the position itself); a chop eases up to 1 by 0.62 of its
        /// cycle, falls back to 0 with growing speed by 0.74 and stays there; a ping-pong is a smooth 0 to 1 and back; a wave a sine
        /// from 0 to 1, through 0 to -1 and back. Allocates nothing.
        /// </summary>
        /// <param name="kind">How the part moves.</param>
        /// <param name="cycles">The cycle position: any real number, whole cycles repeat.</param>
        /// <returns>The weight.</returns>
        public static float WeightAt(MotionKind kind, float cycles)
        {
            float f = cycles - MathF.Floor(cycles);
            switch (kind)
            {
                case MotionKind.Spin:
                    return cycles;
                case MotionKind.Chop:
                    if (f < ChopRiseEnd)
                    {
                        float u = f / ChopRiseEnd;
                        return u * u * (3f - 2f * u);
                    }

                    if (f < ChopBlowEnd)
                    {
                        float u = (f - ChopRiseEnd) / (ChopBlowEnd - ChopRiseEnd);
                        return 1f - u * u;
                    }

                    return 0f;
                case MotionKind.PingPong:
                    return 0.5f - 0.5f * MathF.Cos(2f * MathF.PI * f);
                case MotionKind.Wave:
                    return MathF.Sin(2f * MathF.PI * f);
                default:
                    return 0f;
            }
        }
    }
}
