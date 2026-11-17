using System.Globalization;

namespace CityBuilder.MetricsReport;

/// <summary>
/// The report page (docs/plan/CR-0009-T2-PLAN.md §3.4): one self-contained HTML file with, in order, the overview, the checkpoint
/// explorer, the funnels, the actions, the friction, the return and (with <c>--player</c>) one player's timeline. It loads nothing,
/// encodes every text from the data or the checkpoint file, shows player ids only in the last section and never a dev name, and
/// writes the same bytes for the same input.
/// </summary>
public static class HtmlReport
{
    /// <summary>The reading note above the explorer (the CR's Risks): the numbers show what goes together, not what makes it happen.</summary>
    private const string _readingNote =
        "The \"left\" columns show what goes together with leaving, not what makes players leave: a late checkpoint is reached only by "
        + "players who stayed long, an early one by almost everyone. Read the counts next to every percentage, and a row's detail (what "
        + "the players who left did last) before changing the design.";

    /// <summary>Writes the page.</summary>
    /// <param name="context">The population and its numbers' context.</param>
    /// <param name="file">The checkpoint file.</param>
    /// <param name="settings">What the overview says about the run.</param>
    /// <param name="player">The timeline of <c>--player</c>, or none.</param>
    /// <returns>The page.</returns>
    public static string Render(ReportContext context, CheckpointFile file, ReportSettings settings, OnePlayerTimeline? player)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(settings);
        var html = new HtmlBuilder(context.MinSample);
        html.Line("<!doctype html>")
            .Line("<html lang=\"en\">")
            .Line("<head>")
            .Line("<meta charset=\"utf-8\">")
            .Line("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
            .Line("<title>Player metrics</title>")
            .Line("<style>").Line(HtmlAssets.Style).Line("</style>")
            .Line("</head>")
            .Line("<body>")
            .Line("<h1>Player metrics</h1>")
            .Line("<nav><a href=\"#overview\">Overview</a><a href=\"#explorer\">Checkpoint explorer</a><a href=\"#funnels\">Funnels</a>"
                + "<a href=\"#actions\">Actions</a><a href=\"#friction\">Friction</a><a href=\"#return\">Return</a>"
                + (player is null ? "" : "<a href=\"#player\">One player</a>") + "</nav>");
        Overview(html, context, settings);
        ExplorerSection(html, context, file);
        FunnelsSection(html, context, file);
        ActionsSection(html, context);
        FrictionSection(html, context);
        ReturnSection(html, context);
        if (player is not null)
        {
            PlayerSection(html, player);
        }

        return html.Line("<script>").Line(HtmlAssets.Script).Line("</script>").Line("</body>").Line("</html>").ToString();
    }

    /// <summary>The overview: the population, who was seen lately, who left, the events, the run's settings and the definitions.</summary>
    /// <param name="html">The page.</param>
    /// <param name="context">The report's context.</param>
    /// <param name="settings">The run's settings.</param>
    private static void Overview(HtmlBuilder html, ReportContext context, ReportSettings settings)
    {
        html.Line("<section id=\"overview\">").Line("<h2>Overview</h2>").Line("<dl>");
        Term(html, "Players", Count(context.Population));
        foreach (int days in new[] { 1, 7, 30 })
        {
            int seen = context.Timelines.Count(t => context.Now.ToUnixTimeSeconds() - t.Player.LastSeenUnix < days * 86_400L);
            Term(html, $"Seen in the last {Count(days)} day{(days == 1 ? "" : "s")}",
                $"{Count(seen)} ({html.RateText(context.Percent(seen, context.Population))})");
        }

        Term(html, "Left", $"{Count(context.LeftCount)} ({html.RateText(context.Percent(context.LeftCount, context.Population))})");
        Term(html, "Events", Count(context.Timelines.Sum(t => (long)t.Events.Count)));
        Term(html, "Period (creation day, UTC)", $"{Day(settings.From)} to {Day(settings.To)}");
        Term(html, "Left after", $"{Count(context.LeftAfterDays)} days unseen");
        Term(html, "Small sample", $"under {Count(context.MinSample)} players <span class=\"small\">*</span>");
        Term(html, "Dev names excluded", Count(settings.ExcludedNames));
        Term(html, "Report made", Time(settings.Clock));
        Term(html, "Now (for \"left\")", Time(context.Now));
        html.Line("</dl>")
            .Line("<details><summary>How to read this</summary><ul class=\"note\">")
            .Line("<li><strong>Population:</strong> the players created in the period, minus the excluded dev names; every percentage is of it unless it says otherwise.</li>")
            .Line("<li><strong>Now:</strong> the later of the machine's clock and the newest last seen (a Development server with a fast clock runs ahead).</li>")
            .Line("<li><strong>Left:</strong> not seen for at least the \"left after\" days before now.</li>")
            .Line("<li><strong>Reached:</strong> at least <code>atLeast</code> matching events; the reach is that many-th match in time order.</li>")
            .Line("<li><strong>Unnamed checkpoints:</strong> every distinct event type / detail / outcome in the data.</li>")
            .Line("<li><strong>Last stop:</strong> for a player who left, the checkpoint with their latest reach, once among the named and once among the unnamed ones.</li>")
            .Line("<li><strong>Funnel step:</strong> the player reached every step up to it, each at or after the step before.</li>")
            .Line("<li><strong>Percentages</strong> have one decimal, rounded half up; over nobody they show " + Rate.None + ". <strong>Medians</strong> are the lower middle value.</li>")
            .Line("</ul></details>")
            .Line("</section>");
    }

