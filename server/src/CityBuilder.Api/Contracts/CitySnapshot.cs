namespace CityBuilder.Api.Contracts;

/// <summary>
/// The snapshot: a city as the server sees it right after a request, the body of <c>GET /api/v1/city</c> and of every successful command
/// (core-instructions/05-api-contract.md, snapshot). The client shows it and snaps its own display clock to it; it never sends any of it back.
/// </summary>
/// <param name="RequestId">The command's request id, JSON <c>requestId</c>; <see langword="null"/> for <c>GET /city</c>, which is no command.</param>
/// <param name="StateVersion">The city's stored version after the request, JSON <c>stateVersion</c>; it only rises when something changed.</param>
/// <param name="ServerTimeUtc">The server time of the request in whole seconds, JSON <c>serverTimeUtc</c>, for example <c>2026-10-03T10:30:05Z</c>.</param>
/// <param name="ContentVersion">The version of the content the city was settled with, JSON <c>contentVersion</c>; fetch <c>/content</c> again when it differs
/// from the client's.</param>
/// <param name="City">The city, JSON <c>city</c>.</param>
/// <param name="Settlement">What the settlement of this request did, JSON <c>settlement</c>.</param>
public sealed record CitySnapshot(
    Guid? RequestId,
    long StateVersion,
    string ServerTimeUtc,
    string ContentVersion,
    SnapshotCity City,
    SnapshotSettlement Settlement);
