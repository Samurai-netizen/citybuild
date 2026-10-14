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
        private const string BootstrapSceneName = "Bootstrap";
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string ConnectionStatusElementName = "connection-status";

        private readonly List<string> _errors = new List<string>();

        // The handler is added before the scene loads, so an error logged by Awake of the new GameRoot is caught too.
        [UnitySetUp]
        public IEnumerator LoadBootstrapScene()
        {
            _errors.Clear();
            Application.logMessageReceived += OnLogMessage;
            yield return LoadBootstrapSceneAndWaitOneFrame();
        }

        [TearDown]
        public void StopListening()
        {
            Application.logMessageReceived -= OnLogMessage;
        }

        [Test]
        public void BuildSettings_SceneZero_IsTheBootstrapScene()
        {
            Assert.That(SceneUtility.GetScenePathByBuildIndex(0), Is.EqualTo(BootstrapScenePath));
        }

        [Test]
        public void BootstrapScene_WhenLoaded_HasExactlyOneGameRoot()
        {
            Assert.That(FindGameRoots(), Has.Length.EqualTo(1));
        }

        [Test]
        public void BootstrapScene_WhenLoaded_LogsNoErrors()
        {
            Assert.That(_errors, Is.Empty);
        }

        [Test]
        public void GameRoot_WhenBootstrapSceneIsLoaded_LivesInThatScene()
        {
            GameRoot gameRoot = FindGameRoots()[0];

            Assert.That(gameRoot.gameObject.scene.name, Is.EqualTo(BootstrapSceneName));
        }

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

        [UnityTest]
        public IEnumerator BootstrapScene_WhenLoadedAgain_StillHasExactlyOneGameRoot()
        {
            yield return LoadBootstrapSceneAndWaitOneFrame();

            Assert.That(FindGameRoots(), Has.Length.EqualTo(1));
        }

        // One frame after the load: the old scene's objects are destroyed at the end of the frame, and a UIDocument builds its tree on enable.
        private static IEnumerator LoadBootstrapSceneAndWaitOneFrame()
        {
            yield return SceneManager.LoadSceneAsync(BootstrapSceneName, LoadSceneMode.Single);
            yield return null;
        }

        // Looks in every loaded scene, so a second root anywhere (also one that is inactive) is counted.
        private static GameRoot[] FindGameRoots()
        {
            return Object.FindObjectsByType<GameRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        // Only what the spec calls an error: warnings are not counted, because a graphics driver or a package can add one that the project does not own.
        private void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errors.Add($"{type}: {message}");
            }
        }
    }
}
