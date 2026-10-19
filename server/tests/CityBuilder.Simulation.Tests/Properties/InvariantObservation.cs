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
    private static readonly ConcurrentDictionary<string, Lazy<InvariantObservation>> _byContent = new(StringComparer.Ordinal);

    private readonly string _contentName;
    private readonly Dictionary<Invariant, string> _failures = [];

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

    private void Fail(Invariant invariant, int seed, string failure, CityHistory? history)
    {
        if (!_failures.ContainsKey(invariant))
        {
            _failures[invariant] = $"Invariant {(int)invariant} ({invariant}) failed on content {_contentName}, seed {seed}: {failure}\n"
                + (history?.Describe() ?? "(the history could not be generated)");
        }
    }

    private void Count(string what) => Counts[what] = Counts.GetValueOrDefault(what) + 1;
}
