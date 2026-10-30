#nullable enable

using Newtonsoft.Json;

namespace CityBuilder.Client.Net
{
    /// <summary>Body of <c>POST /api/v1/auth/dev-login</c> (<c>05</c> Authentication): the name of the development player to log in as. The server creates the player and the starter city on a name's first use.</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class DevLoginRequestDto
    {
        /// <summary>Creates the DTO. Newtonsoft calls this constructor with the JSON members of the same names, so the object is never half filled.</summary>
        [JsonConstructor]
        public DevLoginRequestDto(string displayName)
        {
            DisplayName = displayName;
        }

        /// <summary>The dev name: 1 to 32 ASCII letters, digits, <c>_</c>, <c>-</c> or <c>.</c> (the server checks it).</summary>
        public string DisplayName { get; }
    }
}
