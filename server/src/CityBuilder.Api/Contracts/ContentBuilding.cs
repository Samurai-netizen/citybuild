namespace CityBuilder.Api.Contracts;

/// <summary>One building type in <see cref="ContentResponse.Buildings"/> (<c>core-instructions/04-simulation-model.md</c>, section 4).</summary>
/// <param name="Id">The type's id, for example <c>sawmill</c>, JSON <c>id</c>.</param>
/// <param name="Name">The display name, JSON <c>name</c>.</param>
/// <param name="Kind">What the type does, JSON <c>kind</c>: <c>House</c> (provides workers, has no recipe) or <c>Producer</c> (runs a recipe with workers).</param>
/// <param name="Era">The era the type belongs to, JSON <c>era</c>; a city can't build a type of a later era.</param>
/// <param name="AvailableAtStart">Whether the type can be built without research, JSON <c>availableAtStart</c>.</param>
/// <param name="UnlockedBy">The id of the research that unlocks the type, JSON <c>unlockedBy</c>; <see langword="null"/> when none does.</param>
/// <param name="Cost">What placing a building of the type costs, JSON <c>cost</c>: item id to quantity, by item id.</param>
/// <param name="BuildSeconds">How long a new building is under construction, in seconds, JSON <c>buildSeconds</c>.</param>
/// <param name="Levels">The levels, JSON <c>levels</c>, by ascending level from 1; the last one is the max level.</param>
public sealed record ContentBuilding(
    string Id,
    string Name,
    string Kind,
    int Era,
    bool AvailableAtStart,
    string? UnlockedBy,
    IReadOnlyDictionary<string, long> Cost,
    long BuildSeconds,
    IReadOnlyList<ContentBuildingLevel> Levels);
