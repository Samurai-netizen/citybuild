#nullable enable

namespace CityBuilder.Client.Net
{
    /// <summary>A store that keeps nothing: what a release build gets, so its token lives in memory only (<see cref="AccessTokenStores"/>).</summary>
    public sealed class NullAccessTokenStore : IAccessTokenStore
    {
        /// <inheritdoc/>
        public StoredLogin? Load()
        {
            return null;
        }

        /// <inheritdoc/>
        public void Save(StoredLogin login)
        {
            // Nothing is kept on purpose.
        }

        /// <inheritdoc/>
        public void Clear()
        {
            // Nothing is kept, so there is nothing to forget.
        }
    }
}
