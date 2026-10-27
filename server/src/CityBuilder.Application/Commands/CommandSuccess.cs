using CityBuilder.Application.Cities;

namespace CityBuilder.Application.Commands;

/// <summary>
/// A command that <see cref="CommandPipeline"/> has just applied, handed to the caller's response writer inside the transaction: everything the
/// snapshot response needs (<c>core-instructions/05-api-contract.md</c>, snapshot). The writer only reads it.
/// </summary>
/// <param name="RequestId">The command's request id.</param>
/// <param name="City">The city after the mutation and the closing zero-length advance, as it is being saved.</param>
/// <param name="Settlement">The settlement that ran before the mutation (with the closing advance).</param>
/// <param name="StateVersion">The city's <c>state_version</c> once saved: the loaded one plus 1.</param>
/// <param name="ServerTime">The server time the command ran at (<c>serverTimeUtc</c>).</param>
public sealed record CommandSuccess(Guid RequestId, CityAggregate City, Settlement Settlement, long StateVersion, DateTimeOffset ServerTime);
