using CityBuilder.Infrastructure.Content;
using CityBuilder.Simulation.Content;

namespace CityBuilder.MetricsReport.Tests;

/// <summary>
/// The checkpoint file (docs/plan/CR-0009-T2-PLAN.md §3.2): what a valid file gives, every refusal with the line it names, and the
/// committed <c>server/analytics/checkpoints.json</c> against the committed content.
/// </summary>
public sealed class CheckpointFileTests
{
    /// <summary>The repository root, found from the test binaries.</summary>
    private static readonly string _root = RepositoryRoot.Find(AppContext.BaseDirectory);

    /// <summary>The committed content, loaded once: the ids a <c>detail</c> may name.</summary>
    private static readonly GameContent _content = ContentFileLoader.Load(Path.Combine(_root, "server", "content", "mvp-content.json"));

    /// <summary>Every field of a checkpoint is read, <c>atLeast</c> defaults to 1, and funnels keep their step order.</summary>
    [Fact]
    public void Parse_ValidFile_GivesCheckpointsAndFunnels()
    {
        const string json = """
            {
              "checkpoints": [
                { "id": "placed_woodcutter", "title": "Placed a woodcutter", "event": "command.place-building", "detail": "woodcutter", "outcome": "accepted" },
                { "id": "sawmill_level_2", "title": "A sawmill reached level 2", "event": "building.completed", "detail": "sawmill", "minValue": 2 },
                { "id": "came_back", "title": "Came back", "event": "session.start", "atLeast": 2 },
                { "id": "made_planks", "title": "Made planks", "event": "production.first", "detail": "planks" }
              ],
              "funnels": [ { "name": "First session", "steps": ["came_back", "placed_woodcutter"] } ]
            }
            """;

        CheckpointFile file = CheckpointFile.Parse(json, _content);

        Assert.Equal(4, file.Checkpoints.Count);
        Assert.Equal(
            new Checkpoint("placed_woodcutter", "Placed a woodcutter", "command.place-building", "woodcutter", "accepted", null, 1),
            file.Checkpoints[0]);
        Assert.Equal(new Checkpoint("sawmill_level_2", "A sawmill reached level 2", "building.completed", "sawmill", null, 2, 1), file.Checkpoints[1]);
        Assert.Equal(2, file.Checkpoints[2].AtLeast);
        Assert.Equal("production.first", file.Checkpoints[3].Event);
        Funnel funnel = Assert.Single(file.Funnels);
        Assert.Equal("First session", funnel.Name);
        Assert.Equal(["came_back", "placed_woodcutter"], funnel.Steps);
    }