    /// <summary>The checkpoint explorer: one row per named and unnamed checkpoint, sortable and filterable, with its detail block.</summary>
    /// <param name="html">The page.</param>
    /// <param name="context">The report's context.</param>
    /// <param name="file">The checkpoint file.</param>
    private static void ExplorerSection(HtmlBuilder html, ReportContext context, CheckpointFile file)
    {
        html.Line("<section id=\"explorer\">").Line("<h2>Checkpoint explorer</h2>")
            .Raw("<p class=\"note\">").Text(_readingNote).Line("</p>")
            .Line("<p class=\"note\">Click a header to sort (descending, then ascending); click a title to open its detail.</p>")
            .Line("<div class=\"controls\"><input id=\"explorer-filter\" type=\"search\" placeholder=\"Filter by title\" aria-label=\"Filter by title\">"
                + "<label><input id=\"explorer-named\" type=\"checkbox\"> Named only</label></div>")
            .Line("<div class=\"scroll\">")
            .TableStart(
                "explorer-table",
                ("Checkpoint", false), ("Kind", false), ("Reached", true), ("Reached %", true), ("Times", true),
                ("Median times per player", true), ("Median time to reach", true), ("Median city clock at reach", true),
                ("Left after reaching %", true), ("Left without it %", true), ("Difference", true), ("Last stop", true),
                ("Last stop % of left", true), ("Median play after reach", true), ("Median sessions after reach", true));
        foreach (ExplorerRow row in Explorer.Build(context, file))
        {
            ReportCheckpoint checkpoint = row.Checkpoint;
            string kind = checkpoint.IsNamed ? "named" : "unnamed";
            html.Line($"<tr data-kind=\"{kind}\">").Raw($"<td data-sort=\"{HtmlBuilder.Encode(checkpoint.Title)}\">");
            if (row.Detail is null)
            {
                TitleText(html, context, row);
            }
            else
            {
                html.Raw("<details><summary>");
                TitleText(html, context, row);
                html.Raw("</summary>");
                DetailList(html, "Next events after the reach", row.Detail.NextEvents);
                DetailList(html, "Last events of the players who left after it", row.Detail.LastEventsOfLeft);
                DetailList(html, "Other named checkpoints these players reached", row.Detail.OtherCheckpointsOfLeft);
                html.Raw("</details>");
            }

            html.Line("</td>")
                .TextCell(kind)
                .NumberCell(row.Reached)
                .RateCell(row.ReachedShare, bar: true)
                .NumberCell(row.TimesTotal)
                .NumberCell(row.MedianTimesPerPlayer)
                .DurationCell(row.MedianSecondsToReach)
                .DurationCell(row.MedianCityClockAtReach)
                .RateCell(row.LeftAfter)
                .RateCell(row.LeftWithout)
                .RateCell(row.Difference, "pp")
                .NumberCell(row.LastStop)
                .RateCell(row.LastStopShare)
                .DurationCell(row.MedianSecondsAfterReach)
                .NumberCell(row.MedianSessionsAfterReach)
                .Line("</tr>");
        }

        html.TableEnd().Line("</div>").Line("</section>");
    }

