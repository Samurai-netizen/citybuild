#nullable enable

using System;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// What a development build keeps between runs so it need not log in again: the access token and the display name it was issued for.
    /// It holds no snapshot, no balance and no expiry; the server alone says when a token stops working (<c>05</c> Authentication).
    /// </summary>
    public sealed class StoredLogin
    {
        /// <summary>Creates the pair.</summary>
        /// <param name="displayName">The dev player the token belongs to.</param>
        /// <param name="accessToken">The bearer token the server issued for that player.</param>
        /// <exception cref="ArgumentException">Either text is null or empty.</exception>
        public StoredLogin(string displayName, string accessToken)
        {
            if (string.IsNullOrEmpty(displayName))
            {
                throw new ArgumentException("The display name must not be empty.", nameof(displayName));
            }

            if (string.IsNullOrEmpty(accessToken))
            {
                throw new ArgumentException("The access token must not be empty.", nameof(accessToken));
            }

            DisplayName = displayName;
            AccessToken = accessToken;
        }

        /// <summary>The dev player the token was issued for.</summary>
        public string DisplayName { get; }

        /// <summary>The bearer token. A secret: it never goes into a log or an error message.</summary>
        public string AccessToken { get; }
    }
}
