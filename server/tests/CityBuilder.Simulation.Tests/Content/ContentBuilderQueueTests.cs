using CityBuilder.Simulation.Content;
using static CityBuilder.Simulation.Tests.Content.ContentFixture;

namespace CityBuilder.Simulation.Tests.Content;

/// <summary>
/// The content rule of the builder queue (CR-0013, <c>04</c> §14): <c>rules.builderQueueLength</c> is optional, 0 when left out (no queue,
/// the behaviour of M0 content), and 0 to 32767 (a stored small value).
/// </summary>
public sealed class ContentBuilderQueueTests
{
    /// <summary>Without <c>builderQueueLength</c> the content has no queue: length 0.</summary>
    [Fact]
    public void Create_WithoutBuilderQueueLength_LengthIsZero() =>
        Assert.Equal(0, ContentAssert.Valid(Valid()).Rules.BuilderQueueLength);

    /// <summary>The length is read as written, at both ends of its range.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(32767)]
    public void Create_WithBuilderQueueLength_KeepsIt(int length) =>
        Assert.Equal(length, ContentAssert.Valid(ValidWith(data => data.Rules!.BuilderQueueLength = length)).Rules.BuilderQueueLength);

    /// <summary>A negative length gives one <see cref="ContentErrorCode.Negative"/>.</summary>
    [Fact]
    public void Create_NegativeBuilderQueueLength_ReportsNegative() =>
        ContentAssert.SingleError(ValidWith(data => data.Rules!.BuilderQueueLength = -1), ContentErrorCode.Negative, "rules.builderQueueLength");

    /// <summary>A length above the stored small value gives one <see cref="ContentErrorCode.ValueTooLarge"/>.</summary>
    [Fact]
    public void Create_BuilderQueueLengthAboveTheStoredLimit_ReportsValueTooLarge() =>
        ContentAssert.SingleError(ValidWith(data => data.Rules!.BuilderQueueLength = 32768), ContentErrorCode.ValueTooLarge, "rules.builderQueueLength");
}
