#nullable enable

using System;
using CityBuilder.Client.Presentation.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Presentation.Inspector
{
    /// <summary>
    /// The building inspector on the screen (M0-51): a thin component over a <see cref="BuildingInspectorModel"/>. It loads <c>UI/Inspector</c>
    /// from <c>Resources</c>, writes the model's words into the elements by name, enables the stepper's two buttons and the upgrade button by
    /// what the model allows, and shows the panel while there is a model (<see cref="Show"/> with null hides it). A press on a button only
    /// raises <see cref="MinusRequested"/>, <see cref="PlusRequested"/>, <see cref="UpgradeRequested"/> or <see cref="CloseRequested"/>: what
    /// they send, and when, is the flow's, and nothing here decides or calls the server. <c>GameRoot</c> creates it once the start has connected,
    /// with <see cref="Create"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingInspector : MonoBehaviour
    {
        /// <summary>The inspector layout (UXML), loaded by name from <c>Assets/_Project/Resources</c>.</summary>
        private const string LayoutPath = "UI/Inspector";

        /// <summary>The panel settings the UI shares with the other screens.</summary>
        private const string PanelSettingsPath = "UI/PanelSettings";

        /// <summary>The root's padding in panel units before the safe area is added.</summary>
        private const float BasePadding = 8f;

        /// <summary>Drawn over the HUD (0.5) and under the build menu (1), so an open sheet covers the inspector and not the other way round.</summary>
        private const float SortingOrder = 0.8f;

        /// <summary>The name of the root element in <c>Inspector.uxml</c>.</summary>
        public const string RootName = "inspector-root";

        /// <summary>The name of the panel that is shown and hidden.</summary>
        public const string PanelName = "inspector-panel";

        /// <summary>The name of the element that holds the building's picture.</summary>
        public const string IconName = "inspector-icon";

        /// <summary>The name of the building's name label.</summary>
        public const string NameName = "inspector-name";

        /// <summary>The name of the level label.</summary>
        public const string LevelName = "inspector-level";

        /// <summary>The name of the status label.</summary>
        public const string StatusName = "inspector-status";

        /// <summary>The name of the time label.</summary>
        public const string TimeName = "inspector-time";

        /// <summary>The name of the recipe label.</summary>
        public const string RecipeName = "inspector-recipe";

        /// <summary>The name of the rates label.</summary>
        public const string RatesName = "inspector-rates";

        /// <summary>The name of the row that holds the worker stepper.</summary>
        public const string StepperName = "inspector-stepper";

        /// <summary>The name of the stepper's − button.</summary>
        public const string MinusName = "inspector-minus";

        /// <summary>The name of the stepper's value label.</summary>
        public const string WorkersName = "inspector-workers";

        /// <summary>The name of the stepper's + button.</summary>
        public const string PlusName = "inspector-plus";

        /// <summary>The name of the preview label (the rates at the pending value).</summary>
        public const string PreviewName = "inspector-preview";

        /// <summary>The name of the note under the stepper.</summary>
        public const string StepperNoteName = "inspector-stepper-note";

        /// <summary>The name of the upgrade section's title label.</summary>
        public const string UpgradeTitleName = "inspector-upgrade-title";

        /// <summary>The name of the upgrade cost label.</summary>
        public const string UpgradeCostName = "inspector-upgrade-cost";

        /// <summary>The name of the upgrade time label.</summary>
        public const string UpgradeTimeName = "inspector-upgrade-time";

        /// <summary>The name of the upgrade gain label.</summary>
        public const string UpgradeGainName = "inspector-upgrade-gain";

        /// <summary>The name of the upgrade note label (why the button is off).</summary>
        public const string UpgradeNoteName = "inspector-upgrade-note";

        /// <summary>The name of the Upgrade button.</summary>
        public const string UpgradeName = "inspector-upgrade";

        /// <summary>The name of the close button.</summary>
        public const string CloseName = "inspector-close";

        /// <summary>The USS class of the panel while there is nothing to show.</summary>
        private const string HiddenClass = "inspector-panel--hidden";

        /// <summary>The UI document of the inspector; null before <see cref="Create"/> made it.</summary>
        private UIDocument? _document;

        /// <summary>What the screen shows now, or null for nothing.</summary>
        private BuildingInspectorModel? _model;

        /// <summary>The elements the inspector writes to; null until <c>Start</c> found them.</summary>
        private Elements? _elements;

        /// <summary>Raised when the player presses − (only while the model allows it).</summary>
        public event Action? MinusRequested;

        /// <summary>Raised when the player presses + (only while the model allows it).</summary>
        public event Action? PlusRequested;

        /// <summary>Raised when the player presses Upgrade (only while the model allows it).</summary>
        public event Action? UpgradeRequested;

        /// <summary>Raised when the player presses the close button.</summary>
        public event Action? CloseRequested;

        /// <summary>Raised when the panel goes from hidden to shown (not when a shown panel gets another model), so the other menus can close (<see cref="MenuGroup"/>).</summary>
        public event Action? Opened;

        /// <summary>The root of the inspector's element tree; null when the layout was not loaded.</summary>
        public VisualElement? Root => _document == null ? null : _document.rootVisualElement;

        /// <summary>What the screen shows now; null while the panel is hidden.</summary>
        public BuildingInspectorModel? Model => _model;

        /// <summary>Whether the panel is shown: there is a model and the elements exist.</summary>
        public bool IsShown => _elements != null && !_elements.Panel.ClassListContains(HiddenClass);

        /// <summary>
        /// Makes the inspector: a child object of <paramref name="parent"/> with a <see cref="UIDocument"/> of the shared panel settings. The
        /// elements are found in <c>Start</c>, before the first frame, when the document's tree exists. Returns null, after logging one error
        /// that names the assets, when one is missing.
        /// </summary>
        /// <param name="parent">The object the inspector's object goes under.</param>
        public static BuildingInspector? Create(Transform parent)
        {
            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsPath);
            var layout = Resources.Load<VisualTreeAsset>(LayoutPath);
            if (panelSettings == null || layout == null)
            {
                Debug.LogError(
                    "BuildingInspector: cannot show the inspector, an asset is missing under Assets/_Project/Resources: " +
                    $"{PanelSettingsPath} {(panelSettings == null ? "missing" : "found")}, " +
                    $"{LayoutPath} {(layout == null ? "missing" : "found")}.");
                return null;
            }

            // A UIDocument builds its tree when it is enabled, so it gets its settings while the object is still inactive.
            var inspectorObject = new GameObject("Building Inspector");
            inspectorObject.transform.SetParent(parent, false);
            inspectorObject.SetActive(false);
            var inspector = inspectorObject.AddComponent<BuildingInspector>();
            inspector._document = inspectorObject.AddComponent<UIDocument>();
            inspector._document.panelSettings = panelSettings;
            inspector._document.visualTreeAsset = layout;
            inspector._document.sortingOrder = SortingOrder;
            inspectorObject.SetActive(true);
            return inspector;
        }

        /// <summary>
        /// Shows <paramref name="model"/>, or hides the panel for null. A model equal to the one shown changes nothing, so the once-a-second
        /// refresh costs nothing while the words stay the same.
        /// </summary>
        /// <param name="model">What to show, or null for nothing.</param>
        public void Show(BuildingInspectorModel? model)
        {
            if (Equals(_model, model))
            {
                return;
            }

            _model = model;
            Refresh();
        }

        /// <summary>The player pressed −: what a finger on the button does, as a call. Does nothing unless the model allows it.</summary>
        public void PressMinus()
        {
            if (_model != null && _model.MinusEnabled)
            {
                MinusRequested?.Invoke();
            }
        }

        /// <summary>The player pressed +: what a finger on the button does, as a call. Does nothing unless the model allows it.</summary>
        public void PressPlus()
        {
            if (_model != null && _model.PlusEnabled)
            {
                PlusRequested?.Invoke();
            }
        }

        /// <summary>The player pressed Upgrade: what a finger on the button does, as a call. Does nothing unless the model allows it.</summary>
        public void PressUpgrade()
        {
            if (_model != null && _model.CanUpgrade)
            {
                UpgradeRequested?.Invoke();
            }
        }

        /// <summary>The player pressed the close button: what a finger on the button does, as a call.</summary>
        public void PressClose()
        {
            CloseRequested?.Invoke();
        }

        /// <summary>
        /// Whether a screen position (pixels, origin bottom left) is over the panel. The ground ignores a press there. False before the UI
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
            Elements? elements = root == null ? null : Elements.Find(root);
            if (root == null || elements == null)
            {
                Debug.LogError("BuildingInspector: the layout lacks one of its elements (" + RootName + ", " + PanelName + ", ... see the name constants).");
                return;
            }

            _elements = elements;
            elements.Minus.clicked += PressMinus;
            elements.Plus.clicked += PressPlus;
            elements.Upgrade.clicked += PressUpgrade;
            elements.Close.clicked += PressClose;
            root.RegisterCallback<GeometryChangedEvent>(_ => UiRoot.PadBySafeArea(root, BasePadding));
            Refresh();
        }

        /// <summary>Writes the model's words into the elements, enables the buttons by what the model allows, and shows the panel while there is a model.</summary>
        private void Refresh()
        {
            Elements? e = _elements;
            if (e == null)
            {
                return;
            }

            BuildingInspectorModel? model = _model;
            bool wasHidden = e.Panel.ClassListContains(HiddenClass);
            e.Panel.EnableInClassList(HiddenClass, model == null);
            if (model == null)
            {
                return;
            }

            UiIcons.Show(e.Icon, UiIcons.ForBuilding(model.TypeId));
            e.Name.text = model.Name;
            e.Level.text = model.LevelText;
            SetLine(e.Status, model.StatusText);
            SetLine(e.Time, model.TimeText);
            SetLine(e.Recipe, model.RecipeText);
            SetLine(e.Rates, model.RatesText);
            e.Stepper.style.display = model.HasStepper ? DisplayStyle.Flex : DisplayStyle.None;
            e.Workers.text = model.WorkersText;
            e.Minus.SetEnabled(model.MinusEnabled);
            e.Plus.SetEnabled(model.PlusEnabled);
            SetLine(e.Preview, model.PreviewText);
            SetLine(e.StepperNote, model.StepperNote);
            SetLine(e.UpgradeTitle, model.UpgradeTitle);
            SetLine(e.UpgradeCost, model.UpgradeCostText);
            SetLine(e.UpgradeTime, model.UpgradeTimeText);
            SetLine(e.UpgradeGain, model.UpgradeGainText);
            SetLine(e.UpgradeNote, model.UpgradeNote);
            e.Upgrade.SetEnabled(model.CanUpgrade);
            // At the highest level there is nothing to press: the title says so.
            e.Upgrade.style.display = model.UpgradeCostText.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            if (wasHidden)
            {
                Opened?.Invoke();
            }
        }

        /// <summary>Sets a label's text and hides the label when the text is empty, so an empty line takes no room.</summary>
        private static void SetLine(Label label, string text)
        {
            label.text = text;
            label.style.display = text.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>The elements of the layout the inspector writes to, found by name once.</summary>
        private sealed class Elements
        {
            /// <summary>The panel that is shown and hidden.</summary>
            public VisualElement Panel = null!;

            /// <summary>The element that holds the building's picture.</summary>
            public VisualElement Icon = null!;

            /// <summary>The element that holds the worker stepper.</summary>
            public VisualElement Stepper = null!;

            /// <summary>The labels the inspector writes.</summary>
            public Label Name = null!, Level = null!, Status = null!, Time = null!, Recipe = null!, Rates = null!, Workers = null!, Preview = null!,
                StepperNote = null!, UpgradeTitle = null!, UpgradeCost = null!, UpgradeTime = null!, UpgradeGain = null!, UpgradeNote = null!;

            /// <summary>The buttons.</summary>
            public Button Minus = null!, Plus = null!, Upgrade = null!, Close = null!;

            /// <summary>Finds every element in <paramref name="root"/>; null when one is missing.</summary>
            public static Elements? Find(VisualElement root)
            {
                var found = new Elements
                {
                    Panel = root.Q<VisualElement>(PanelName),
                    Icon = root.Q<VisualElement>(IconName),
                    Stepper = root.Q<VisualElement>(StepperName),
                    Name = root.Q<Label>(NameName),
                    Level = root.Q<Label>(LevelName),
                    Status = root.Q<Label>(StatusName),
                    Time = root.Q<Label>(TimeName),
                    Recipe = root.Q<Label>(RecipeName),
                    Rates = root.Q<Label>(RatesName),
                    Workers = root.Q<Label>(WorkersName),
                    Preview = root.Q<Label>(PreviewName),
                    StepperNote = root.Q<Label>(StepperNoteName),
                    UpgradeTitle = root.Q<Label>(UpgradeTitleName),
                    UpgradeCost = root.Q<Label>(UpgradeCostName),
                    UpgradeTime = root.Q<Label>(UpgradeTimeName),
                    UpgradeGain = root.Q<Label>(UpgradeGainName),
                    UpgradeNote = root.Q<Label>(UpgradeNoteName),
                    Minus = root.Q<Button>(MinusName),
                    Plus = root.Q<Button>(PlusName),
                    Upgrade = root.Q<Button>(UpgradeName),
                    Close = root.Q<Button>(CloseName),
                };
                bool complete = found.Panel != null && found.Icon != null && found.Stepper != null && found.Name != null && found.Level != null
                    && found.Status != null && found.Time != null && found.Recipe != null && found.Rates != null && found.Workers != null
                    && found.Preview != null && found.StepperNote != null && found.UpgradeTitle != null && found.UpgradeCost != null
                    && found.UpgradeTime != null && found.UpgradeGain != null && found.UpgradeNote != null && found.Minus != null
                    && found.Plus != null && found.Upgrade != null && found.Close != null;
                return complete ? found : null;
            }
        }
    }
}