    /// <summary>A row's title: the checkpoint's title, a named one's id, and the small-sample tag when few players reached it.</summary>
    /// <param name="html">The page.</param>
    /// <param name="context">The report's context.</param>
    /// <param name="row">The row.</param>
    private static void TitleText(HtmlBuilder html, ReportContext context, ExplorerRow row)
    {
        html.Text(row.Checkpoint.Title);
        if (row.Checkpoint.IsNamed)
        {
            html.Raw(" <code>").Text(row.Checkpoint.Key).Raw("</code>");
        }

        if (row.Reached < context.MinSample)
        {
            html.Raw("<span class=\"tag\">small sample</span>");
        }
    }

    /// <summary>One list of a detail block: each line an event or checkpoint with its players and share.</summary>
    /// <param name="html">The page.</param>
    /// <param name="heading">The list's heading.</param>
    /// <param name="lines">The lines.</param>
    private static void DetailList(HtmlBuilder html, string heading, IReadOnlyList<DetailCount> lines)
    {
        html.Raw("<p>").Text(heading).Raw("</p>");
        if (lines.Count == 0)
        {
            html.Raw("<ol><li>none</li></ol>");
            return;
        }

        html.Raw("<ol>");
        foreach (DetailCount line in lines)
        {
            html.Raw("<li>").Text(line.Title).Raw($": {Count(line.Players)} players ({html.RateText(line.Share)})</li>");
        }

        html.Raw("</ol>");
    }

    /// <summary>The funnels of the checkpoint file, one table each, or a line saying the file names none.</summary>
    /// <param name="html">The page.</param>
    /// <param name="context">The report's context.</param>
    /// <param name="file">The checkpoint file.</param>
    private static void FunnelsSection(HtmlBuilder html, ReportContext context, CheckpointFile file)
    {
        html.Line("<section id=\"funnels\">").Line("<h2>Funnels</h2>");
        IReadOnlyList<FunnelTable> funnels = Funnels.Build(context, file);
        if (funnels.Count == 0)
        {
            html.Line("<p class=\"note\">The checkpoint file names no funnel.</p>");
        }

        foreach (FunnelTable funnel in funnels)
        {
            html.Raw("<h3>").Text(funnel.Name).Line("</h3>")
                .TableStart(
                    null, ("Step", true), ("Checkpoint", false), ("Reached", true), ("% of all", true), ("% of the step before", true),
                    ("Drop", true), ("Median time from the step before", true));
            for (int index = 0; index < funnel.Steps.Count; index++)
            {
                FunnelStep step = funnel.Steps[index];
                html.Line("<tr>").NumberCell(index + 1).TextCell(step.Title).NumberCell(step.Reached).RateCell(step.OfPopulation, bar: true)
                    .RateCell(step.OfPrevious).NumberCell(step.Drop).DurationCell(step.MedianSecondsFromPrevious).Line("</tr>");
            }

            html.TableEnd();
        }

        html.Line("</section>");
    }

    /// <summary>The actions: per event type, the players who did it, the times and the median per player.</summary>
    /// <param name="html">The page.</param>
    /// <param name="context">The report's context.</param>
    private static void ActionsSection(HtmlBuilder html, ReportContext context)
    {
        html.Line("<section id=\"actions\">").Line("<h2>Actions</h2>")
            .TableStart(null, ("Event type", false), ("Players", true), ("% of all", true), ("Times", true), ("Median per player", true));
        foreach (ActionRow row in Actions.Build(context))
        {
            html.Line("<tr>").TextCell(row.EventType).NumberCell(row.Players).RateCell(row.Share, bar: true).NumberCell(row.Times)
                .NumberCell(row.MedianPerPlayer).Line("</tr>");
        }

        html.TableEnd().Line("</section>");
    }

    /// <summary>The friction: refused commands, the last event of each player who left, and the refusals in their last session.</summary>
    /// <param name="html">The page.</param>
    /// <param name="context">The report's context.</param>
    private static void FrictionSection(HtmlBuilder html, ReportContext context)
    {
        FrictionReport friction = Friction.Build(context);
        html.Line("<section id=\"friction\">").Line("<h2>Friction</h2>").Line("<h3>Refused commands</h3>");
        RefusalTable(html, friction.Refusals, "% of all");
        html.Line("<h3>The last event of each player who left</h3>")
            .TableStart(null, ("Event", false), ("Players", true), ("% of those who left", true));
        foreach (DetailCount line in friction.LastEvents)
        {
            html.Line("<tr>").TextCell(line.Title).NumberCell(line.Players).RateCell(line.Share, bar: true).Line("</tr>");
        }

        html.TableEnd().Line("<h3>Refusals in the last session of the players who left</h3>");
        RefusalTable(html, friction.LastSessionRefusals, "% of those who left");
        html.Line("</section>");
    }

