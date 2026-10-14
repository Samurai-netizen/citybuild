using CityBuilder.Client.Core;
using NUnit.Framework;
using UnityEditor;

namespace CityBuilder.Client.Tests.EditMode
{
    public sealed class AppInfoTests
    {
        [Test]
        public void Version_InTheEditor_IsTheBundleVersionOfThePlayerSettings()
        {
            Assert.That(AppInfo.Version, Is.EqualTo(PlayerSettings.bundleVersion));
        }

        [Test]
        public void Version_Always_IsNotBlank()
        {
            Assert.That(AppInfo.Version, Is.Not.Null.And.Not.Empty);
            Assert.That(AppInfo.Version.Trim(), Is.EqualTo(AppInfo.Version), "A version with spaces around it would reach the server header as typed.");
        }
    }
}
