using System.IO;
using System.Linq;
using CityBuilder.Client.Net;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace CityBuilder.Client.Tests.EditMode.Build
{
    /// <summary>
    /// The settings a development build is made from (M0-82, <c>docs/BUILD.md</c>): the one scene, IL2CPP on Android, and the
    /// <c>link.xml</c> that keeps the DTOs from IL2CPP's stripping. <c>DevBuild</c> (the Editor assembly) builds from these; like
    /// <c>ArtImportTests</c>, this assembly reads the settings and needs no reference to it. The builds themselves are run with
    /// <c>scripts/dev/build-client.sh</c>.
    /// </summary>
    public sealed class BuildSettingsTests
    {
        /// <summary>The build settings enable the Bootstrap scene only, the one <c>GameBootstrap</c> starts the game from.</summary>
        [Test]
        public void BuildScenes_AreTheBootstrapSceneOnly()
        {
            string[] enabled = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();

            Assert.That(enabled, Is.EqualTo(new[] { "Assets/Scenes/Bootstrap.unity" }));
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
