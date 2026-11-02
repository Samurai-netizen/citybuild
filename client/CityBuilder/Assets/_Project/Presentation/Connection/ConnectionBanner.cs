#nullable enable

using System;
using CityBuilder.Client.Presentation.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Presentation.Connection
{
    /// <summary>
    /// The connection banner and the blocking screen (M0-55): a thin component over a <see cref="ConnectionBannerModel"/>. It loads
    /// <c>UI/ConnectionBanner</c> from <c>Resources</c>, writes the model into the elements and shows the banner under the HUD or the full-screen
    /// blocker, whichever the model says, or neither. A press on either button only raises <see cref="ActionRequested"/>: what the action does (a
    /// poll, which includes the one new login) is <see cref="ConnectionFlow"/>'s. The blocker takes the pointer over the whole screen. It decides
    /// nothing; every sentence comes from the model. <c>GameRoot</c> creates it once connected.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ConnectionBanner : MonoBehaviour
    {
        /// <summary>The layout (UXML), loaded by name from <c>Assets/_Project/Resources</c>.</summary>
        private const string LayoutPath = "UI/ConnectionBanner";

        /// <summary>The panel settings the UI shares with the other screens.</summary>
        private const string PanelSettingsPath = "UI/PanelSettings";

        /// <summary>The padding in panel units before the safe area is added.</summary>
        private const float BasePadding = 8f;

        /// <summary>Drawn over every other screen, the welcome-back report (2.5) and the toasts (3) included: a game that does not match the server shows nothing true.</summary>
        private const float SortingOrder = 4f;

        /// <summary>The name of the root element in <c>ConnectionBanner.uxml</c>.</summary>
        public const string RootName = "connection-root";

        /// <summary>The name of the banner strip.</summary>
        public const string BannerName = "connection-banner";

        /// <summary>The name of the banner's message label.</summary>
        public const string MessageName = "connection-message";

        /// <summary>The name of the label with the age of the last sync.</summary>
        public const string SyncedName = "connection-synced";

        /// <summary>The name of the banner's action button.</summary>
        public const string ActionName = "connection-action";

        /// <summary>The name of the full-screen scrim of the blocking screen.</summary>
        public const string BlockerName = "connection-blocker";

        /// <summary>The name of the blocking screen's title label.</summary>
        public const string BlockerTitleName = "connection-blocker-title";

        /// <summary>The name of the blocking screen's message label.</summary>
        public const string BlockerMessageName = "connection-blocker-message";

        /// <summary>The name of the blocking screen's button.</summary>
        public const string BlockerActionName = "connection-blocker-action";

        /// <summary>The USS class of the banner while it is hidden.</summary>
        private const string BannerHiddenClass = "connection-banner--hidden";

        /// <summary>The USS class of the blocker while it is hidden.</summary>
        private const string BlockerHiddenClass = "connection-blocker--hidden";

        /// <summary>The UI document; null before <see cref="Create"/> made it.</summary>
        private UIDocument? _document;

        /// <summary>The elements the component writes to; null until <c>Start</c> found them.</summary>
        private Elements? _elements;

        /// <summary>What is shown; null before the first <see cref="Show"/>.</summary>
        private ConnectionBannerModel? _model;

        /// <summary>What the component shows now; null before the first <see cref="Show"/>.</summary>
        public ConnectionBannerModel? Model => _model;

        /// <summary>Raised when the player presses the action of the banner or of the blocking screen, and the action works.</summary>
        public event Action? ActionRequested;

        /// <summary>The root of the element tree; null when the layout was not loaded.</summary>
        public VisualElement? Root => _document == null ? null : _document.rootVisualElement;

        /// <summary>
        /// Makes the component: a child object of <paramref name="parent"/> with a <see cref="UIDocument"/> of the shared panel settings. The elements
        /// are found in <c>Start</c>, when the document's tree exists. Returns null, after logging one error that names the assets, when one is missing.
        /// </summary>
        /// <param name="parent">The object the component's object goes under.</param>
        public static ConnectionBanner? Create(Transform parent)
        {
            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsPath);
            var layout = Resources.Load<VisualTreeAsset>(LayoutPath);
            if (panelSettings == null || layout == null)
            {
                Debug.LogError(
                    "ConnectionBanner: cannot show the connection state, an asset is missing under Assets/_Project/Resources: " +
                    $"{PanelSettingsPath} {(panelSettings == null ? "missing" : "found")}, " +
                    $"{LayoutPath} {(layout == null ? "missing" : "found")}.");
                return null;
            }

            // A UIDocument builds its tree when it is enabled, so it gets its settings while the object is still inactive.
            var bannerObject = new GameObject("Connection Banner");
            bannerObject.transform.SetParent(parent, false);
            bannerObject.SetActive(false);
            var banner = bannerObject.AddComponent<ConnectionBanner>();
            banner._document = bannerObject.AddComponent<UIDocument>();
            banner._document.panelSettings = panelSettings;
            banner._document.visualTreeAsset = layout;
            banner._document.sortingOrder = SortingOrder;
            bannerObject.SetActive(true);
            return banner;
        }

        /// <summary>Shows <paramref name="model"/>: the banner, the blocking screen or neither. The same model again changes nothing.</summary>
        /// <param name="model">What to show.</param>
        /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
        public void Show(ConnectionBannerModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (model.Equals(_model))
            {
                return;
            }

            _model = model;
            Refresh();
        }

        /// <summary>The player pressed the action: what a finger on either button does, as a call. Does nothing when nothing is shown or the action is off.</summary>
        public void PressAction()
        {
            ConnectionBannerModel? model = _model;
            if (model != null && model.IsAnythingShown && model.ActionEnabled)
            {
                ActionRequested?.Invoke();
            }
        }

        /// <summary>
        /// Whether a screen position (pixels, origin bottom left) is over the banner or, while the blocking screen is up, anywhere on the screen:
        /// a press there is the UI's, not the ground's. False when nothing is shown, and before the UI exists.
        /// </summary>
        public bool IsOverUi(Vector2 screenPosition)
        {
            return _model != null && _model.IsAnythingShown && UiRoot.IsOver(Root, screenPosition);
        }

        /// <summary>Unity calls it before the first frame: the document's tree exists now, so the elements are found and the state is shown.</summary>
        private void Start()
        {
            VisualElement? root = Root;
            Elements? elements = root == null ? null : Elements.Find(root);
            if (root == null || elements == null)
            {
                Debug.LogError("ConnectionBanner: the layout lacks one of its elements (" + RootName + ", " + BannerName + ", ... see the name constants).");
                return;
            }

            _elements = elements;
            elements.Action.clicked += PressAction;
            elements.BlockerAction.clicked += PressAction;
            root.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                UiRoot.PadBySafeArea(root, BasePadding);
                UiRoot.PadBySafeArea(elements.Blocker, BasePadding);
            });
            Refresh();
        }

        /// <summary>Writes the model into the elements; does nothing before the elements exist (<c>Start</c> calls it again).</summary>
        private void Refresh()
        {
            Elements? e = _elements;
            ConnectionBannerModel? model = _model;
            if (e == null || model == null)
            {
                return;
            }

            e.Banner.EnableInClassList(BannerHiddenClass, !model.IsBannerShown);
            e.Blocker.EnableInClassList(BlockerHiddenClass, !model.IsBlocking);
            e.Message.text = model.Message;
            e.Synced.text = model.SyncedText;
            e.Action.text = model.IsBannerShown ? model.ActionText : e.Action.text;
            e.Action.SetEnabled(model.ActionEnabled);
            e.BlockerTitle.text = model.BlockingTitleText;
            e.BlockerMessage.text = model.BlockingMessageText;
            e.BlockerAction.text = model.IsBlocking ? model.ActionText : e.BlockerAction.text;
            e.BlockerAction.SetEnabled(model.ActionEnabled);
        }

        /// <summary>The elements of the layout the component writes to, found by name once.</summary>
        private sealed class Elements
        {
            /// <summary>The banner strip and the full-screen scrim of the blocking screen, which are shown and hidden.</summary>
            public VisualElement Banner = null!, Blocker = null!;

            /// <summary>The labels the component writes.</summary>
            public Label Message = null!, Synced = null!, BlockerTitle = null!, BlockerMessage = null!;

            /// <summary>The banner's button and the blocking screen's button.</summary>
            public Button Action = null!, BlockerAction = null!;

            /// <summary>Finds every element in <paramref name="root"/>; null when one is missing.</summary>
            public static Elements? Find(VisualElement root)
            {
                var found = new Elements
                {
                    Banner = root.Q<VisualElement>(BannerName),
                    Blocker = root.Q<VisualElement>(BlockerName),
                    Message = root.Q<Label>(MessageName),
                    Synced = root.Q<Label>(SyncedName),
                    BlockerTitle = root.Q<Label>(BlockerTitleName),
                    BlockerMessage = root.Q<Label>(BlockerMessageName),
                    Action = root.Q<Button>(ActionName),
                    BlockerAction = root.Q<Button>(BlockerActionName),
                };
                bool complete = found.Banner != null && found.Blocker != null && found.Message != null && found.Synced != null
                    && found.BlockerTitle != null && found.BlockerMessage != null && found.Action != null && found.BlockerAction != null;
                return complete ? found : null;
            }
        }
    }
}
