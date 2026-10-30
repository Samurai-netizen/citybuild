#nullable enable

using UnityEngine;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// Keeps the <see cref="StoredLogin"/> in <c>PlayerPrefs</c>, which are plain text on the device: fine for a development token of a
    /// development server, never for a release build (<see cref="AccessTokenStores"/> hands this store to development builds only).
    /// The two keys are named for what they hold so nobody mistakes them for game state.
    /// </summary>
    public sealed class PlayerPrefsAccessTokenStore : IAccessTokenStore
    {
        /// <summary>The prefix of the two keys the game uses; a test passes its own so it never touches the Editor's real login.</summary>
        public const string DefaultKeyPrefix = "CityBuilder.DevLogin.";

        /// <summary>The key of the bearer token.</summary>
        private readonly string _tokenKey;

        /// <summary>The key of the display name the token was issued for.</summary>
        private readonly string _displayNameKey;

        /// <summary>Creates the store.</summary>
        /// <param name="keyPrefix">The text both keys start with.</param>
        public PlayerPrefsAccessTokenStore(string keyPrefix = DefaultKeyPrefix)
        {
            _tokenKey = keyPrefix + "AccessToken";
            _displayNameKey = keyPrefix + "DisplayName";
        }

        /// <inheritdoc/>
        public StoredLogin? Load()
        {
            string token = PlayerPrefs.GetString(_tokenKey, string.Empty);
            string displayName = PlayerPrefs.GetString(_displayNameKey, string.Empty);
            if (token.Length == 0 || displayName.Length == 0)
            {
                return null;
            }

            return new StoredLogin(displayName, token);
        }

        /// <inheritdoc/>
        public void Save(StoredLogin login)
        {
            PlayerPrefs.SetString(_tokenKey, login.AccessToken);
            PlayerPrefs.SetString(_displayNameKey, login.DisplayName);
            PlayerPrefs.Save();
        }

        /// <inheritdoc/>
        public void Clear()
        {
            PlayerPrefs.DeleteKey(_tokenKey);
            PlayerPrefs.DeleteKey(_displayNameKey);
            PlayerPrefs.Save();
        }
    }
}
