using CityBuilder.Application.Commands;
using CityBuilder.Domain.Ledger;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;

namespace CityBuilder.Application.Tests.Commands;

/// <summary>
/// The canonical payload of a command and its SHA-256 hash (<see cref="CommandPayload"/>; <c>05</c>, idempotency rules): fixed text, pinned
/// hash, independent of the order the properties are declared in, and different for another value or another command type.
/// </summary>
public sealed class CommandPayloadTests
{
    /// <summary>
    /// The payload is the command type, a line break, and the data properties by ordinal name, without the members of
    /// <see cref="ICityCommand"/> that describe the type (<c>CommandType</c>, <c>CostReason</c>, <c>RefundReason</c>).
    /// </summary>
    [Fact]
    public void Canonical_PlaceCommand_IsTheTypeAndTheDataInOrdinalOrder()
    {
        Assert.Equal(
            "place-test\n{\"BuildingTypeId\":\"cottage\",\"RequestId\":\"0b7e9c4a-0000-4000-8000-00000000000a\",\"X\":1,\"Y\":1}",
            CommandPayload.Canonical(PipelineFixture.PlaceCottage()));
    }

    /// <summary>
    /// The hash is pinned: it was computed outside the code (<c>printf … | shasum -a 256</c> over the text above), so a change of the format,
    /// which would turn every stored receipt into a conflict, fails here.
    /// </summary>
    [Fact]
    public void Hash_PlaceCommand_IsThePinnedSha256()
    {
        Assert.Equal(
            "dea37823a9909e82e2259870e2d44c8070016e085b820fb6f1235ca88ecdf672",
            Convert.ToHexStringLower(CommandPayload.Hash(PipelineFixture.PlaceCottage())));
    }

    /// <summary>The same data declared in another order hashes the same: the order is the names', not the code's.</summary>
    [Fact]
    public void Hash_PropertiesDeclaredInAnotherOrder_IsTheSame()
    {
        var reordered = new ReorderedPlaceCommand(1, 1, "cottage", PipelineFixture.RequestId);

        Assert.Equal(CommandPayload.Hash(PipelineFixture.PlaceCottage()), CommandPayload.Hash(reordered));
    }

    /// <summary>Another value of any data property gives another hash.</summary>
    [Fact]
    public void Hash_AnotherValue_Differs()
    {
        PlaceTestCommand command = PipelineFixture.PlaceCottage();
        byte[] hash = CommandPayload.Hash(command);

        Assert.NotEqual(hash, CommandPayload.Hash(command with { X = 2 }));
        Assert.NotEqual(hash, CommandPayload.Hash(command with { BuildingTypeId = "manor" }));
        Assert.NotEqual(hash, CommandPayload.Hash(command with { RequestId = Guid.CreateVersion7() }));
    }

    /// <summary>The same data under another command type gives another hash, so a request id reused on another command is a conflict.</summary>
    [Fact]
    public void Hash_AnotherCommandType_Differs()
    {
        var other = new OtherTypeCommand(PipelineFixture.RequestId, "cottage", 1, 1);

        Assert.NotEqual(CommandPayload.Hash(PipelineFixture.PlaceCottage()), CommandPayload.Hash(other));
    }

    /// <summary><see cref="PlaceTestCommand"/>'s data declared in reverse order, under the same command type; it is only hashed.</summary>
    /// <param name="Y">The tile's row.</param>
    /// <param name="X">The tile's column.</param>
    /// <param name="BuildingTypeId">The type to place.</param>
    /// <param name="RequestId">The request id.</param>
    private sealed record ReorderedPlaceCommand(int Y, int X, string BuildingTypeId, Guid RequestId) : ICityCommand
    {
        /// <inheritdoc />
        public string CommandType => PlaceTestCommand.Type;

        /// <inheritdoc />
        public LedgerReason? CostReason => LedgerReason.CostPlace;

        /// <inheritdoc />
        public LedgerReason? RefundReason => null;

        /// <inheritdoc />
        public string? ValidateShape() => null;

        /// <inheritdoc />
        public MutationResult Apply(CityState city, GameContent content) => throw new NotSupportedException("Only hashed.");
    }

    /// <summary><see cref="PlaceTestCommand"/>'s data under another command type; it is only hashed.</summary>
    /// <param name="RequestId">The request id.</param>
    /// <param name="BuildingTypeId">The type to place.</param>
    /// <param name="X">The tile's column.</param>
    /// <param name="Y">The tile's row.</param>
    private sealed record OtherTypeCommand(Guid RequestId, string BuildingTypeId, int X, int Y) : ICityCommand
    {
        /// <inheritdoc />
        public string CommandType => "other-test";

        /// <inheritdoc />
        public LedgerReason? CostReason => LedgerReason.CostPlace;

        /// <inheritdoc />
        public LedgerReason? RefundReason => null;

        /// <inheritdoc />
        public string? ValidateShape() => null;

        /// <inheritdoc />
        public MutationResult Apply(CityState city, GameContent content) => throw new NotSupportedException("Only hashed.");
    }
}
