#nullable enable

using CityBuilder.Client.Net;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>A token store that keeps the login in a field: what the auth tests use instead of <c>PlayerPrefs</c>, and how they see what was saved.</summary>
    public sealed class MemoryAccessTokenStore : IAccessTokenStore
    {
        /// <summary>The kept login, or null.</summary>
        public StoredLogin? Kept { get; set; }

        /// <inheritdoc/>
        public StoredLogin? Load()
        {
            return Kept;
        }

        /// <inheritdoc/>
        public void Save(StoredLogin login)
        {
            Kept = login;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            Kept = null;
        }
    }
}
