#nullable enable

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// An amount mode that research unlocks (<c>core-instructions/04-simulation-model.md</c>, section 19): a preset is always allowed, a
    /// percentage and an exact number only after a research with <see cref="UnlockAmountModeEffect"/> for the mode. JSON: <c>percent</c>, <c>exact</c>.
    /// </summary>
    public enum AmountMode
    {
        /// <summary>Any percentage 1 to 100 of the base (Era I research, Tally Sticks in M0).</summary>
        Percent,

        /// <summary>An exact number of goods (an Era II research; locked in M0, OD-10).</summary>
        Exact,
    }
}
