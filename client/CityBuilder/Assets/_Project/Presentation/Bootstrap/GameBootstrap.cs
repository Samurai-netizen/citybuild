using UnityEngine;
using UnityEngine.SceneManagement;

namespace CityBuilder.Client.Presentation.Bootstrap
{
    /// <summary>
    /// Creates the one <see cref="GameRoot"/> when the Bootstrap scene loads, so the scene file can stay empty and never needs
    /// hand-editing (core-instructions/09). A <see cref="GameRoot"/> that is already in the scene is kept: the scene never gets two.
    /// </summary>
    internal static class GameBootstrap
    {
        /// <summary>The name of the scene that gets a <see cref="GameRoot"/> (<c>Assets/Scenes/Bootstrap.unity</c>).</summary>
        internal const string BootstrapSceneName = "Bootstrap";

        // Runs once per start of the app or of Play Mode, before the first scene loads, so the handler also sees that first scene.
        // Unsubscribing first keeps a single handler when Domain Reload is off and Play Mode starts again in the same domain.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != BootstrapSceneName || ContainsGameRoot(scene))
            {
                return;
            }

            // The new object goes into the loaded scene, not the active one: they differ when Bootstrap is loaded additively.
            var gameRootObject = new GameObject(nameof(GameRoot));
            SceneManager.MoveGameObjectToScene(gameRootObject, scene);
            gameRootObject.AddComponent<GameRoot>();
        }

        private static bool ContainsGameRoot(Scene scene)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.GetComponentInChildren<GameRoot>(true) != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
