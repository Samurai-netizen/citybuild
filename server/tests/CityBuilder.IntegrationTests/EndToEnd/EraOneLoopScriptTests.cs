using CityBuilder.Simulation.State;

namespace CityBuilder.IntegrationTests.EndToEnd;

/// <summary>
/// The end-to-end script (<see cref="EraOneLoop"/>) on the engine alone, without a server or a database: every act does what the script
/// says, so a failure of the HTTP suite points at the server, not at the script.
/// </summary>
public sealed class EraOneLoopScriptTests
{
    /// <summary>Every accepted act is accepted by the engine and every refused act is refused with its code, through the 30-hour absence.</summary>
    [Fact]
    public void Replay_WholeScript_EveryActDoesWhatTheScriptSays()
    {
        CityState city = EraOneLoop.Replay(EraOneLoop.ReturnTime);

        Assert.Equal(EraOneLoop.ReturnTime, city.Clock);
    }
}
