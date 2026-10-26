using CityBuilder.Domain.Players;

namespace CityBuilder.Domain.Tests.Players;

/// <summary>The rules of <see cref="Player"/>: an id and a name.</summary>
public sealed class PlayerTests
{
    /// <summary>A valid player keeps its id and name.</summary>
    [Fact]
    public void Constructor_ValidValues_KeepsThem()
    {
        var id = new Guid("5a1c0f00-0000-4000-8000-000000000001");

        var player = new Player(id, "dev1");

        Assert.Equal(id, player.Id);
        Assert.Equal("dev1", player.DevName);
    }

    /// <summary>An empty id or a blank name is rejected.</summary>
    [Fact]
    public void Constructor_MissingValues_Throw()
    {
        Assert.Throws<ArgumentException>(() => new Player(Guid.Empty, "dev1"));
        Assert.Throws<ArgumentException>(() => new Player(Guid.NewGuid(), " "));
    }
}
