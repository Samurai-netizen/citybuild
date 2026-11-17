using System.IO;
using CityBuilder.Client.Editor;
using CityBuilder.Client.Net;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace CityBuilder.Client.Tests.EditMode.Build
{
    /// <summary>
    /// The development build's settings (M0-82, <c>docs/BUILD.md</c>): the one scene, a development player (which allows plain http),
    /// IL2CPP on Android, and the <c>link.xml</c> that keeps the DTOs from IL2CPP's stripping.
    /// </summary>
    public sealed class DevBuildTests
    {
        /// <summary>The build has the Bootstrap scene only, the one <c>GameBootstrap</c> starts the game from.</summary>
        [Test]
        public void Options_HaveTheBootstrapSceneOnly()
        {
            BuildPlayerOptions options = DevBuild.Options(BuildTarget.StandaloneOSX, "Builds/macOS/CityBuilder.app");

            Assert.That(options.scenes, Is.EqualTo(new[] { "Assets/Scenes/Bootstrap.unity" }));
        }

        /// <summary>A development player: <c>Debug.isDebugBuild</c> is true in it, so the client allows http and keeps the dev token.</summary>
        [Test]
        public void Options_MakeADevelopmentPlayer()
        {
            BuildPlayerOptions options = DevBuild.Options(BuildTarget.Android, "Builds/Android/CityBuilder.apk");

            Assert.That(options.options.HasFlag(BuildOptions.Development), Is.True);
            Assert.That(options.target, Is.EqualTo(BuildTarget.Android));
            Assert.That(options.targetGroup, Is.EqualTo(BuildTargetGroup.Android));
            Assert.That(options.locationPathName, Is.EqualTo("Builds/Android/CityBuilder.apk"));
        }

        /// <summary>Each supported target has its output path under <c>Builds/</c>, which git ignores.</summary>
        /// <param name="target">The target.</param>
        /// <param name="expected">Its output path.</param>
        [TestCase(BuildTarget.StandaloneOSX, "Builds/macOS/CityBuilder.app")]
        [TestCase(BuildTarget.StandaloneWindows64, "Builds/Windows/CityBuilder.exe")]
        [TestCase(BuildTarget.Android, "Builds/Android/CityBuilder.apk")]
        [TestCase(BuildTarget.iOS, "Builds/iOS")]
        public void DefaultOutput_IsUnderBuilds(BuildTarget target, string expected)
        {
            Assert.That(DevBuild.DefaultOutput(target), Is.EqualTo(expected));
        }

        /// <summary>Device builds use IL2CPP (<c>09</c> Project settings): Android is set so.</summary>
        [Test]
        public void Android_UsesIl2Cpp()
        {
            Assert.That(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android), Is.EqualTo(ScriptingImplementation.IL2CPP));
        }

        /// <summary>The <c>link.xml</c> next to the Net code preserves the whole assembly the DTOs live in.</summary>
        [Test]
        public void LinkXml_PreservesTheDtoAssembly()
        {
            string assembly = typeof(ApiClient).Assembly.GetName().Name;
            string linkXml = File.ReadAllText("Assets/_Project/Net/link.xml");

            Assert.That(linkXml, Does.Contain($"<assembly fullname=\"{assembly}\" preserve=\"all\"/>"));
            Assert.That(typeof(AssignWorkersRequestDto).Assembly.GetName().Name, Is.EqualTo(assembly));
        }
    }
}
