using System.Collections.Concurrent;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// Observes every property seed of one content once (<see cref="InvariantObserver"/>, <see cref="InvariantChecks"/>) and keeps, per
/// invariant, the first failure with its seed, step and operation list, plus the counts of what the checks exercised. The stepped replay is
/// too slow to repeat for every invariant, so the result is cached per content and each invariant's test reads it. A seed whose replay throws
/// fails every invariant with the exception.
/// </summary>
internal sealed class InvariantObservation
{
    /// <summary>The observation of each content, by content name, made once on first use, also when several tests ask at the same time.</summary>
    private static readonly ConcurrentDictionary<string, Lazy<InvariantObservation>> _byContent = new(StringComparer.Ordinal);

    /// <summary>The name of the observed content, for failure messages.</summary>
    private readonly string _contentName;
    /// <summary>The first failure of each invariant that failed, with its seed and the history.</summary>
    private readonly Dictionary<Invariant, string> _failures = [];

    /// <summary>Observes every seed of the named content: generates the history, replays it step by step, and records failures and counts.</summary>
    private InvariantObservation(string contentName)
    {
        _contentName = contentName;
        GameContent content = PropertyContent.Named(contentName);
        for (int seed = 0; seed < PropertyContent.Seeds; seed++)
        {
            CityHistory? history = null;
            try
            {
                history = CityGenerator.Generate(content, seed);
                var checks = new InvariantChecks(content, (invariant, failure) => Fail(invariant, seed, failure, history), Count);

                // As in PropertyCheck, the observer's own random numbers come from the complement of the seed.
                InvariantObserver.Observe(content, history, new SeededRandom(~seed), checks);
            }
            catch (Exception exception)
            {
                foreach (Invariant invariant in Enum.GetValues<Invariant>())
                {
                    Fail(invariant, seed, $"the replay threw {exception}", history);
                }
            }
        }
    }

    /// <summary>How often each counted case happened over all seeds.</summary>
    public SortedDictionary<string, int> Counts { get; } = new(StringComparer.Ordinal);

    /// <summary>The observation of the named content, made on the first call and cached for every later one.</summary>
    public static InvariantObservation For(string contentName) =>
        _byContent.GetOrAdd(contentName, name => new Lazy<InvariantObservation>(() => new InvariantObservation(name))).Value;

    /// <summary>Fails with the first failure of <paramref name="invariant"/>, if any seed had one.</summary>
    public void AssertHolds(Invariant invariant)
    {
        if (_failures.TryGetValue(invariant, out string? failure))
        {
            Assert.Fail(failure);
        }
    }

    /// <summary>Fails unless the case was counted at least <paramref name="floor"/> times; the message lists every count.</summary>
    public void AssertAtLeast(string what, int floor) =>
        Assert.True(Counts.GetValueOrDefault(what) >= floor, $"Content {_contentName}: fewer than {floor} {what}. Observed: {CountsText()}");

    /// <summary>Every count, as one line.</summary>
    public string CountsText() => string.Join(", ", Counts.Select(count => $"{count.Key} {count.Value}"));

    /// <summary>Records the failure when it is the invariant's first: the seed, the failure, and the generated history when there is one.</summary>
    private void Fail(Invariant invariant, int seed, string failure, CityHistory? history)
    {
        if (!_failures.ContainsKey(invariant))
        {
            _failures[invariant] = $"Invariant {(int)invariant} ({invariant}) failed on content {_contentName}, seed {seed}: {failure}\n"
                + (history?.Describe() ?? "(the history could not be generated)");
        }
    }

    /// <summary>Counts one more case of <paramref name="what"/>.</summary>
    private void Count(string what) => Counts[what] = Counts.GetValueOrDefault(what) + 1;
}
