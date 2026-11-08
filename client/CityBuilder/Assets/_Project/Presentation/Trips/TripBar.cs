#nullable enable

using System;
using CityBuilder.Client.Core;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Presentation.Trips
{
    /// <summary>
    /// The manual trip bar at the bottom of the screen (CR-0008): a thin view over <see cref="TripFlowModel"/>. It loads <c>UI/TripBar</c>
    /// from <c>Resources</c>, shows the prompt, walk detail and refusal hint, lets the player cycle the item or choose <c>All</c>, <c>Half</c>,
    /// <c>Quarter</c> (and a percentage once unlocked), and raises <see cref="ConfirmRequested"/> or <see cref="CancelRequested"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TripBar : MonoBehaviour
    {
        /// <summary>The trip bar layout (UXML), loaded from <c>Assets/_Project/Resources</c>.</summary>
        private const string LayoutPath = "UI/TripBar";

        /// <summary>The shared panel settings.</summary>
        private const string PanelSettingsPath = "UI/PanelSettings";

        /// <summary>The root's padding in panel units before the safe area is added.</summary>
        private const float BasePadding = 8f;

        /// <summary>Drawn at the same height as the placement bar (1.2), above the HUD and dock and under toasts.</summary>
        private const float SortingOrder = 1.2f;

        /// <summary>The name of the root element in <c>TripBar.uxml</c>.</summary>
        public const string RootName = "trip-root";

        /// <summary>The name of the bar element.</summary>
        public const string BarName = "trip-bar";

        /// <summary>The name of the prompt label.</summary>
        public const string PromptName = "trip-prompt";

        /// <summary>The name of the detail label.</summary>
        public const string DetailName = "trip-detail";

        /// <summary>The name of the refusal hint label.</summary>
        public const string HintName = "trip-hint";

        /// <summary>The name of the item chip button.</summary>
        public const string ItemButtonName = "trip-item";

        /// <summary>The name of the All preset button.</summary>
        public const string AllButtonName = "trip-all";

        /// <summary>The name of the Half preset button.</summary>
        public const string HalfButtonName = "trip-half";

        /// <summary>The name of the Quarter preset button.</summary>
        public const string QuarterButtonName = "trip-quarter";

        /// <summary>The name of the percentage button.</summary>
        public const string PercentButtonName = "trip-percent";

        /// <summary>The name of the Send button.</summary>
        public const string ConfirmButtonName = "trip-confirm";

        /// <summary>The name of the Cancel button.</summary>
        public const string CancelButtonName = "trip-cancel";

        /// <summary>The USS class that hides the bar while <see cref="TripPhase.Idle"/>.</summary>
        private const string HiddenClass = "trip-bar--hidden";

        /// <summary>The USS class that hides the hint label while empty.</summary>
        private const string HintHiddenClass = "trip-hint--hidden";

        /// <summary>The UI document of the bar.</summary>
        private UIDocument? _document;

        /// <summary>The model the bar follows.</summary>
        private TripFlowModel? _model;

        /// <summary>The elements the bar writes to; null until <c>Start</c> finds them.</summary>
        private Elements? _elements;

        /// <summary>Raised when the player presses the item chip button to cycle the item.</summary>
        public event Action? ItemCycleRequested;

        /// <summary>Raised when the player presses a preset button (<c>all</c>, <c>half</c>, or <c>quarter</c>).</summary>
        public event Action<string>? PresetRequested;

        /// <summary>Raised when the player presses the percentage button.</summary>
        public event Action? PercentRequested;

        /// <summary>Raised when the player presses Send.</summary>
        public event Action? ConfirmRequested;

        /// <summary>Raised when the player presses Cancel.</summary>
        public event Action? CancelRequested;

        /// <summary>The root of the bar's element tree; null when the layout was not loaded.</summary>
        public VisualElement? Root => _document == null ? null : _document.rootVisualElement;

        /// <summary>Creates the trip bar under <paramref name="parent"/>.</summary>
        public static TripBar? Create(Transform parent, TripFlowModel model)
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
                    "TripBar: cannot show the trip bar, an asset is missing under Assets/_Project/Resources: " +
                    $"{PanelSettingsPath} {(panelSettings == null ? "missing" : "found")}, " +
                    $"{LayoutPath} {(layout == null ? "missing" : "found")}.");
                return null;
            }

            var barObject = new GameObject("Trip Bar");
            barObject.transform.SetParent(parent, false);
            barObject.SetActive(false);
            var bar = barObject.AddComponent<TripBar>();
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
            TripFlowModel? model = _model;
            if (e == null || model == null)
            {
                return;
            }

            bool hidden = model.Phase == TripPhase.Idle;
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

            bool interactive = model.Phase != TripPhase.Sending;
            e.Item.text = model.ItemButtonText;
            e.Item.SetEnabled(interactive && model.AvailableItems.Count > 1);
            e.All.SetEnabled(interactive);
            e.Half.SetEnabled(interactive);
            e.Quarter.SetEnabled(interactive);
            e.Percent.style.display = model.IsPercentUnlocked ? DisplayStyle.Flex : DisplayStyle.None;
            e.Percent.SetEnabled(interactive && model.IsPercentUnlocked);
            if (model.Amount.Mode == AmountMode.Percent)
            {
                e.Percent.text = model.Amount.ToString();
            }

            e.Confirm.style.display = model.Phase == TripPhase.PickingTarget ? DisplayStyle.None : DisplayStyle.Flex;
            e.Confirm.SetEnabled(model.CanConfirm);
            e.Cancel.SetEnabled(interactive);
        }

        /// <summary>Simulates pressing Send.</summary>
        public void PressConfirm() => ConfirmRequested?.Invoke();

        /// <summary>Simulates pressing Cancel.</summary>
        public void PressCancel() => CancelRequested?.Invoke();

        /// <summary>Simulates pressing the item chip button.</summary>
        public void PressItem() => ItemCycleRequested?.Invoke();

        /// <summary>Simulates pressing a preset button (<c>all</c>, <c>half</c>, or <c>quarter</c>).</summary>
        public void PressPreset(string presetId) => PresetRequested?.Invoke(presetId);

        /// <summary>Simulates pressing the percentage button.</summary>
        public void PressPercent() => PercentRequested?.Invoke();

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
                ClientLog.Error("TripBar: the layout lacks one of its elements.");
                return;
            }

            _elements = elements;
            elements.Item.clicked += PressItem;
            elements.All.clicked += () => PressPreset("all");
            elements.Half.clicked += () => PressPreset("half");
            elements.Quarter.clicked += () => PressPreset("quarter");
            elements.Percent.clicked += PressPercent;
            elements.Confirm.clicked += PressConfirm;
            elements.Cancel.clicked += PressCancel;
            root.RegisterCallback<GeometryChangedEvent>(_ => UiRoot.PadBySafeArea(root, BasePadding));
            Refresh();
        }

        /// <summary>The elements of <c>TripBar.uxml</c> looked up by name.</summary>
        private sealed class Elements
        {
            /// <summary>The bar container.</summary>
            public VisualElement Bar = null!;

            /// <summary>The labels.</summary>
            public Label Prompt = null!, Detail = null!, Hint = null!;

            /// <summary>The buttons.</summary>
            public Button Item = null!, All = null!, Half = null!, Quarter = null!, Percent = null!, Confirm = null!, Cancel = null!;

            /// <summary>Finds all elements in <paramref name="root"/>.</summary>
            public static Elements? Find(VisualElement root)
            {
                var found = new Elements
                {
                    Bar = root.Q<VisualElement>(BarName),
                    Prompt = root.Q<Label>(PromptName),
                    Detail = root.Q<Label>(DetailName),
                    Hint = root.Q<Label>(HintName),
                    Item = root.Q<Button>(ItemButtonName),
                    All = root.Q<Button>(AllButtonName),
                    Half = root.Q<Button>(HalfButtonName),
                    Quarter = root.Q<Button>(QuarterButtonName),
                    Percent = root.Q<Button>(PercentButtonName),
                    Confirm = root.Q<Button>(ConfirmButtonName),
                    Cancel = root.Q<Button>(CancelButtonName),
                };
                bool complete = found.Bar != null && found.Prompt != null && found.Detail != null && found.Hint != null
                    && found.Item != null && found.All != null && found.Half != null && found.Quarter != null && found.Percent != null
                    && found.Confirm != null && found.Cancel != null;
                return complete ? found : null;
            }
        }
    }
}
