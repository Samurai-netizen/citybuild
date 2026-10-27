namespace CityBuilder.Api.Contracts;

/// <summary>One research node in <see cref="ContentResponse.Research"/> (<c>core-instructions/25-eras-and-research.md</c>).</summary>
/// <param name="Id">The node's id, for example <c>frame_saw</c>, JSON <c>id</c>.</param>
/// <param name="Name">The display name, JSON <c>name</c>.</param>
/// <param name="Era">The era the node belongs to, JSON <c>era</c>; a city researches only the nodes of its own era.</param>
/// <param name="Cost">What starting the research costs, JSON <c>cost</c>: item id to quantity, by item id.</param>
/// <param name="DurationSeconds">How long the research runs, in seconds, JSON <c>durationSeconds</c>.</param>
/// <param name="Prerequisites">The ids of the nodes that must be completed first, JSON <c>prerequisites</c>, by id; empty when there are none.</param>
/// <param name="Effects">What completing the node changes, JSON <c>effects</c>, in the order the content lists them.</param>
public sealed record ContentResearch(
    string Id,
    string Name,
    int Era,
    IReadOnlyDictionary<string, long> Cost,
    long DurationSeconds,
    IReadOnlyList<string> Prerequisites,
    IReadOnlyList<ContentResearchEffect> Effects);
