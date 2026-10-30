#nullable enable

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// Chooses where the token lives. The one rule (<c>M0-38</c>): only a development build writes it to <c>PlayerPrefs</c>; every other
    /// build keeps it in memory. Kept as a function of a flag so a test can check both answers on one machine.
    /// </summary>
    public static class AccessTokenStores
    {
        /// <summary>The store for a build.</summary>
        /// <param name="isDevelopmentBuild">Whether this is the Editor or a development player (<c>Debug.isDebugBuild</c>).</param>
        public static IAccessTokenStore For(bool isDevelopmentBuild)
        {
            return isDevelopmentBuild ? new PlayerPrefsAccessTokenStore() : new NullAccessTokenStore();
        }
    }
}
