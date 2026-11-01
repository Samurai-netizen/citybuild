#nullable enable

using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Presentation.Bootstrap
{
    /// <summary>
    /// The connection status on the screen (split from <see cref="GameRoot"/> in M0-48): a thin component that writes a <see cref="ConnectionStatusModel"/>
    /// into the status label of <c>UI/ConnectionStatus</c>, or swaps in <c>UI/FatalError</c> with its lines when the model is fatal. It decides nothing:
    /// the sentences come from the model. <see cref="GameRoot"/> creates it first of all the screens, with <see cref="Create"/>, so the start has a place to show its status.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ConnectionStatusView : MonoBehaviour
    {
        /// <summary>
        /// The UI's panel settings, loaded by name from <c>Assets/_Project/Resources/UI</c>: this object is created from code, so there is no
        /// scene field to assign.
        /// </summary>
        private const string PanelSettingsPath = "UI/PanelSettings";

        /// <summary>The connection status layout (UXML), loaded by name from the same folder.</summary>
        private const string ConnectionStatusPath = "UI/ConnectionStatus";

        /// <summary>The fatal error layout (UXML), loaded by name from the same folder.</summary>
        private const string FatalErrorPath = "UI/FatalError";

        /// <summary>The name of the status label in <c>ConnectionStatus.uxml</c>.</summary>
        public const string StatusLabelName = "connection-status";

        /// <summary>The name of the title label in <c>FatalError.uxml</c>.</summary>
        public const string FatalTitleName = "fatal-error-title";

        /// <summary>The name of the details label in <c>FatalError.uxml</c>.</summary>
        public const string FatalDetailsName = "fatal-error-details";

        /// <summary>The UI document of the status; null before <see cref="Create"/> made it.</summary>
        private UIDocument? _document;

        /// <summary>The fatal error layout, kept to swap in; null before <see cref="Create"/>.</summary>
        private VisualTreeAsset? _fatalErrorLayout;

        /// <summary>The root of the shown layout's element tree; null when the document has none.</summary>
        public VisualElement? Root => _document == null ? null : _document.rootVisualElement;

        /// <summary>
        /// Makes the view: a child object <c>UI</c> of <paramref name="parent"/> with a <see cref="UIDocument"/> of the panel settings and the status
        /// layout. Returns null, after logging one error that names the assets, when one is missing.
        /// </summary>
        /// <param name="parent">The object the view's object goes under.</param>
        public static ConnectionStatusView? Create(Transform parent)
        {
            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsPath);
            var connectionStatus = Resources.Load<VisualTreeAsset>(ConnectionStatusPath);
            var fatalErrorLayout = Resources.Load<VisualTreeAsset>(FatalErrorPath);
            if (panelSettings == null || connectionStatus == null || fatalErrorLayout == null)
            {
                Debug.LogError(
                    "ConnectionStatusView: cannot show the connection status, an asset is missing under Assets/_Project/Resources: " +
                    $"{PanelSettingsPath} {(panelSettings == null ? "missing" : "found")}, " +
                    $"{ConnectionStatusPath} {(connectionStatus == null ? "missing" : "found")}, " +
                    $"{FatalErrorPath} {(fatalErrorLayout == null ? "missing" : "found")}.");
                return null;
            }

            // A UIDocument builds its tree when it is enabled, so it gets its settings while the object is still inactive.
            var uiObject = new GameObject("UI");
            uiObject.transform.SetParent(parent, false);
            uiObject.SetActive(false);
            var view = uiObject.AddComponent<ConnectionStatusView>();
            view._document = uiObject.AddComponent<UIDocument>();
            view._document.panelSettings = panelSettings;
            view._document.visualTreeAsset = connectionStatus;
            view._fatalErrorLayout = fatalErrorLayout;
            uiObject.SetActive(true);
            return view;
        }

        /// <summary>Puts <paramref name="status"/> on the screen: the label's text, or the fatal error layout with its lines.</summary>
        /// <param name="status">What to say.</param>
        public void Show(ConnectionStatusModel status)
        {
            VisualElement? root = Root;
            if (_document == null || root == null)
            {
                return;
            }

            if (status.IsFatal)
            {
                // Setting the layout rebuilds the document's tree, so the labels are looked up after it.
                _document.visualTreeAsset = _fatalErrorLayout;
                VisualElement? fatalRoot = _document.rootVisualElement;
                if (fatalRoot == null)
                {
                    return;
                }

                SetText(fatalRoot, FatalTitleName, status.Headline);
                SetText(fatalRoot, FatalDetailsName, string.Join("\n", status.Details));
                return;
            }

            SetText(root, StatusLabelName, status.Headline);
        }

        /// <summary>Sets the text of the label called <paramref name="labelName"/> under <paramref name="root"/>; logs an error when the layout lacks it.</summary>
        private static void SetText(VisualElement root, string labelName, string text)
        {
            var label = root.Q<Label>(labelName);
            if (label == null)
            {
                Debug.LogError($"ConnectionStatusView: the layout has no label named '{labelName}'.");
                return;
            }

            label.text = text;
        }
    }
}
