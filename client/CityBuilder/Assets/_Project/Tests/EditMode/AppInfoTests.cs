using CityBuilder.Client.Core;
using NUnit.Framework;
using UnityEditor;

namespace CityBuilder.Client.Tests.EditMode
{
    /// <summary><see cref="AppInfo"/> in the Editor: the version is the Player settings' bundle version, and it is never blank or padded.</summary>
    public sealed class AppInfoTests
    {
        /// <summary>In the Editor the version is the bundle version of the Player settings.</summary>
        [Test]
        public void Version_InTheEditor_IsTheBundleVersionOfThePlayerSettings()
        {
            Assert.That(AppInfo.Version, Is.EqualTo(PlayerSettings.bundleVersion));
        }

        /// <summary>The version is never null or empty, and has no spaces around it.</summary>
        [Test]
        public void Version_Always_IsNotBlank()
        {
            Assert.That(AppInfo.Version, Is.Not.Null.And.Not.Empty);
            Assert.That(AppInfo.Version.Trim(), Is.EqualTo(AppInfo.Version), "A version with spaces around it would reach the server header as typed.");
        }
    }
}
