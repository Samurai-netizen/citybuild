using CityBuilder.Application.Cities;

namespace CityBuilder.Api.Endpoints;

/// <summary>
/// The log messages about settlements (source-generated, decision 029 in core-instructions/12-decisions.md; ids 6000–6999): one line for
/// every settle, from <c>GET /city</c> or a command, with its size (ADR-0005, Watch), and the warning for a server clock behind a city
/// (core-instructions/04-simulation-model.md, section 1). <see cref="Write"/> writes both from a <see cref="Settlement"/>.
/// </summary>
internal static partial class SettlementLog
{
    /// <summary>What a settle triggered by <c>GET /city</c> is called in <c>{Trigger}</c>; a command's is its type.</summary>
    public const string CityTrigger = "city";

    /// <summary>
    /// Logs one settlement and, when the server clock was behind the city's last settled second, the warning. The values are computed here,
    /// before the source-generated calls.
    /// </summary>
    /// <param name="logger">The logger of the endpoint that settled.</param>
    /// <param name="playerId">The player whose city was settled.</param>
    /// <param name="trigger"><see cref="CityTrigger"/>, or the type of the command that settled first.</param>
    /// <param name="settlement">The settlement.</param>
    /// <param name="durationMs">How long the request took so far, in milliseconds.</param>
    public static void Write(ILogger logger, Guid playerId, string trigger, Settlement settlement, double durationMs)
    {
        ArgumentNullException.ThrowIfNull(settlement);

        int events = settlement.CompletedBuildings.Count + settlement.CompletedResearch.Count + (settlement.EraChanged ? 1 : 0);
        string researchCompleted = string.Join(",", settlement.CompletedResearch.Select(research => research.Value));
        Settled(logger, playerId, trigger, settlement.ElapsedSeconds, settlement.ProductionSeconds, events, researchCompleted, durationMs);
        if (settlement.Window.IsClockBehind)
        {
            ClockBehind(logger, playerId, settlement.ClockBehindSeconds);
        }
    }

    /// <summary>
    /// Logs one settlement (event id 6000): the seconds the city clock moved, the seconds in which cycles could start, the number of events
    /// (constructions, upgrades and research completed, an era entered) and the research completed, by id. The source generator writes the
    /// method's body.
    /// </summary>
    /// <param name="logger">The logger of the endpoint that settled.</param>
    /// <param name="playerId">The player whose city was settled.</param>
    /// <param name="trigger"><see cref="CityTrigger"/> or the command type.</param>
    /// <param name="elapsedSeconds">The seconds the city clock moved.</param>
    /// <param name="productionSeconds">The seconds of the window in which cycles could start.</param>
    /// <param name="events">How many timed events completed.</param>
    /// <param name="researchCompleted">The research completed, ids joined by commas; empty for none.</param>
    /// <param name="durationMs">How long the request took so far, in milliseconds.</param>
    [LoggerMessage(EventId = 6000, Level = LogLevel.Information,
        Message = "Settled city of player {PlayerId} for {Trigger}: {ElapsedSeconds} s elapsed, {ProductionSeconds} s of production, " +
            "{Events} events, research completed [{ResearchCompleted}], in {DurationMs} ms")]
    private static partial void Settled(
        ILogger logger, Guid playerId, string trigger, long elapsedSeconds, long productionSeconds, int events, string researchCompleted, double durationMs);

    /// <summary>
    /// Logs that the server clock is behind the city's last settled second (event id 6001): it was set back, so nothing elapsed and the
    /// seconds it repeats are not counted again (<c>04</c> section 1). The source generator writes the method's body.
    /// </summary>
    /// <param name="logger">The logger of the endpoint that settled.</param>
    /// <param name="playerId">The player whose city it is.</param>
    /// <param name="secondsBehind">How many seconds the clock is behind.</param>
    [LoggerMessage(EventId = 6001, Level = LogLevel.Warning,
        Message = "Server clock is {SecondsBehind} s behind the last settled second of player {PlayerId}'s city; nothing elapsed")]
    private static partial void ClockBehind(ILogger logger, Guid playerId, long secondsBehind);
}