    /// <summary>A table of refusals by command and error code.</summary>
    /// <param name="html">The page.</param>
    /// <param name="rows">The rows.</param>
    /// <param name="shareTitle">The share column's title (what it is a share of).</param>
    private static void RefusalTable(HtmlBuilder html, IReadOnlyList<FrictionRow> rows, string shareTitle)
    {
        html.TableStart(null, ("Command", false), ("Error code", false), ("Players", true), (shareTitle, true), ("Times", true));
        foreach (FrictionRow row in rows)
        {
            html.Line("<tr>").TextCell(row.EventType).TextCell(row.Outcome).NumberCell(row.Players).RateCell(row.Share, bar: true)
                .NumberCell(row.Times).Line("</tr>");
        }

        html.TableEnd();
    }

    /// <summary>The return: per creation day, the players and the share with a session start on day 1, 3 and 7 after it.</summary>
    /// <param name="html">The page.</param>
    /// <param name="context">The report's context.</param>
    private static void ReturnSection(HtmlBuilder html, ReportContext context)
    {
        html.Line("<section id=\"return\">").Line("<h2>Return</h2>")
            .Line($"<p class=\"note\">{Rate.None} while that day has not ended.</p>")
            .TableStart(null, [("Creation day (UTC)", false), ("Players", true), .. Returns.Days.Select(d => ($"Day {Count(d)} %", true))]);
        foreach (ReturnRow row in Returns.Build(context))
        {
            html.Line("<tr>").TextCell(Day(row.Day)).NumberCell(row.Players);
            foreach (Rate rate in row.Returned)
            {
                html.RateCell(rate);
            }

            html.Line("</tr>");
        }

        html.TableEnd().Line("</section>");
    }

    /// <summary>One player's timeline (<c>--player</c>): the only section with a player id.</summary>
    /// <param name="html">The page.</param>
    /// <param name="player">The timeline.</param>
    private static void PlayerSection(HtmlBuilder html, OnePlayerTimeline player)
    {
        html.Line("<section id=\"player\">").Line("<h2>One player</h2>")
            .Raw("<p>Player <code>").Text(player.PlayerId.ToString()).Raw($"</code>: {Count(player.TotalEvents)} events")
            .Raw(player.TotalEvents > player.Rows.Count ? $", the first {Count(player.Rows.Count)} shown" : "").Line(".</p>")
            .TableStart(null, ("Time (UTC)", false), ("City clock", true), ("Event", false), ("Detail", false), ("Outcome", false), ("Value", true));
        foreach (MetricsEvent row in player.Rows)
        {
            html.Line("<tr>").TextCell(Time(row.CreatedAt)).DurationCell(row.CityClock).TextCell(row.EventType)
                .TextCell(row.Detail ?? "").TextCell(row.Outcome ?? "").NumberCell(row.Value).Line("</tr>");
        }

        html.TableEnd().Line("</section>");
    }

    /// <summary>One line of the overview's list.</summary>
    /// <param name="html">The page.</param>
    /// <param name="term">The term (no data in it).</param>
    /// <param name="markup">Its value, markup the report made (numbers, dates, marks).</param>
    private static void Term(HtmlBuilder html, string term, string markup) => html.Raw("<dt>").Text(term).Raw("</dt><dd>").Raw(markup).Line("</dd>");

    /// <summary>A whole number as the page writes it.</summary>
    /// <param name="value">The number.</param>
    /// <returns>Its digits.</returns>
    private static string Count(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A day as the page writes it, or "any" for none.</summary>
    /// <param name="day">The day.</param>
    /// <returns>The text.</returns>
    private static string Day(DateOnly? day) => day?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "any";

    /// <summary>A moment as the page writes it, in UTC to the second.</summary>
    /// <param name="time">The moment.</param>
    /// <returns>The text.</returns>
    private static string Time(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
}

/// <summary>What the overview says about the run, apart from what the context holds.</summary>
/// <param name="From">The first creation day of the population, or none.</param>
/// <param name="To">The last creation day of the population, or none.</param>
/// <param name="ExcludedNames">How many dev names were excluded (the names themselves never appear: ids only).</param>
/// <param name="Clock">The machine's clock when the report ran.</param>
public sealed record ReportSettings(DateOnly? From, DateOnly? To, int ExcludedNames, DateTimeOffset Clock);
