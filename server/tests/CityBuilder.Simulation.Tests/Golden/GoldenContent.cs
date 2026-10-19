using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Tests.Content;

namespace CityBuilder.Simulation.Tests.Golden;

/// <summary>
/// The frozen content files of the golden scenarios (<c>08</c>): copies of <c>server/content/mvp-content.json</c> that never change, so a
/// balance change can't rewrite a golden expectation. A content change that should reach a golden scenario gets a new file (v2) and new,
/// hand-checked expectations.
/// </summary>
internal static class GoldenContent
{
    /// <summary><c>Fixtures/content-golden-v1.json</c>: the content <c>mvp-1</c> as it was in M0-11, for golden scenario #1.</summary>
    public static GameContent V1 { get; } = ContentFile.Load("content-golden-v1.json");
}
