using CityBuilder.Domain.Metrics;

namespace CityBuilder.Domain.Tests.Metrics;

/// <summary>
/// The rules of <see cref="PlayerEvent"/> (CR-0009, <c>core-instructions/06-data-model.md</c>, <c>player_events</c>): a player, an event
/// type of at most 64 lower-case letters, digits, dots, dashes and underscores, an optional detail and outcome of at most 64 characters,
/// and a city clock of 0 or more. And the names of <see cref="PlayerEventTypes"/>.
/// </summary>
public sealed class PlayerEventTests
{
    /// <summary>The player every test uses.</summary>
    private static readonly Guid _player = Guid.Parse("7d3c2b1a-0000-4000-8000-000000000001");

    /// <summary>The server time every test uses.</summary>
    private static readonly DateTimeOffset _at = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A valid event keeps every value it was given.</summary>
    [Fact]
    public void Constructor_ValidValues_KeepsThem()
    {
        var playerEvent = new PlayerEvent(_player, "building.completed", "woodcutter", "accepted", 2, 3600, _at);

        Assert.Equal(_player, playerEvent.PlayerId);
        Assert.Equal("building.completed", playerEvent.EventType);
        Assert.Equal("woodcutter", playerEvent.Detail);
        Assert.Equal("accepted", playerEvent.Outcome);
        Assert.Equal(2, playerEvent.Value);
        Assert.Equal(3600, playerEvent.CityClock);
        Assert.Equal(_at, playerEvent.OccurredAt);
    }

    /// <summary>Detail, outcome and value may all be missing, and a city clock of 0 (a new city) is valid.</summary>
    [Fact]
    public void Constructor_NoOptionalValuesAndClockZero_IsValid()
    {
        var playerEvent = new PlayerEvent(_player, "session.start", null, null, null, 0, _at);

        Assert.Null(playerEvent.Detail);
        Assert.Null(playerEvent.Outcome);
        Assert.Null(playerEvent.Value);
        Assert.Equal(0, playerEvent.CityClock);
    }

    /// <summary>An event type of exactly 64 allowed characters (the column's limit) is valid, with every allowed kind of character.</summary>
    [Fact]
    public void Constructor_EventTypeOfMaxLengthWithEveryAllowedCharacter_IsValid()
    {
        string eventType = "command.place-building_2" + new string('x', PlayerEvent.MaxLength - 24);

        var playerEvent = new PlayerEvent(_player, eventType, null, null, null, 0, _at);

        Assert.Equal(PlayerEvent.MaxLength, playerEvent.EventType.Length);
    }

    /// <summary>An empty player id is refused: an event always belongs to one player.</summary>
    [Fact]
    public void Constructor_EmptyPlayer_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PlayerEvent(Guid.Empty, "session.start", null, null, null, 0, _at));
    }

    /// <summary>A blank or too long event type, or one with an upper-case letter, a space or another sign, is refused.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Command.place-building")]
    [InlineData("command.place building")]
    [InlineData("command/place-building")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public void Constructor_BadEventType_Throws(string eventType)
    {
        Assert.Throws<ArgumentException>(() => new PlayerEvent(_player, eventType, null, null, null, 0, _at));
    }

    /// <summary>A null event type is refused.</summary>
    [Fact]
    public void Constructor_NullEventType_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => new PlayerEvent(_player, null!, null, null, null, 0, _at));
    }

    /// <summary>A detail that is blank or longer than 64 characters is refused; a missing one is written as null, never as "".</summary>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public void Constructor_BadDetail_Throws(string detail)
    {
        Assert.Throws<ArgumentException>(() => new PlayerEvent(_player, "command.place-building", detail, null, null, 0, _at));
    }

    /// <summary>An outcome that is blank or longer than 64 characters is refused; a missing one is written as null, never as "".</summary>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public void Constructor_BadOutcome_Throws(string outcome)
    {
        Assert.Throws<ArgumentException>(() => new PlayerEvent(_player, "command.place-building", null, outcome, null, 0, _at));
    }

    /// <summary>A detail and an outcome of exactly 64 characters are valid (an error code or a content id at the column's limit).</summary>
    [Fact]
    public void Constructor_DetailAndOutcomeOfMaxLength_AreValid()
    {
        string text = new('a', PlayerEvent.MaxLength);

        var playerEvent = new PlayerEvent(_player, "command.place-building", text, text, null, 0, _at);

        Assert.Equal(text, playerEvent.Detail);
        Assert.Equal(text, playerEvent.Outcome);
    }

    /// <summary>A city clock below 0 is refused: the city clock starts at 0 and only grows.</summary>
    [Fact]
    public void Constructor_NegativeCityClock_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PlayerEvent(_player, "session.start", null, null, null, -1, _at));
    }

    /// <summary>A command's event type is the prefix and the command's wire type.</summary>
    [Fact]
    public void ForCommand_CommandType_IsPrefixedName()
    {
        Assert.Equal("command.place-building", PlayerEventTypes.ForCommand("place-building"));
    }

    /// <summary>A blank command type has no event type.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ForCommand_BlankCommandType_Throws(string commandType)
    {
        Assert.Throws<ArgumentException>(() => PlayerEventTypes.ForCommand(commandType));
    }

    /// <summary>Every fixed event type name is valid for an event, so no recorder can trip over its own constant.</summary>
    [Fact]
    public void FixedEventTypes_AreValidEventTypes()
    {
        string[] names =
        [
            PlayerEventTypes.ResearchCompleted,
            PlayerEventTypes.BuildingCompleted,
            PlayerEventTypes.SessionStart,
            PlayerEventTypes.PlayerCreated,
        ];

        foreach (string name in names)
        {
            Assert.Equal(name, new PlayerEvent(_player, name, null, null, null, 0, _at).EventType);
        }

        Assert.Equal(4, names.Distinct().Count());
    }
}
