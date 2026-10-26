using CityBuilder.Domain.Commands;

namespace CityBuilder.Domain.Tests.Commands;

/// <summary>The rules of <see cref="CommandReceipt"/>: a request id, a command type, a SHA-256 hash and a response.</summary>
public sealed class CommandReceiptTests
{
    /// <summary>A request id for the tests.</summary>
    private static readonly Guid _requestId = new("0b7e1a52-0000-4000-8000-000000000001");

    /// <summary>A valid receipt keeps its values, and its hash is a copy the caller can't change afterwards.</summary>
    [Fact]
    public void Constructor_ValidValues_KeepsThemAndCopiesTheHash()
    {
        byte[] hash = Enumerable.Range(0, CommandReceipt.PayloadHashLength).Select(value => (byte)value).ToArray();

        var receipt = new CommandReceipt(_requestId, "place-building", hash, "{}");
        hash[0] = 255;

        Assert.Equal(_requestId, receipt.RequestId);
        Assert.Equal("place-building", receipt.CommandType);
        Assert.Equal("{}", receipt.ResponseJson);
        Assert.Equal(0, receipt.PayloadHash[0]);
        Assert.Equal(CommandReceipt.PayloadHashLength, receipt.PayloadHash.Length);
    }

    /// <summary>A hash that isn't 32 bytes long is rejected.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(33)]
    public void Constructor_HashOfWrongLength_Throws(int length)
    {
        Assert.Throws<ArgumentException>(() => new CommandReceipt(_requestId, "place-building", new byte[length], "{}"));
    }

    /// <summary>An empty request id, a blank command type or a blank response is rejected.</summary>
    [Fact]
    public void Constructor_MissingValues_Throw()
    {
        byte[] hash = new byte[CommandReceipt.PayloadHashLength];

        Assert.Throws<ArgumentException>(() => new CommandReceipt(Guid.Empty, "place-building", hash, "{}"));
        Assert.Throws<ArgumentException>(() => new CommandReceipt(_requestId, " ", hash, "{}"));
        Assert.Throws<ArgumentException>(() => new CommandReceipt(_requestId, "place-building", hash, ""));
    }
}
