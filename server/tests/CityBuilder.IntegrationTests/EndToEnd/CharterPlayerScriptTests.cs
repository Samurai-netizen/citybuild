using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.IntegrationTests.EndToEnd;

/// <summary>
/// The Era I charter player (<see cref="CharterPlayer"/>, M1-02) on the engine alone, without a server or a database: on the server's Era I
/// content it reaches the last research, and its script replays to the same city, so a failure of <see cref="CharterHttpTests"/> points at
/// the server, not at the player.
/// </summary>
public sealed class CharterPlayerScriptTests
{
    /// <summary>
    /// The player completes the Town Charter, and with it every research of the era, within its limit of 14 days, on the map (M1-03): its
    /// Farm stands on the east bank, reached over the bridge it built.
    /// </summary>
    [Fact]
    public void Play_Era1Content_CompletesTheTownCharterAndTheWholeTree()
    {
        CityState end = CharterPlayer.End;

        Assert.True(end.CompletedResearch.Contains(CharterPlayer.Charter), EraOneLoop.Describe(end) + "\n" + string.Join("\n", CharterPlayer.Script.TakeLast(5)
            .Select(step => $"{step.Time}: {string.Join(", ", step.Acts.Select(act => act.ToString()))}")));
        Assert.Equal(CharterPlayer.Content.Research.Select(node => node.Id).Order(), end.CompletedResearch.Order());
        Assert.True(end.Clock <= CharterPlayer.LimitSeconds);
        Assert.True(end.TryGetRoad(12, 5, out RoadTile bridge) && bridge.IsBridge);
        Assert.Contains(end.Buildings, building => building.TypeId.Value == "farm" && building.X > 12 && building.IsBuilt);
    }

    /// <summary>Replaying the kept acts alone, as the server would receive them, gives the city the player ended with.</summary>
    [Fact]
    public void Replay_WholeScript_GivesThePlayersCity()
    {
        CityState replayed = CharterPlayer.Replay(CharterPlayer.Script[^1].Time);
        Simulator.Advance(replayed, CharterPlayer.Content, CharterPlayer.End.Clock - replayed.Clock, CharterPlayer.Script[^1].Time + CharterPlayer.GraceSeconds);

        Assert.Equal(EraOneLoop.Describe(CharterPlayer.End), EraOneLoop.Describe(replayed));
    }
}
