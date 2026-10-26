using System.Collections;
using System.Collections.Generic;
using CityBuilder.Client.Presentation.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.PlayMode
{
    /// <summary>
    /// Loads the real <c>Assets/Scenes/Bootstrap.unity</c> (the empty scene the owner made) in Play Mode and checks what
    /// <see cref="GameBootstrap"/> and <see cref="GameRoot"/> build in it.
    /// </summary>
    public sealed class BootstrapSceneTests
    {
        /// <summary>The name of the scene under test.</summary>
        private const string BootstrapSceneName = "Bootstrap";
        /// <summary>The scene's path in the project; the build settings must list it first.</summary>
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        /// <summary>The name of the label in the connection status layout.</summary>
        private const string ConnectionStatusElementName = "connection-status";

        /// <summary>The errors logged since the test's setup, each with its log type.</summary>
        private readonly List<string> _errors = new List<string>();

        /// <summary>
        /// Before each test: listens for errors, then loads the Bootstrap scene. The handler is added before the scene loads, so an error logged by
        /// <c>Awake</c> of the new <see cref="GameRoot"/> is caught too.
        /// </summary>
        [UnitySetUp]
        public IEnumerator LoadBootstrapScene()
        {
            _errors.Clear();
            Application.logMessageReceived += OnLogMessage;
            yield return LoadBootstrapSceneAndWaitOneFrame();
        }

        /// <summary>After each test: stops listening for log messages.</summary>
        [TearDown]
        public void StopListening()
        {
            Application.logMessageReceived -= OnLogMessage;
        }

        /// <summary>The first scene of the build settings is the Bootstrap scene.</summary>
        [Test]
        public void BuildSettings_SceneZero_IsTheBootstrapScene()
        {
            Assert.That(SceneUtility.GetScenePathByBuildIndex(0), Is.EqualTo(BootstrapScenePath));
        }

        /// <summary>The loaded scene has exactly one <see cref="GameRoot"/>.</summary>
        [Test]
        public void BootstrapScene_WhenLoaded_HasExactlyOneGameRoot()
        {
            Assert.That(FindGameRoots(), Has.Length.EqualTo(1));
        }

        /// <summary>Loading the scene logs no errors, exceptions or failed asserts.</summary>
        [Test]
        public void BootstrapScene_WhenLoaded_LogsNoErrors()
        {
            Assert.That(_errors, Is.Empty);
        }

        /// <summary>The <see cref="GameRoot"/> lives in the Bootstrap scene.</summary>
        [Test]
        public void GameRoot_WhenBootstrapSceneIsLoaded_LivesInThatScene()
        {
            GameRoot gameRoot = FindGameRoots()[0];

            Assert.That(gameRoot.gameObject.scene.name, Is.EqualTo(BootstrapSceneName));
        }

        /// <summary>The connection status label reads <c>Not connected</c>.</summary>
        [Test]
        public void ConnectionStatus_WhenBootstrapSceneIsLoaded_ReadsNotConnected()
        {
            var document = FindGameRoots()[0].GetComponentInChildren<UIDocument>();
            Assert.That(document, Is.Not.Null, "GameRoot has no UIDocument.");
            Assert.That(document.rootVisualElement, Is.Not.Null, "The UIDocument has not built its tree.");

            var label = document.rootVisualElement.Q<Label>(ConnectionStatusElementName);

            Assert.That(label, Is.Not.Null, $"No label named '{ConnectionStatusElementName}' in the UI tree.");
            Assert.That(label.text, Is.EqualTo("Not connected"));
        }

        /// <summary>Loading the scene a second time still leaves exactly one <see cref="GameRoot"/>.</summary>
        [UnityTest]
        public IEnumerator BootstrapScene_WhenLoadedAgain_StillHasExactlyOneGameRoot()
        {
            yield return LoadBootstrapSceneAndWaitOneFrame();

            Assert.That(FindGameRoots(), Has.Length.EqualTo(1));
        }

        /// <summary>
        /// Loads the Bootstrap scene and waits one frame after the load: the old scene's objects are destroyed at the end of the frame, and a
        /// UIDocument builds its tree on enable.
        /// </summary>
        private static IEnumerator LoadBootstrapSceneAndWaitOneFrame()
        {
            yield return SceneManager.LoadSceneAsync(BootstrapSceneName, LoadSceneMode.Single);
            yield return null;
        }

        /// <summary>Every <see cref="GameRoot"/> in every loaded scene, inactive ones included, so a second root anywhere is counted.</summary>
        private static GameRoot[] FindGameRoots()
        {
            return Object.FindObjectsByType<GameRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        /// <summary>
        /// Records errors, exceptions and failed asserts: what the spec calls an error. Warnings are not counted, because a graphics driver or a
        /// package can add one that the project does not own.
        /// </summary>
        private void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errors.Add($"{type}: {message}");
            }
        }
    }
}
