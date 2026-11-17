using System.Text.RegularExpressions;

namespace CityBuilder.MetricsReport.Tests;

/// <summary>
/// The report page (docs/plan/CR-0009-T2-PLAN.md §3.4, §4 step 7): its sections, the encoding of every text from the data or the file,
/// no network reference, the sort structure of the explorer, and the same bytes for the same input. The click itself is checked by hand
/// (point A, approved).
/// </summary>
public sealed partial class HtmlReportTests
{
    /// <summary>The report's clock: ten days after <see cref="TestData.Start"/>.</summary>
    private static readonly DateTimeOffset _clock = TestData.Start.AddDays(10);

    /// <summary>The section ids of the page, in their order; the last one only with <c>--player</c>.</summary>
    private static readonly string[] _sections = ["overview", "explorer", "funnels", "actions", "friction", "return", "player"];

    /// <summary>A checkpoint file with a funnel of two steps and a title that tries to open a script.</summary>
    private static readonly CheckpointFile _file = new(
        [
            new("placed_woodcutter", "Placed a <script>alert(1)</script> woodcutter", "command.place-building", "woodcutter", "accepted", null, 1),
            new("came_back", "Came back & stayed", "session.start", null, null, null, 2),
        ],
        [new Funnel("First \"hour\"", ["placed_woodcutter", "came_back"])]);

    /// <summary>
    /// Three players: player 1 placed a woodcutter, was refused a sawmill and left; player 2 came back and stayed; player 3 left after
    /// one session. One event detail carries markup.
    /// </summary>
    /// <returns>The data set.</returns>
    private static MetricsData Data() => new TestData()
        .Player(1, createdAtSecond: 0, lastSeenSecond: 100_000)
        .Player(2, createdAtSecond: 1_000, lastSeenSecond: 800_000)
        .Player(3, createdAtSecond: 2_000, lastSeenSecond: 2_500)
        .Event(1, 10, "session.start")
        .Event(1, 20, "command.place-building", "woodcutter", "accepted", cityClock: 5)
        .Event(1, 30, "command.place-building", "<b>sawmill</b>", "INSUFFICIENT_RESOURCE", cityClock: 8)
        .Event(2, 1_010, "session.start")
        .Event(2, 1_020, "command.place-building", "woodcutter", "accepted", cityClock: 3)
        .Event(2, 90_000, "session.start")
        .Event(2, 90_100, "building.completed", "woodcutter", value: 1, cityClock: 3_600)
        .Event(3, 2_010, "session.start")
        .Build();

    /// <summary>Renders the page of a data set.</summary>
    /// <param name="data">The data.</param>
    /// <param name="player">The player whose timeline is added, or none.</param>
    /// <returns>The page.</returns>
    private static string Render(MetricsData data, Guid? player = null)
    {
        var context = ReportContext.Create(data, _clock, leftAfterDays: 3, minSample: 2);
        OnePlayerTimeline? timeline = player is Guid id ? OnePlayer.Build(context, id) : null;
        return HtmlReport.Render(context, _file, new ReportSettings(null, null, 1, _clock), timeline);
    }

    /// <summary>With <c>--player</c> the page has the seven sections in order.</summary>
    [Fact]
    public void Render_WithPlayer_HasTheSevenSectionsInOrder()
    {
        string page = Render(Data(), TestData.PlayerId(2));

        Assert.Equal(_sections, SectionIds(page));
    }

