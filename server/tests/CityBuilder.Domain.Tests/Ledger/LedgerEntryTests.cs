using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation;

namespace CityBuilder.Domain.Tests.Ledger;

/// <summary>The rules of <see cref="LedgerEntry"/>: a real change, no quantity below 0, a defined reason, a whole reference or none.</summary>
public sealed class LedgerEntryTests
{
    /// <summary>The item every test uses.</summary>
    private static readonly ItemId _logs = new("logs");

    /// <summary>A valid entry keeps every value it was given.</summary>
    [Fact]
    public void Constructor_ValidValues_KeepsThem()
    {
        var entry = new LedgerEntry(_logs, -5, 3, LedgerReason.CostPlace, "command", "place-building");

        Assert.Equal(_logs, entry.Item);
        Assert.Equal(-5, entry.Delta);
        Assert.Equal(3, entry.BalanceAfter);
        Assert.Equal(LedgerReason.CostPlace, entry.Reason);
        Assert.Equal("command", entry.RefType);
        Assert.Equal("place-building", entry.RefId);
    }

    /// <summary>A starting grant has no reference, and an entry may take the quantity to exactly 0 or start from exactly 0.</summary>
    [Theory]
    [InlineData(10, 10)]
    [InlineData(-10, 0)]
    public void Constructor_NoReferenceAndQuantityAtZero_IsValid(long delta, long balanceAfter)
    {
        var entry = new LedgerEntry(_logs, delta, balanceAfter, LedgerReason.StartingGrant);

        Assert.Null(entry.RefType);
        Assert.Null(entry.RefId);
    }

    /// <summary>A delta of 0, a quantity after below 0, or a quantity before below 0 (more added than there is now) is rejected.</summary>
    [Theory]
    [InlineData(0, 5)]
    [InlineData(-1, -1)]
    [InlineData(6, 5)]
    public void Constructor_NoChangeOrNegativeQuantity_Throws(long delta, long balanceAfter)
    {
        Assert.Throws<ArgumentException>(() => new LedgerEntry(_logs, delta, balanceAfter, LedgerReason.SettlementProduced));
    }

    /// <summary>A delta at the edge of <see cref="long"/> doesn't overflow the check of the quantity before.</summary>
    [Fact]
    public void Constructor_ExtremeNegativeDelta_IsValid()
    {
        var entry = new LedgerEntry(_logs, long.MinValue, 0, LedgerReason.SettlementConsumed);

        Assert.Equal(long.MinValue, entry.Delta);
    }

    /// <summary>A reason outside the enum is rejected.</summary>
    [Fact]
    public void Constructor_UndefinedReason_Throws()
    {
        Assert.Throws<ArgumentException>(() => new LedgerEntry(_logs, 1, 1, (LedgerReason)99));
    }

    /// <summary>An entry without an item (the default <see cref="ItemId"/>) is rejected.</summary>
    [Fact]
    public void Constructor_DefaultItem_Throws()
    {
        Assert.Throws<ArgumentException>(() => new LedgerEntry(default, 1, 1, LedgerReason.StartingGrant));
    }

    /// <summary>Half a reference, or a blank part of one, is rejected.</summary>
    [Theory]
    [InlineData("command", null)]
    [InlineData(null, "42")]
    [InlineData(" ", "42")]
    [InlineData("command", "")]
    public void Constructor_PartialOrBlankReference_Throws(string? refType, string? refId)
    {
        Assert.Throws<ArgumentException>(() => new LedgerEntry(_logs, 1, 1, LedgerReason.CostResearch, refType, refId));
    }
}
