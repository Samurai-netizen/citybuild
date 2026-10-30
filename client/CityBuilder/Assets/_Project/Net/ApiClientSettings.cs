using UnityEngine;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// The config asset that says where the server is and how long to wait for it (<c>09</c> Net). It is a text asset in
    /// <c>Assets/_Project/Resources/Config/</c> that <c>GameRoot</c> loads by name, so another server needs a different asset, not
    /// different code. It holds no secret and no game number.
    /// </summary>
    [CreateAssetMenu(fileName = "ApiClientSettings", menuName = "CityBuilder/Api Client Settings")]
    public sealed class ApiClientSettings : ScriptableObject
    {
        /// <summary>The server's address without a path, for example <c>http://localhost:8080</c> (the dev server of <c>docs/getting-started.md</c>).</summary>
        [SerializeField]
        private string baseUrl = "http://localhost:8080";

        /// <summary>Seconds one request may take before it counts as lost (<c>CLIENT_TIMEOUT</c>); a mobile network needs more than a desktop.</summary>
        [SerializeField]
        private int requestTimeoutSeconds = 15;

        /// <summary>
        /// The dev player's name the client logs in as (<c>POST /auth/dev-login</c>, Development servers only, <c>05</c>): 1 to 32 ASCII letters,
        /// digits, <c>_</c>, <c>-</c> or <c>.</c>. Another name is another player with its own city. An asset saved before this field existed
        /// reads the default.
        /// </summary>
        [SerializeField]
        private string devDisplayName = "dev1";

        /// <summary>The server's address without a path.</summary>
        public string BaseUrl => baseUrl;

        /// <summary>Seconds one request may take before it counts as lost.</summary>
        public int RequestTimeoutSeconds => requestTimeoutSeconds;

        /// <summary>The dev player's name the client logs in as.</summary>
        public string DevDisplayName => devDisplayName;
    }
}
