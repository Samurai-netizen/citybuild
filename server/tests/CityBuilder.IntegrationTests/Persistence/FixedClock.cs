namespace CityBuilder.IntegrationTests.Persistence;

/// <summary>A server clock that always shows the same instant, for repository tests that check the wall times they stamp.</summary>
/// <param name="now">The instant <see cref="GetUtcNow"/> returns.</param>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    /// <summary>The instant given to the constructor.</summary>
    /// <returns>Always the same instant.</returns>
    public override DateTimeOffset GetUtcNow() => now;
}