    /// <summary>An empty object and empty lists are valid files with nothing in them.</summary>
    /// <param name="json">The file's text.</param>
    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "checkpoints": [], "funnels": [] }""")]
    [InlineData("""{ "checkpoints": [] }""")]
    public void Parse_EmptyFile_IsValid(string json)
    {
        CheckpointFile file = CheckpointFile.Parse(json, _content);

        Assert.Empty(file.Checkpoints);
        Assert.Empty(file.Funnels);
    }

    /// <summary>Each broken file is refused with a message that names the line of the problem and the problem.</summary>
    /// <param name="json">The file's text; <c>|</c> stands for a line break.</param>
    /// <param name="line">The line the message must name.</param>
    /// <param name="message">A part of the expected message.</param>
    [Theory]
    [InlineData("""{|"checkpoints": [|{ "id": "a", |]|}""", 4, "not valid JSON")]
    [InlineData("""[]""", 1, "object")]
    [InlineData("""{|"checkpoints": [],|"extra": 1|}""", 3, "Unknown field \"extra\"")]
    [InlineData("""{|"checkpoints": [|{ "id": "a", "title": "A", "event": "session.start", "colour": "red" }|]|}""", 3, "Unknown field \"colour\"")]
    [InlineData("""{|"checkpoints": [|{ "title": "A", "event": "session.start" }|]|}""", 3, "\"id\" is missing")]
    [InlineData("""{|"checkpoints": [|{ "id": "a", "event": "session.start" }|]|}""", 3, "\"title\" is missing")]
    [InlineData("""{|"checkpoints": [|{ "id": "a", "title": "A" }|]|}""", 3, "\"event\" is missing")]
    [InlineData("""{|"checkpoints": [|{ "id": "Bad-Id", "title": "A", "event": "session.start" }|]|}""", 3, "\"Bad-Id\"")]
    [InlineData("""{|"checkpoints": [|{ "id": "a", "title": "A", "event": "session.start" },|{ "id": "a", "title": "B", "event": "session.start" }|]|}""", 4, "Duplicate id \"a\"")]
    [InlineData("""{|"checkpoints": [|{ "id": "a", "title": "A", "event": "command.fly-away" }|]|}""", 3, "Unknown event \"command.fly-away\"")]
    [InlineData("""{|"checkpoints": [|{ "id": "a", "title": "A", "event": "session.ended" }|]|}""", 3, "Unknown event \"session.ended\"")]
    [InlineData("""{|"checkpoints": [|{ "id": "a", "title": "A", "event": "research.completed", "detail": "time_travel" }|]|}""", 3, "\"time_travel\"")]
    [InlineData("""{|"checkpoints": [|{ "id": "a", "title": "A", "event": "session.start", "atLeast": 0 }|]|}""", 3, "\"atLeast\"")]
    [InlineData("""{|"checkpoints": [|{ "id": "a", "title": "A", "event": "session.start", "atLeast": "two" }|]|}""", 3, "\"atLeast\"")]
    [InlineData("""{|"checkpoints": [|{ "id": "a", "title": "", "event": "session.start" }|]|}""", 3, "\"title\"")]
    [InlineData("""{|"checkpoints": [|{ "id": "a", "title": "A", "event": "session.start" }|],|"funnels": [|{ "name": "F", "steps": ["a", "b"] }|]|}""", 6, "\"b\"")]
    [InlineData("""{|"checkpoints": [|{ "id": "a", "title": "A", "event": "session.start" }|],|"funnels": [|{ "name": "F", "steps": ["a", "a"] }|]|}""", 6, "twice")]
    [InlineData("""{|"funnels": [|{ "name": "F" }|]|}""", 3, "\"steps\" is missing")]
    public void Parse_BrokenFile_NamesTheLine(string json, int line, string message)
    {
        CheckpointFileException exception = Assert.Throws<CheckpointFileException>(
            () => CheckpointFile.Parse(json.Replace('|', '\n'), _content, "test.json"));

        Assert.Equal(line, exception.Line);
        Assert.Contains($"test.json, line {line}: ", exception.Message, StringComparison.Ordinal);
        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>A missing file is refused with its path.</summary>
    [Fact]
    public void Load_MissingFile_IsRefused()
    {
        string path = Path.Combine(Path.GetTempPath(), $"checkpoints-{Guid.NewGuid():N}.json");

        CheckpointFileException exception = Assert.Throws<CheckpointFileException>(() => CheckpointFile.Load(path, _content));

        Assert.Contains(path, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The committed checkpoint file is valid against the committed content, and holds the starter set (point C of the plan): a
    /// placement of each building type, a completion of each research, and a first production of each item but the currency.
    /// </summary>
    [Fact]
    public void CommittedFile_IsValidAndHoldsTheStarterSet()
    {
        CheckpointFile file = CheckpointFile.Load(Path.Combine(_root, "server", "analytics", "checkpoints.json"), _content);

        foreach (var building in _content.Buildings)
        {
            Assert.Contains(file.Checkpoints, c => c.Event == "command.place-building" && c.Detail == building.Id.Value && c.Outcome == "accepted");
        }

        foreach (var research in _content.Research)
        {
            Assert.Contains(file.Checkpoints, c => c.Event == "research.completed" && c.Detail == research.Id.Value);
        }

        foreach (var item in _content.Items.Where(item => item.Id != _content.Currency))
        {
            Assert.Contains(file.Checkpoints, c => c.Event == KnownEvents.ProductionFirst && c.Detail == item.Id.Value);
        }

        Assert.Contains(file.Checkpoints, c => c.Id == "came_back" && c.AtLeast == 2);
        Assert.Contains(file.Checkpoints, c => c.Id == "came_back_5" && c.AtLeast == 5);
        Assert.Contains(file.Funnels, f => f.Name == "First session");
    }

    /// <summary>The known events are the commands' types, the settlement's and session's names, and the first production.</summary>
    [Fact]
    public void KnownEvents_HoldEveryCommandAndRecordedName()
    {
        Assert.True(KnownEvents.IsKnown("command.place-building"));
        Assert.True(KnownEvents.IsKnown("command.start-research"));
        Assert.True(KnownEvents.IsKnown("command.set-route"));
        Assert.True(KnownEvents.IsKnown("research.completed"));
        Assert.True(KnownEvents.IsKnown("building.completed"));
        Assert.True(KnownEvents.IsKnown("session.start"));
        Assert.True(KnownEvents.IsKnown("player.created"));
        Assert.True(KnownEvents.IsKnown("production.first"));
        Assert.False(KnownEvents.IsKnown("command."));
        Assert.False(KnownEvents.IsKnown("accepted"));
    }
}
