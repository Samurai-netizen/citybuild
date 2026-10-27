namespace CityBuilder.Api.Contracts;

/// <summary>What a producer makes in one cycle, the <c>recipe</c> of a <see cref="ContentBuildingLevel"/>.</summary>
/// <param name="Inputs">What a cycle consumes when it starts, JSON <c>inputs</c>: item id to quantity, by item id; empty for a raw producer.</param>
/// <param name="Outputs">What a cycle makes when it ends, JSON <c>outputs</c>: item id to quantity, by item id.</param>
/// <param name="Labor">The work one cycle needs in worker-seconds before research reductions, JSON <c>labor</c>. With <c>w</c> workers a cycle takes
/// <c>ceil(labor / w)</c> seconds.</param>
public sealed record ContentRecipe(IReadOnlyDictionary<string, long> Inputs, IReadOnlyDictionary<string, long> Outputs, long Labor);
