namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// The random source of the property tests: SplitMix64, written out here so a seed gives the same numbers on every runtime and machine
/// forever (<c>System.Random</c> doesn't promise that across .NET versions), and a printed failing seed reproduces the failure. Test code
/// only: the engine never sees randomness.
/// </summary>
internal sealed class SeededRandom(int seed)
{
    private const ulong _golden = 0x9E3779B97F4A7C15;

    private ulong _state = unchecked((ulong)seed * _golden);

    /// <summary>A number from <paramref name="min"/> to <paramref name="max"/>, both included.</summary>
    public long Between(long min, long max)
    {
        if (max < min)
        {
            throw new ArgumentOutOfRangeException(nameof(max), FormattableString.Invariant($"The range {min}..{max} is empty."));
        }

        // The modulo's bias is below 2^-40 for the ranges the tests use, far too small to matter for picking test cases.
        ulong span = unchecked((ulong)(max - min) + 1);
        return span == 0 ? unchecked((long)Next()) : min + (long)(Next() % span);
    }

    /// <summary>An index into a list of <paramref name="count"/> items (at least 1).</summary>
    public int Index(int count) => (int)Between(0, count - 1);

    /// <summary>One of the items, each equally likely.</summary>
    public T Pick<T>(IReadOnlyList<T> items) => items[Index(items.Count)];

    /// <summary>True with a chance of <paramref name="percent"/> in 100.</summary>
    public bool Chance(int percent) => Between(1, 100) <= percent;

    private ulong Next()
    {
        unchecked
        {
            _state += _golden;
            ulong mixed = _state;
            mixed = (mixed ^ (mixed >> 30)) * 0xBF58476D1CE4E5B9;
            mixed = (mixed ^ (mixed >> 27)) * 0x94D049BB133111EB;
            return mixed ^ (mixed >> 31);
        }
    }
}
