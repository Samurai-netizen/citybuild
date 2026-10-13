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
        // Loaded by name from Assets/_Project/Resources/UI: this object is created from code, so there is no scene field to assign.
        private const string PanelSettingsPath = "UI/PanelSettings";
        private const string ConnectionStatusPath = "UI/ConnectionStatus";

        private void Awake()
        {
            CreateConnectionStatus();
        }

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
