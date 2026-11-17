#nullable enable

using System;
using CityBuilder.Client.Core;
using CityBuilder.Client.Presentation.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Presentation.Feedback
{
    /// <summary>
    /// The re-energize popup (M0-C-5, the owner's playtest): when the player sends a delivery or saves a route that needs a resting courier,
    /// it says <see cref="MessageText"/> on a card in the middle of a dark scrim, with Re-energize couriers and Not now. It loads
    /// <c>UI/ReenergizePrompt</c> from <c>Resources</c>. It is not a member of the menu group, so the delivery bar and the route editor under it
    /// stay as they were and the player presses Send or Save again once the couriers are back. It decides nothing and calls no server:
    /// <see cref="ReenergizePromptFlow"/> sends the command. <c>GameRoot</c> creates it with <see cref="Create"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReenergizePrompt : MonoBehaviour
    {
        /// <summary>The message, the owner's sentence.</summary>
        public const string MessageText = "You need to re-energize your couriers to do that";

        /// <summary>The name of the root element in <c>ReenergizePrompt.uxml</c>.</summary>
        public const string RootName = "reenergize-root";

        /// <summary>The name of the scrim that is shown and hidden and takes the pointer while shown.</summary>
        public const string ScrimName = "reenergize-scrim";

        /// <summary>The name of the title label.</summary>
        public const string TitleName = "reenergize-title";

        /// <summary>The name of the message label.</summary>
        public const string MessageName = "reenergize-message";

        /// <summary>The name of the Re-energize couriers button.</summary>
        public const string ReenergizeName = "reenergize-confirm";

        /// <summary>The name of the Not now button.</summary>
        public const string NotNowName = "reenergize-not-now";

        /// <summary>The USS class of the scrim while the popup is closed.</summary>
        public const string HiddenClass = "reenergize-scrim--hidden";

        /// <summary>The layout (UXML), loaded by name from <c>Assets/_Project/Resources</c>.</summary>
        private const string LayoutPath = "UI/ReenergizePrompt";

        /// <summary>The panel settings the UI shares with the other screens.</summary>
        private const string PanelSettingsPath = "UI/PanelSettings";

        /// <summary>The scrim's padding in panel units before the safe area is added.</summary>
        private const float BasePadding = 8f;

        /// <summary>Drawn over every other screen, the welcome-back report (2.5) included, and under the toasts (3), so a result still shows.</summary>
        private const float SortingOrder = 2.7f;

        /// <summary>The UI document of the popup; null before <see cref="Create"/> made it.</summary>
        private UIDocument? _document;

        /// <summary>The elements the popup writes to; null until <c>Start</c> found them.</summary>
        private Elements? _elements;

        /// <summary>Whether the popup is open.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Whether the Re-energize button waits for an answer: it is off then, so a second press sends nothing.</summary>
        public bool Busy { get; private set; }

        /// <summary>Raised when the player presses Re-energize couriers.</summary>
        public event Action? ReenergizeRequested;

        /// <summary>The root of the popup's element tree; null when the layout was not loaded.</summary>
        public VisualElement? Root => _document == null ? null : _document.rootVisualElement;

        /// <summary>
        /// Makes the popup: a child object of <paramref name="parent"/> with a <see cref="UIDocument"/> of the shared panel settings. Returns
        /// null, after logging one error that names the assets, when one is missing.
        /// </summary>
        /// <param name="parent">The object the popup's object goes under.</param>
        public static ReenergizePrompt? Create(Transform parent)
        {
            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsPath);
            var layout = Resources.Load<VisualTreeAsset>(LayoutPath);
            if (panelSettings == null || layout == null)
            {
                ClientLog.Error(
                    "ReenergizePrompt: cannot show the re-energize popup, an asset is missing under Assets/_Project/Resources: " +
                    $"{PanelSettingsPath} {(panelSettings == null ? "missing" : "found")}, " +
                    $"{LayoutPath} {(layout == null ? "missing" : "found")}.");
                return null;
            }

            // A UIDocument builds its tree when it is enabled, so it gets its settings while the object is still inactive.
            var promptObject = new GameObject("Reenergize Prompt");
            promptObject.transform.SetParent(parent, false);
            promptObject.SetActive(false);
            var prompt = promptObject.AddComponent<ReenergizePrompt>();
            prompt._document = promptObject.AddComponent<UIDocument>();
            prompt._document.panelSettings = panelSettings;
            prompt._document.visualTreeAsset = layout;
            prompt._document.sortingOrder = SortingOrder;
            promptObject.SetActive(true);
            return prompt;
        }

        /// <summary>Opens the popup; harmless while it is open.</summary>
        public void Show()
        {
            IsOpen = true;
            Refresh();
        }

        /// <summary>Closes the popup; harmless while it is closed.</summary>
        public void Dismiss()
        {
            IsOpen = false;
            Refresh();
        }

        /// <summary>Turns the Re-energize button off while its command is on its way, and on again after.</summary>
        /// <param name="busy">True while the command waits for the server.</param>
        public void SetBusy(bool busy)
        {
            Busy = busy;
            Refresh();
        }

        /// <summary>The player pressed Re-energize couriers: what a finger on the button does, as a call. Does nothing while closed or waiting.</summary>
        public void PressReenergize()
        {
            if (IsOpen && !Busy)
            {
                ReenergizeRequested?.Invoke();
            }
        }

        /// <summary>
        /// Whether a screen position (pixels, origin bottom left) is over the popup: the whole screen while it is open, because the scrim takes
        /// the pointer; never while it is closed. False before the UI exists.
        /// </summary>
        public bool IsOverUi(Vector2 screenPosition)
        {
            return IsOpen && UiRoot.IsOver(Root, screenPosition);
        }

        /// <summary>Unity calls it before the first frame: the document's tree exists now, so the elements are found and the state is shown.</summary>
        private void Start()
        {
            VisualElement? root = Root;
            Elements? elements = root == null ? null : Elements.Find(root);
            if (root == null || elements == null)
            {
                ClientLog.Error("ReenergizePrompt: the layout lacks one of its elements (" + RootName + ", " + ScrimName + ", ... see the name constants).");
                return;
            }

            _elements = elements;
            elements.Message.text = MessageText;
            elements.Reenergize.clicked += PressReenergize;
            elements.NotNow.clicked += Dismiss;
            elements.Scrim.RegisterCallback<GeometryChangedEvent>(_ => UiRoot.PadBySafeArea(elements.Scrim, BasePadding));
            Refresh();
        }

        /// <summary>Shows or hides the scrim and turns the button on or off; does nothing before the elements exist (<c>Start</c> calls it again).</summary>
        private void Refresh()
        {
            if (_elements == null)
            {
                return;
            }

            _elements.Scrim.EnableInClassList(HiddenClass, !IsOpen);
            _elements.Reenergize.SetEnabled(!Busy);
        }

        /// <summary>The elements of the layout the popup writes to, found by name once.</summary>
        private sealed class Elements
        {
            /// <summary>The scrim that is shown and hidden.</summary>
            public VisualElement Scrim = null!;

            /// <summary>The message label.</summary>
            public Label Message = null!;

            /// <summary>The two buttons.</summary>
            public Button Reenergize = null!, NotNow = null!;

            /// <summary>Finds every element in <paramref name="root"/>; null when one is missing.</summary>
            public static Elements? Find(VisualElement root)
            {
                var found = new Elements
                {
                    Scrim = root.Q<VisualElement>(ScrimName),
                    Message = root.Q<Label>(MessageName),
                    Reenergize = root.Q<Button>(ReenergizeName),
                    NotNow = root.Q<Button>(NotNowName),
                };
                return found.Scrim != null && found.Message != null && found.Reenergize != null && found.NotNow != null ? found : null;
            }
        }
    }
}
