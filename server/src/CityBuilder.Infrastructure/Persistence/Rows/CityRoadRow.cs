namespace CityBuilder.Infrastructure.Persistence.Rows;

/// <summary>
/// A row of <c>city_roads</c> (core-instructions/06-data-model.md, CR-0008): one road tile of a city with its level
/// (core-instructions/04-simulation-model.md, section 17). A road never lies on a building's tile; the engine checks that on load.
/// </summary>
public sealed class CityRoadRow
{
    /// <summary><c>city_id</c>: the city (<see cref="CityRow"/>); with <see cref="X"/> and <see cref="Y"/> the primary key.</summary>
    public Guid CityId { get; set; }

    /// <summary><c>x</c>: the grid column, 0 or more.</summary>
    public short X { get; set; }

    /// <summary><c>y</c>: the grid row, 0 or more.</summary>
    public short Y { get; set; }

    /// <summary><c>level</c>: the road level, 1 or more (the content's road levels).</summary>
    public short Level { get; set; }
}
