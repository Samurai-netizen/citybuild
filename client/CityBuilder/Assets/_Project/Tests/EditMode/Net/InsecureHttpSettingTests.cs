using NUnit.Framework;
using UnityEditor;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>
    /// The Player setting "Allow downloads over HTTP" (<c>09</c> Project settings, <c>07</c>): plain http works in the Editor and in
    /// development builds only. It is the engine's own guard under <c>ApiClient</c>'s check (M0-60), so a release build refuses an http
    /// address twice: at the client's construction and in <c>UnityWebRequest</c>.
    /// </summary>
    public sealed class InsecureHttpSettingTests
    {
        /// <summary>The project allows http in development builds, never in a release build and not "always".</summary>
        [Test]
        public void InsecureHttp_IsAllowedInDevelopmentBuildsOnly()
        {
            Assert.That(PlayerSettings.insecureHttpOption, Is.EqualTo(InsecureHttpOption.DevelopmentOnly));
        }
    }
}
