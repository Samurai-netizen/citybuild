#nullable enable

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// Where a development build keeps its <see cref="StoredLogin"/> between runs. A seam so <see cref="AuthService"/> is tested without
    /// <c>PlayerPrefs</c>, and so a release build can be given a store that keeps nothing (<see cref="AccessTokenStores"/>).
    /// </summary>
    public interface IAccessTokenStore
    {
        /// <summary>The kept login, or null when there is none or it is incomplete.</summary>
        StoredLogin? Load();

        /// <summary>Keeps <paramref name="login"/>, replacing any earlier one.</summary>
        void Save(StoredLogin login);

        /// <summary>Forgets the kept login; does nothing when there is none.</summary>
        void Clear();
    }
}
