using System.Globalization;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using CityBuilder.Simulation.Tests.State;

namespace CityBuilder.Simulation.Tests.Rules;

/// <summary>Checks the result of a mutation and, for a rejection, that the city did not change at all.</summary>
internal static class RulesAssert
{
    public static void Ok(MutationResult result)
    {
        Assert.True(result.IsOk, result.ToString());
        Assert.Null(result.ErrorCode);
    }

    /// <summary>
    /// The mutation was applied and names exactly these items as spent and refunded, in the form <c>gold=20 wood=5</c> (empty for none).
    /// </summary>
    public static void Moved(MutationResult result, string spent, string refunded)
    {
        Ok(result);
        Assert.Equal((spent, refunded), (Text(result.Spent), Text(result.Refunded)));
    }

    /// <summary>The mutation is rejected with exactly this code, names nothing as spent or refunded, and every stored value of the city is as before.</summary>
    public static void Rejected(MutationErrorCode code, CityState city, Func<CityState, MutationResult> mutation)
    {
        string before = CityDump.Describe(city);

        MutationResult result = mutation(city);

        Assert.False(result.IsOk);
        Assert.Equal(code, result.ErrorCode);
        Assert.Empty(result.Spent);
        Assert.Empty(result.Refunded);
        Assert.Equal(before, CityDump.Describe(city));
    }

    private static string Text(IReadOnlyList<ItemQuantity> items) =>
        string.Join(" ", items.Select(item => string.Create(CultureInfo.InvariantCulture, $"{item.Item}={item.Quantity}")));
}
