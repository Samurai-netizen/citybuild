using CityBuilder.Application.Metrics;
using CityBuilder.Simulation.Rules;

namespace CityBuilder.Application.Tests.Metrics;

/// <summary>
/// <see cref="CommandEvents.Outcome"/>: a refused command's event outcome is the error code in upper snake case, as the API spells it
/// (every code against the catalogue: <c>CommandErrorMappingTests.Outcome_EngineCode_IsTheContractsCode</c>).
/// </summary>
public sealed class CommandEventsTests
{
    /// <summary>One word, two words and three words are spelled as the API spells them.</summary>
    /// <param name="code">The engine's code.</param>
    /// <param name="outcome">The outcome text.</param>
    [Theory]
    [InlineData(MutationErrorCode.MaxLevel, "MAX_LEVEL")]
    [InlineData(MutationErrorCode.InsufficientResource, "INSUFFICIENT_RESOURCE")]
    [InlineData(MutationErrorCode.NoScienceBuilding, "NO_SCIENCE_BUILDING")]
    public void Outcome_Code_IsUpperSnakeCase(MutationErrorCode code, string outcome)
    {
        Assert.Equal(outcome, CommandEvents.Outcome(code));
    }
}
