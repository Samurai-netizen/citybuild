using CityBuilder.Simulation.State;

namespace CityBuilder.BalanceSim.Strategies;

/// <summary>
/// Picks the amount a delivery asks for, from the amounts the city may use (core-instructions/04-simulation-model.md §19): an exact number
/// once research unlocks it, a percentage once research unlocks that, else the content's presets. A percentage <c>p</c> of a stock <c>b</c>
/// gives <c>max(1, b × p / 100)</c> rounded down: the engine's rule (<c>AmountRules.Of</c>, internal to the engine), repeated here to choose.
/// </summary>
public static class DeliveryAmount
{
    /// <summary>
    /// The amount that gives the fewest goods that still cover <c>min(wanted, stock)</c>; the largest preset when none covers it.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <param name="stock">The source's stock, above 0.</param>
    /// <param name="wanted">How many the target should get, above 0.</param>
    /// <returns>The amount, or null when the content has no preset and no mode is unlocked.</returns>
    public static Amount? Choose(Turn turn, long stock, long wanted)
    {
        long target = Math.Min(stock, wanted);
        if (CityQueries.IsAmountModeUnlocked(turn.City, turn.Content, Simulation.Content.AmountMode.Exact))
        {
            return Amount.Exact(target);
        }

        if (CityQueries.IsAmountModeUnlocked(turn.City, turn.Content, Simulation.Content.AmountMode.Percent))
        {
            // The smallest percentage whose share covers the target; 100 always does.
            for (int percent = 1; percent <= 100; percent++)
            {
                if (Share(stock, percent) >= target)
                {
                    return Amount.Percent(percent);
                }
            }
        }

        (Simulation.AmountPresetId Id, int Percent)[] presets = [.. turn.Content.AmountPresets
            .Select(preset => (preset.Id, preset.Percent))
            .OrderBy(preset => preset.Percent).ThenBy(preset => preset.Id.Value, StringComparer.Ordinal)];
        if (presets.Length == 0)
        {
            return null;
        }

        foreach ((Simulation.AmountPresetId id, int percent) in presets)
        {
            if (Share(stock, percent) >= target)
            {
                return Amount.Preset(id);
            }
        }

        return Amount.Preset(presets[^1].Id);
    }

    /// <summary>What an amount the city may use gives of a stock: an exact number itself, a percentage or preset its share.</summary>
    /// <param name="turn">The turn.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="stock">The stock, above 0.</param>
    /// <returns>The quantity, at most the stock.</returns>
    public static long QuantityOf(Turn turn, Amount amount, long stock)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(amount);
        long quantity = amount.Mode switch
        {
            Simulation.Content.AmountMode.Exact => amount.Value,
            Simulation.Content.AmountMode.Percent => Share(stock, (int)amount.Value),
            _ => turn.Content.TryGetAmountPreset(amount.PresetId, out Simulation.Content.AmountPresetDefinition? preset) ? Share(stock, preset.Percent) : stock,
        };
        return Math.Min(quantity, stock);
    }

    /// <summary>What a percentage gives of a stock: <c>max(1, stock × percent / 100)</c> rounded down.</summary>
    /// <param name="stock">The stock, above 0.</param>
    /// <param name="percent">The percentage.</param>
    /// <returns>The quantity.</returns>
    private static long Share(long stock, int percent) => Math.Max(1, checked(stock * percent) / 100);
}
