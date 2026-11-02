#nullable enable

using System;
using CityBuilder.Client.Presentation.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Presentation.Build
{
    /// <summary>
    /// The placement flow's bar on the screen (M0-46): a thin component over a <see cref="PlacementFlowModel"/>. It loads <c>UI/PlacementBar</c>
    /// from <c>Resources</c>, writes the model's prompt, tile warning and error into the labels by name, enables <b>Place</b> only while the
    /// model can confirm and <b>Cancel</b> while it can cancel, and shows the panel while a card is picked. A press on a button only raises
    /// <see cref="PlaceRequested"/> or <see cref="CancelRequested"/>: what happens next is the flow's, and nothing here decides or calls the server.
    /// <c>GameRoot</c> creates it once the start has connected, with <see cref="Create"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlacementBar : MonoBehaviour
    {
        /// <summary>The bar layout (UXML), loaded by name from <c>Assets/_Project/Resources</c>.</summary>
        private const string LayoutPath = "UI/PlacementBar";

        /// <summary>The panel settings the UI shares with the other screens.</summary>
        private const string PanelSettingsPath = "UI/PanelSettings";

        /// <summary>The root's padding in panel units before the safe area is added.</summary>
        private const float BasePadding = 8f;

        /// <summary>Drawn above the build menu, which uses sort order 1.</summary>
        private const float SortingOrder = 2f;

        /// <summary>The name of the root element in <c>PlacementBar.uxml</c>.</summary>
        public const string RootName = "placement-root";

        /// <summary>The name of the panel that is shown and hidden.</summary>
        public const string PanelName = "placement-panel";

        /// <summary>The name of the prompt label.</summary>
        public const string PromptName = "placement-prompt";

        /// <summary>The name of the tile warning label.</summary>
        public const string HintName = "placement-hint";

        /// <summary>The name of the error label.</summary>
        public const string ErrorName = "placement-error";

        /// <summary>The name of the Place button.</summary>
        public const string PlaceName = "placement-place";

        /// <summary>The name of the Cancel button.</summary>
        public const string CancelName = "placement-cancel";

        /// <summary>The USS class of the panel while no card is picked.</summary>
        private const string HiddenClass = "placement-panel--hidden";

        /// <summary>The model the screen shows; null before <see cref="Create"/>.</summary>
        private PlacementFlowModel? _model;

        /// <summary>The UI document of the bar; null before <see cref="Create"/> made it.</summary>
        private UIDocument? _document;

        /// <summary>The elements the bar writes to; null until <c>Start</c> found them.</summary>
        private VisualElement? _panel;

        /// <summary>The prompt label.</summary>
        private Label? _prompt;

        /// <summary>The tile warning label.</summary>
        private Label? _hint;

        /// <summary>The error label.</summary>
        private Label? _error;

        /// <summary>The Place button.</summary>
        private Button? _place;

        /// <summary>The Cancel button.</summary>
        private Button? _cancel;

        /// <summary>Raised when the player presses Place (only while the model can confirm).</summary>
        public event Action? PlaceRequested;

        /// <summary>Raised when the player presses Cancel (only while the model can cancel).</summary>
        public event Action? CancelRequested;

        /// <summary>The root of the bar's element tree; null when the layout was not loaded.</summary>
        public VisualElement? Root => _document == null ? null : _document.rootVisualElement;

        /// <summary>Whether the panel is shown: a card is picked or a command is on its way.</summary>
        public bool IsShown => _panel != null && !_panel.ClassListContains(HiddenClass);

        /// <summary>
        /// Makes the bar: a child object of <paramref name="parent"/> with a <see cref="UIDocument"/> of the shared panel settings. The
        /// elements are found in <c>Start</c>, before the first frame, when the document's tree exists. Returns null, after logging one
        /// error that names the assets, when one is missing.
        /// </summary>
        /// <param name="parent">The object the bar's object goes under.</param>
        /// <param name="model">The flow's state.</param>
        public static PlacementBar? Create(Transform parent, PlacementFlowModel model)
        {
            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsPath);
            var layout = Resources.Load<VisualTreeAsset>(LayoutPath);
            if (panelSettings == null || layout == null)
            {
                Debug.LogError(
                    "PlacementBar: cannot show the placement bar, an asset is missing under Assets/_Project/Resources: " +
                    $"{PanelSettingsPath} {(panelSettings == null ? "missing" : "found")}, " +
                    $"{LayoutPath} {(layout == null ? "missing" : "found")}.");
                return null;
            }

            // A UIDocument builds its tree when it is enabled, so it gets its settings while the object is still inactive.
            var barObject = new GameObject("Placement Bar");
            barObject.transform.SetParent(parent, false);
            barObject.SetActive(false);
            var bar = barObject.AddComponent<PlacementBar>();
            bar._document = barObject.AddComponent<UIDocument>();
            bar._document.panelSettings = panelSettings;
            bar._document.visualTreeAsset = layout;
            bar._document.sortingOrder = SortingOrder;
            bar._model = model;
            barObject.SetActive(true);
            return bar;
        }

        /// <summary>The player pressed Place: what a finger on the button does, as a call. Does nothing unless the model can confirm.</summary>
        public void PressPlace()
        {
            if (_model != null && _model.CanConfirm)
            {
                PlaceRequested?.Invoke();
            }
        }

        /// <summary>The player pressed Cancel: what a finger on the button does, as a call. Does nothing unless the model can cancel.</summary>
        public void PressCancel()
        {
            if (_model != null && _model.CanCancel)
            {
                CancelRequested?.Invoke();
            }
        }

        /// <summary>
        /// Whether a screen position (pixels, origin bottom left) is over the bar's panel. The ground ignores a press there. False before the UI
        /// exists and while the panel is hidden.
        /// </summary>
        public bool IsOverUi(Vector2 screenPosition)
        {
            return UiRoot.IsOver(Root, screenPosition);
        }

        /// <summary>Unity calls it before the first frame: the document's tree exists now, so the elements are found and the model is shown.</summary>
        private void Start()
        {
            VisualElement? root = Root;
            _panel = root?.Q<VisualElement>(PanelName);
            _prompt = root?.Q<Label>(PromptName);
            _hint = root?.Q<Label>(HintName);
            _error = root?.Q<Label>(ErrorName);
            _place = root?.Q<Button>(PlaceName);
            _cancel = root?.Q<Button>(CancelName);
            if (_model == null || root == null || _panel == null || _prompt == null || _hint == null || _error == null || _place == null || _cancel == null)
            {
                Debug.LogError(
                    $"PlacementBar: the layout lacks '{PanelName}', '{PromptName}', '{HintName}', '{ErrorName}', '{PlaceName}' or '{CancelName}'.");
                return;
            }

            _place.clicked += PressPlace;
            _cancel.clicked += PressCancel;
            root.RegisterCallback<GeometryChangedEvent>(_ => UiRoot.PadBySafeArea(root, BasePadding));
            _model.Changed += Refresh;
            Refresh();
        }

        /// <summary>Unity calls it when the object is destroyed: the model no longer reports to a screen that is gone.</summary>
        private void OnDestroy()
        {
            if (_model != null)
            {
                _model.Changed -= Refresh;
            }
        }

        /// <summary>Writes the model's words into the labels, enables the buttons by what the model allows, and shows the panel while a card is picked.</summary>
        private void Refresh()
        {
            if (_model == null || _panel == null || _prompt == null || _hint == null || _error == null || _place == null || _cancel == null)
            {
                return;
            }

            _panel.EnableInClassList(HiddenClass, _model.Phase == PlacementPhase.Idle);
            _prompt.text = _model.PromptText;
            SetLine(_hint, _model.HintText);
            SetLine(_error, _model.ErrorText);
            _place.SetEnabled(_model.CanConfirm);
            _cancel.SetEnabled(_model.CanCancel);
        }

        /// <summary>Sets a label's text and hides the label when the text is empty, so an empty line takes no room.</summary>
        private static void SetLine(Label label, string text)
        {
            label.text = text;
            label.style.display = text.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
