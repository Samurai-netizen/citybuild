using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Tests.Content;

namespace CityBuilder.Simulation.Tests.Golden;

/// <summary>
/// The frozen content files of the golden scenarios (<c>08</c>): copies of <c>server/content/mvp-content.json</c> that never change, so a
/// balance change can't rewrite a golden expectation. A content change that should reach a golden scenario gets a new file and new,
/// hand-checked expectations.
/// </summary>
internal static class GoldenContent
{
    /// <summary>
    /// <c>Fixtures/content-golden-v2.json</c>: the content <c>mvp-2</c> of CR-0008 (<c>core-instructions/23-mvp-content.md</c>), for golden
    /// scenarios #1 and #2, read as it was frozen (it never had the offline cap that CR-0008-T1 part H removed).
    /// </summary>
    public static GameContent V2 { get; } = ContentAssert.Valid(ContentFile.Read("content-golden-v2.json"));
}
