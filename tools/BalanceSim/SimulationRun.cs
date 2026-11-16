using CityBuilder.BalanceSim.Strategies;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.BalanceSim;

/// <summary>What one run produced: its settings, the city at the end and everything the recorder measured.</summary>
/// <param name="Options">The run's settings.</param>
/// <param name="Content">The content it played.</param>
/// <param name="City">The city at the end of the run.</param>
/// <param name="Recorder">The measurements.</param>
/// <param name="Sessions">The check-ins: start and end seconds, ascending, not overlapping.</param>
public sealed record RunResult(RunOptions Options, GameContent Content, CityState City, RunRecorder Recorder, IReadOnlyList<(long Start, long End)> Sessions);

/// <summary>
/// Plays one run (docs/modules/BalanceSim.md → How a run works): a new starter city from the content, then, until the run's end, check-ins
/// and absences. During a check-in the strategy acts every <see cref="RunOptions.PresentTickSeconds"/> and the player is present. Between
/// check-ins the city advances in steps of at most <see cref="RunOptions.AbsentTickSeconds"/>, and the player counts as absent from the
/// check-in's end plus the grace (core-instructions/04-simulation-model.md §2), so couriers spend stamina. The result depends only on the
/// content and the options: no clock, no randomness, no dictionary order.
/// </summary>
public static class SimulationRun
{
    /// <summary>Plays a run.</summary>
    /// <param name="content">The game content.</param>
    /// <param name="options">The run's settings.</param>
    /// <returns>The result.</returns>
    public static RunResult Play(GameContent content, RunOptions options)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(options);
        CityState city = CityFactory.CreateStarterCity(content);
        var recorder = new RunRecorder(content);
        var actions = new CityActions(city, content, recorder);
        ContentChain chain = ContentChain.Of(content);
        Strategy strategy = Strategy.Create(options.Strategy);
        IReadOnlyList<(long Start, long End)> sessions = Sessions(options);

        int session = 0;
        long nextSample = options.SampleSeconds;
        long lastRow = -1;
        while (city.Clock < options.RunSeconds)
        {
            long now = city.Clock;
            while (session < sessions.Count && sessions[session].End <= now)
            {
                session++;
            }

            bool present = session < sessions.Count && sessions[session].Start <= now;
            bool sessionStart = present && sessions[session].Start == now;
            if (sessionStart && lastRow != now)
            {
                recorder.Sample(city, present: true);
                lastRow = now;
            }

            if (present)
            {
                strategy.Act(new Turn(city, content, chain, actions, sessionStart));
            }

            long boundary = session >= sessions.Count ? options.RunSeconds : present ? sessions[session].End : sessions[session].Start;
            long step = present ? RunOptions.PresentTickSeconds : RunOptions.AbsentTickSeconds;
            long next = Math.Min(Math.Min(now + step, boundary), Math.Min(nextSample, options.RunSeconds));
            long? absentFrom = present ? null : AbsentFrom(sessions, session, options.GraceSeconds);

            recorder.Measure(city, next - now);
            recorder.Advance(city, next - now, absentFrom);

            if (city.Clock == nextSample)
            {
                recorder.Sample(city, present: IsPresent(sessions, city.Clock));
                lastRow = city.Clock;
                nextSample += options.SampleSeconds;
            }
        }

        if (lastRow != city.Clock)
        {
            recorder.Sample(city, present: IsPresent(sessions, city.Clock));
        }

        return new RunResult(options, content, city, recorder, sessions);
    }

    /// <summary>The run's check-ins: each start of the pattern with its length (the first is longer), cut at the run's end, overlaps merged.</summary>
    /// <param name="options">The run's settings.</param>
    /// <returns>The check-ins, ascending.</returns>
    public static IReadOnlyList<(long Start, long End)> Sessions(RunOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var sessions = new List<(long Start, long End)>();
        IReadOnlyList<long> starts = options.Sessions.Starts(options.RunSeconds);
        for (int index = 0; index < starts.Count; index++)
        {
            long end = Math.Min(starts[index] + (index == 0 ? options.FirstSessionSeconds : options.SessionSeconds), options.RunSeconds);
            if (sessions.Count > 0 && starts[index] <= sessions[^1].End)
            {
                sessions[^1] = (sessions[^1].Start, Math.Max(sessions[^1].End, end));
            }
            else
            {
                sessions.Add((starts[index], end));
            }
        }

        return sessions;
    }

    /// <summary>The second the player counts as absent from while between check-ins: the last check-in's end plus the grace.</summary>
    /// <param name="sessions">The check-ins.</param>
    /// <param name="next">The index of the next check-in (the last one ended before it).</param>
    /// <param name="graceSeconds">The grace.</param>
    /// <returns>The second, or null before the first check-in.</returns>
    private static long? AbsentFrom(IReadOnlyList<(long Start, long End)> sessions, int next, long graceSeconds) =>
        next == 0 ? null : sessions[next - 1].End + graceSeconds;

    /// <summary>Whether a second lies inside a check-in.</summary>
    /// <param name="sessions">The check-ins.</param>
    /// <param name="second">The second.</param>
    /// <returns>True when the player is present then.</returns>
    private static bool IsPresent(IReadOnlyList<(long Start, long End)> sessions, long second) =>
        sessions.Any(session => session.Start <= second && second < session.End);
}
