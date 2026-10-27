using System.Text.Json.Serialization;

namespace CityBuilder.Api.Contracts;

/// <summary>
/// The body of <c>POST /api/v1/auth/dev-login</c> (core-instructions/05-api-contract.md): the display name only. It has no player id: the
/// server finds or creates the player by name. A body with any other property is refused (400) instead of having it ignored, like every
/// body of the contract.
/// </summary>
/// <param name="DisplayName">The display name, JSON <c>displayName</c>; 1 to 32 ASCII letters, digits, <c>_</c>, <c>-</c> or <c>.</c>.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DevLoginRequest(string? DisplayName);