    /// <summary>Without <c>--player</c> the page has the first six sections and no player id.</summary>
    [Fact]
    public void Render_WithoutPlayer_HasSixSectionsAndNoPlayerId()
    {
        string page = Render(Data());

        Assert.Equal(_sections[..6], SectionIds(page));
        for (int number = 1; number <= 3; number++)
        {
            Assert.DoesNotContain(TestData.PlayerId(number).ToString(), page, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>The player's timeline holds that player's id and events.</summary>
    [Fact]
    public void Render_WithPlayer_ShowsTheTimeline()
    {
        string page = Render(Data(), TestData.PlayerId(2));

        string section = Section(page, "player");
        Assert.Contains(TestData.PlayerId(2).ToString(), section, StringComparison.Ordinal);
        Assert.Contains("building.completed", section, StringComparison.Ordinal);
        Assert.DoesNotContain(TestData.PlayerId(1).ToString(), page, StringComparison.Ordinal);
    }

    /// <summary>A title and a detail with markup come out encoded; the page has one script, its own.</summary>
    [Fact]
    public void Render_TextWithMarkup_IsEncoded()
    {
        string page = Render(Data());

        Assert.Contains("Placed a &lt;script&gt;alert(1)&lt;/script&gt; woodcutter", page, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;sawmill&lt;/b&gt;", page, StringComparison.Ordinal);
        Assert.Contains("Came back &amp; stayed", page, StringComparison.Ordinal);
        Assert.Contains("First &quot;hour&quot;", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", page, StringComparison.Ordinal);
        Assert.Single(ScriptTag().Matches(page));
    }

    /// <summary>The page loads nothing: no address, no source, no stylesheet link, no CSS url, and every link stays in the page.</summary>
    [Fact]
    public void Render_HasNoNetworkReference()
    {
        string page = Render(Data(), TestData.PlayerId(1));

        foreach (string reference in new[] { "http:", "https:", "src=", "<link", "url(", "@import", "fetch(", "XMLHttpRequest" })
        {
            Assert.DoesNotContain(reference, page, StringComparison.OrdinalIgnoreCase);
        }

        Assert.All(Href().Matches(page), href => Assert.StartsWith("#", href.Groups[1].Value, StringComparison.Ordinal));
    }

    /// <summary>Every header of the explorer has a sort key and every cell of its body a <c>data-sort</c>; it has the 15 columns.</summary>
    [Fact]
    public void Render_Explorer_EveryHeaderAndCellSorts()
    {
        string explorer = Section(Render(Data()), "explorer");

        string head = Between(explorer, "<thead>", "</thead>");
        string body = Between(explorer, "<tbody>", "</tbody>");
        string[] headers = [.. HeaderCell().Matches(head).Select(m => m.Value)];
        Assert.Equal(15, headers.Length);
        Assert.All(headers, h => Assert.Contains("data-type=\"", h, StringComparison.Ordinal));
        string[] rows = [.. Row().Matches(body).Select(m => m.Value)];
        Assert.NotEmpty(rows);
        Assert.All(rows, row =>
        {
            string[] cells = [.. BodyCell().Matches(row).Select(m => m.Value)];
            Assert.Equal(15, cells.Length);
            Assert.All(cells, c => Assert.Contains("data-sort=\"", c, StringComparison.Ordinal));
        });
    }

    /// <summary>The explorer has a row per named checkpoint, a kind on each row, and a filter box and a "named only" switch.</summary>
    [Fact]
    public void Render_Explorer_HasNamedRowsAndControls()
    {
        string explorer = Section(Render(Data()), "explorer");

        Assert.Contains("Came back &amp; stayed", explorer, StringComparison.Ordinal);
        Assert.Contains("data-kind=\"named\"", explorer, StringComparison.Ordinal);
        Assert.Contains("data-kind=\"unnamed\"", explorer, StringComparison.Ordinal);
        Assert.Contains("id=\"explorer-filter\"", explorer, StringComparison.Ordinal);
        Assert.Contains("id=\"explorer-named\"", explorer, StringComparison.Ordinal);
        Assert.Contains("<details>", explorer, StringComparison.Ordinal);
    }

    /// <summary>The page's texts say what goes together with leaving, never what causes it.</summary>
    [Fact]
    public void Render_TextsClaimNoCause()
    {
        string page = Render(Data());

        Assert.DoesNotContain("cause", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("because", page, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("goes together with leaving", page, StringComparison.Ordinal);
    }

    /// <summary>The same input gives the same bytes, also when the players and events come in another order.</summary>
    [Fact]
    public void Render_SameInputInAnyOrder_GivesTheSamePage()
    {
        MetricsData data = Data();
        var shuffled = new MetricsData([.. data.Players.Reverse()], [.. data.Events.Reverse()]);

        string first = Render(data, TestData.PlayerId(2));

        Assert.Equal(first, Render(data, TestData.PlayerId(2)));
        Assert.Equal(first, Render(shuffled, TestData.PlayerId(2)));
    }

    /// <summary>An empty population still gives the six sections, with "—" for every share.</summary>
    [Fact]
    public void Render_NoPlayers_GivesTheSixSections()
    {
        string page = Render(new MetricsData([], []));

        Assert.Equal(_sections[..6], SectionIds(page));
        Assert.Contains(Rate.None, page, StringComparison.Ordinal);
    }

    /// <summary>The ids of the page's sections, in order.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The ids.</returns>
    private static string[] SectionIds(string page) => [.. SectionTag().Matches(page).Select(m => m.Groups[1].Value)];

    /// <summary>One section of the page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="id">The section's id.</param>
    /// <returns>Its text from the opening tag to the closing one.</returns>
    private static string Section(string page, string id) => Between(page, $"<section id=\"{id}\">", "</section>");

    /// <summary>The text between the first <paramref name="start"/> and the next <paramref name="end"/>.</summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The opening mark.</param>
    /// <param name="end">The closing mark.</param>
    /// <returns>The text between them.</returns>
    private static string Between(string text, string start, string end)
    {
        int from = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"{start} is missing.");
        from += start.Length;
        int to = text.IndexOf(end, from, StringComparison.Ordinal);
        Assert.True(to >= 0, $"{end} is missing.");
        return text[from..to];
    }

    /// <summary>A section's opening tag.</summary>
    /// <returns>The pattern; group 1 is the id.</returns>
    [GeneratedRegex("<section id=\"([a-z]+)\">")]
    private static partial Regex SectionTag();

    /// <summary>A script's opening tag.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("<script", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptTag();

    /// <summary>A link target.</summary>
    /// <returns>The pattern; group 1 is the target.</returns>
    [GeneratedRegex("href=\"([^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex Href();

    /// <summary>A header cell's opening tag.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("<th[ >][^>]*>")]
    private static partial Regex HeaderCell();

    /// <summary>A body row, from its opening tag to its closing one.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("<tr[ >].*?</tr>", RegexOptions.Singleline)]
    private static partial Regex Row();

    /// <summary>A body cell's opening tag (a detail block holds lists, no table, so every cell of a row is a column).</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("<td[ >][^>]*>")]
    private static partial Regex BodyCell();
}
