using UnityEngine;

namespace CityBuilder.Client.Core
{
    /// <summary>
    /// Facts about this build of the client. The network layer sends <see cref="Version"/> to the server (M0-58); nothing here
    /// is a game number or changes while the app runs.
    /// </summary>
    public static class AppInfo
    {
        /// <summary>
        /// The client build version, for example <c>0.1.0</c>. It is Unity's <see cref="Application.version"/>, which is the
        /// "Version" field of Project Settings → Player (<c>bundleVersion</c>), so the project setting is the one place that holds it.
        /// </summary>
        public static string Version => Application.version;
    }
}
