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

    /// <summary>The mutation is rejected with exactly this code, and every stored value of the city is as before.</summary>
    public static void Rejected(MutationErrorCode code, CityState city, Func<CityState, MutationResult> mutation)
    {
        string before = CityDump.Describe(city);

        MutationResult result = mutation(city);

        Assert.False(result.IsOk);
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal(before, CityDump.Describe(city));
    }
}
