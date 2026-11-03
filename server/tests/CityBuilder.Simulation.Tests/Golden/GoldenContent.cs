using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Content.Data;
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
    /// scenarios #1 and #2, with the 8 h offline production cap added (<see cref="WithTheOfflineCap"/>). The file stays as it was frozen.
    /// </summary>
    public static GameContent V2 { get; } = ContentAssert.Valid(WithTheOfflineCap(ContentFile.Read("content-golden-v2.json")));

    /// <summary>
    /// Adds the M0 offline production cap of 28 800 s to <c>content-golden-v2.json</c>, which was frozen without it: CR-0008 removes the cap in
    /// CR-0008-T1 part H, and until then the engine requires it. No golden row reaches 8 h, so it changes no expectation (build log
    /// BL-20261102-07, decision 22). Temporary: part H removes the cap from the engine and this method.
    /// </summary>
    private static GameContentData WithTheOfflineCap(GameContentData v2)
    {
        GameRulesData rules = v2.Rules ?? throw new InvalidOperationException("content-golden-v2.json has no rules.");
        rules.MaxOfflineProductionSeconds = 28_800;
        return v2;
    }
}
