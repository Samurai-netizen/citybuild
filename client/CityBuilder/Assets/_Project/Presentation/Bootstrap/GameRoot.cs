using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Presentation.Bootstrap
{
    /// <summary>
    /// The composition root: the one place that builds the client's services by hand and hands them to the views
    /// (core-instructions/09, "Runtime structure"). <see cref="GameBootstrap"/> creates exactly one when the Bootstrap scene loads.
    /// Until the networking tasks (M0-37 on) there is nothing to compose, so it only shows that the client is not connected.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameRoot : MonoBehaviour
    {
        /// <summary>
        /// The UI's panel settings, loaded by name from <c>Assets/_Project/Resources/UI</c>: this object is created from code, so there is no
        /// scene field to assign.
        /// </summary>
        private const string PanelSettingsPath = "UI/PanelSettings";
        /// <summary>The connection status layout (UXML), loaded by name from the same folder.</summary>
        private const string ConnectionStatusPath = "UI/ConnectionStatus";

        /// <summary>Unity calls it once, right after the component is created: it shows the connection status.</summary>
        private void Awake()
        {
            CreateConnectionStatus();
        }

        /// <summary>
        /// Shows the connection status: a child object <c>UI</c> with a <see cref="UIDocument"/> of the panel settings and the layout. When an
        /// asset is missing it logs an error that names both and shows nothing.
        /// </summary>
        private void CreateConnectionStatus()
        {
            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsPath);
            var connectionStatus = Resources.Load<VisualTreeAsset>(ConnectionStatusPath);
            if (panelSettings == null || connectionStatus == null)
            {
                Debug.LogError(
                    "GameRoot: cannot show the connection status, an asset is missing under Assets/_Project/Resources: " +
                    $"{PanelSettingsPath} {(panelSettings == null ? "missing" : "found")}, " +
                    $"{ConnectionStatusPath} {(connectionStatus == null ? "missing" : "found")}.");
                return;
            }

            var uiObject = new GameObject("UI");
            uiObject.transform.SetParent(transform, false);

            // A UIDocument builds its tree when it is enabled, so it gets its settings while the object is still inactive.
            uiObject.SetActive(false);
            var document = uiObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = connectionStatus;
            uiObject.SetActive(true);
        }
    }
}
