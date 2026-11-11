#nullable enable

using System;
using CityBuilder.Client.Core;
using CityBuilder.Client.Presentation.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Presentation.Roads
{
    /// <summary>
    /// The road bar at the bottom of the screen (CR-0008): a thin view over <see cref="RoadFlowModel"/>. It loads <c>UI/RoadBar</c> from
    /// <c>Resources</c>, shows the prompt, cost/speed detail and refusal hint, and raises <see cref="PlaceRequested"/>,
    /// <see cref="UpgradeRequested"/>, <see cref="RemoveRequested"/> or <see cref="CancelRequested"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoadBar : MonoBehaviour
    {
        /// <summary>The road bar layout (UXML), loaded from <c>Assets/_Project/Resources</c>.</summary>
        private const string LayoutPath = "UI/RoadBar";

        /// <summary>The shared panel settings.</summary>
        private const string PanelSettingsPath = "UI/PanelSettings";

        /// <summary>The root's padding in panel units before the safe area is added.</summary>
        private const float BasePadding = 8f;

        /// <summary>Drawn at the same height as the placement and trip bars (1.2), above the HUD and dock and under toasts.</summary>
        private const float SortingOrder = 1.2f;

        /// <summary>The name of the root element in <c>RoadBar.uxml</c>.</summary>
        public const string RootName = "road-root";

        /// <summary>The name of the bar element.</summary>
        public const string BarName = "road-bar";

        /// <summary>The name of the prompt label.</summary>
        public const string PromptName = "road-prompt";

        /// <summary>The name of the detail label.</summary>
        public const string DetailName = "road-detail";

        /// <summary>The name of the refusal hint label.</summary>
        public const string HintName = "road-hint";

        /// <summary>The name of the Build road button.</summary>
        public const string PlaceButtonName = "road-place";

        /// <summary>The name of the Upgrade road button.</summary>
        public const string UpgradeButtonName = "road-upgrade";

        /// <summary>The name of the Remove road button.</summary>
        public const string RemoveButtonName = "road-remove";

        /// <summary>The name of the Cancel button.</summary>
        public const string CancelButtonName = "road-cancel";

        /// <summary>The USS class that hides the bar while <see cref="RoadPhase.Idle"/>.</summary>
        private const string HiddenClass = "road-bar--hidden";

        /// <summary>The USS class that hides the hint label while empty.</summary>
        private const string HintHiddenClass = "road-hint--hidden";

        /// <summary>The UI document of the bar.</summary>
        private UIDocument? _document;

        /// <summary>The model the bar follows.</summary>
        private RoadFlowModel? _model;

        /// <summary>The elements the bar writes to; null until <c>Start</c> finds them.</summary>
        private Elements? _elements;

        /// <summary>Raised when the player presses Build road.</summary>
        public event Action? PlaceRequested;

        /// <summary>Raised when the player presses Upgrade road.</summary>
        public event Action? UpgradeRequested;

        /// <summary>Raised when the player presses Remove road.</summary>
        public event Action? RemoveRequested;

        /// <summary>Raised when the player presses Cancel.</summary>
        public event Action? CancelRequested;

        /// <summary>The root of the bar's element tree; null when the layout was not loaded.</summary>
        public VisualElement? Root => _document == null ? null : _document.rootVisualElement;

        /// <summary>Creates the road bar under <paramref name="parent"/>.</summary>
        public static RoadBar? Create(Transform parent, RoadFlowModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsPath);
            var layout = Resources.Load<VisualTreeAsset>(LayoutPath);
            if (panelSettings == null || layout == null)
            {
                ClientLog.Error(
                    "RoadBar: cannot show the road bar, an asset is missing under Assets/_Project/Resources: " +
                    $"{PanelSettingsPath} {(panelSettings == null ? "missing" : "found")}, " +
                    $"{LayoutPath} {(layout == null ? "missing" : "found")}.");
                return null;
            }

            var barObject = new GameObject("Road Bar");
            barObject.transform.SetParent(parent, false);
            barObject.SetActive(false);
            var bar = barObject.AddComponent<RoadBar>();
            bar._model = model;
            bar._document = barObject.AddComponent<UIDocument>();
            bar._document.panelSettings = panelSettings;
            bar._document.visualTreeAsset = layout;
            bar._document.sortingOrder = SortingOrder;
            barObject.SetActive(true);
            return bar;
        }

        /// <summary>Refreshes the bar from its model.</summary>
        public void Refresh()
        {
            Elements? e = _elements;
            RoadFlowModel? model = _model;
            if (e == null || model == null)
            {
                return;
            }

            bool hidden = model.Phase == RoadPhase.Idle;
            e.Bar.EnableInClassList(HiddenClass, hidden);
            if (hidden)
            {
                return;
            }

            e.Prompt.text = model.PromptText;
            e.Detail.text = model.DetailText;
            e.Detail.style.display = model.DetailText.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            e.Hint.text = model.HintText;
            e.Hint.EnableInClassList(HintHiddenClass, model.HintText.Length == 0);

            e.Place.style.display = model.ShowsPlace ? DisplayStyle.Flex : DisplayStyle.None;
            e.Place.SetEnabled(model.CanPlace);
            e.Upgrade.style.display = model.ShowsEditExisting ? DisplayStyle.Flex : DisplayStyle.None;
            e.Upgrade.SetEnabled(model.CanUpgrade);
            e.Remove.style.display = model.ShowsEditExisting ? DisplayStyle.Flex : DisplayStyle.None;
            e.Remove.SetEnabled(model.CanRemove);
            e.Cancel.SetEnabled(model.Phase != RoadPhase.Sending);
        }

        /// <summary>Simulates pressing Build road.</summary>
        public void PressPlace() => PlaceRequested?.Invoke();

        /// <summary>Simulates pressing Upgrade road.</summary>
        public void PressUpgrade() => UpgradeRequested?.Invoke();

        /// <summary>Simulates pressing Remove road.</summary>
        public void PressRemove() => RemoveRequested?.Invoke();

        /// <summary>Simulates pressing Cancel.</summary>
        public void PressCancel() => CancelRequested?.Invoke();

        /// <summary>Whether <paramref name="screenPosition"/> (pixels, origin bottom left) is over the shown bar.</summary>
        public bool IsOverUi(Vector2 screenPosition)
        {
            return UiRoot.IsOver(Root, screenPosition);
        }

        /// <summary>Finds the elements once the document's tree exists.</summary>
        private void Start()
        {
            VisualElement? root = Root;
            Elements? elements = root == null ? null : Elements.Find(root);
            if (root == null || elements == null)
            {
                ClientLog.Error("RoadBar: the layout lacks one of its elements.");
                return;
            }

            _elements = elements;
            elements.Place.clicked += PressPlace;
            elements.Upgrade.clicked += PressUpgrade;
            elements.Remove.clicked += PressRemove;
            elements.Cancel.clicked += PressCancel;
            root.RegisterCallback<GeometryChangedEvent>(_ => UiRoot.PadBySafeArea(root, BasePadding));
            Refresh();
        }

        /// <summary>The elements of <c>RoadBar.uxml</c> looked up by name.</summary>
        private sealed class Elements
        {
            /// <summary>The bar container.</summary>
            public VisualElement Bar = null!;

            /// <summary>The labels.</summary>
            public Label Prompt = null!, Detail = null!, Hint = null!;

            /// <summary>The buttons.</summary>
            public Button Place = null!, Upgrade = null!, Remove = null!, Cancel = null!;

            /// <summary>Finds all elements in <paramref name="root"/>.</summary>
            public static Elements? Find(VisualElement root)
            {
                var found = new Elements
                {
                    Bar = root.Q<VisualElement>(BarName),
                    Prompt = root.Q<Label>(PromptName),
                    Detail = root.Q<Label>(DetailName),
                    Hint = root.Q<Label>(HintName),
                    Place = root.Q<Button>(PlaceButtonName),
                    Upgrade = root.Q<Button>(UpgradeButtonName),
                    Remove = root.Q<Button>(RemoveButtonName),
                    Cancel = root.Q<Button>(CancelButtonName),
                };
                bool complete = found.Bar != null && found.Prompt != null && found.Detail != null && found.Hint != null
                    && found.Place != null && found.Upgrade != null && found.Remove != null && found.Cancel != null;
                return complete ? found : null;
            }
        }
    }
}
