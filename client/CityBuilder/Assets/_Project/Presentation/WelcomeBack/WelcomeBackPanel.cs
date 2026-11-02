#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Client.Presentation.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Presentation.WelcomeBack
{
    /// <summary>
    /// The welcome-back report on the screen (M0-53): a thin component over a <see cref="WelcomeBackModel"/>. It loads <c>UI/WelcomeBack</c> from
    /// <c>Resources</c> and shows a card in the middle of a dark scrim with the title, one heading and its lines per section, the production-cap
    /// note, and a Continue button. <see cref="Show"/> opens it (and raises <see cref="Opened"/> so the other menus close, <see cref="MenuGroup"/>);
    /// Continue or <see cref="Dismiss"/> closes it and raises <see cref="Dismissed"/>. While it is open the scrim takes the pointer, so the ground
    /// and the other screens leave the screen alone. It decides nothing and calls no server. <c>GameRoot</c> creates it once the start has
    /// connected, with <see cref="Create"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WelcomeBackPanel : MonoBehaviour
    {
        /// <summary>The layout (UXML), loaded by name from <c>Assets/_Project/Resources</c>.</summary>
        private const string LayoutPath = "UI/WelcomeBack";

        /// <summary>The panel settings the UI shares with the other screens.</summary>
        private const string PanelSettingsPath = "UI/PanelSettings";

        /// <summary>The root's padding in panel units before the safe area is added.</summary>
        private const float BasePadding = 8f;

        /// <summary>Drawn over every other screen (the placement bar is 2) and under the toasts (3): it is modal, and a result still shows over it.</summary>
        private const float SortingOrder = 2.5f;

        /// <summary>The name of the root element in <c>WelcomeBack.uxml</c>.</summary>
        public const string RootName = "welcome-root";

        /// <summary>The name of the scrim that is shown and hidden and takes the pointer while shown.</summary>
        public const string ScrimName = "welcome-scrim";

        /// <summary>The name of the card on the scrim.</summary>
        public const string CardName = "welcome-card";

        /// <summary>The name of the title label.</summary>
        public const string TitleName = "welcome-title";

        /// <summary>The name of the element the sections go into.</summary>
        public const string SectionsName = "welcome-sections";

        /// <summary>The name of the label shown when nothing happened.</summary>
        public const string EmptyName = "welcome-empty";

        /// <summary>The name of the label with the production-cap note.</summary>
        public const string CapNoteName = "welcome-cap-note";

        /// <summary>The name of the Continue button.</summary>
        public const string ContinueName = "welcome-continue";

        /// <summary>The USS class of a section's heading.</summary>
        public const string HeadingClass = "welcome-heading";

        /// <summary>The USS class of a section's line.</summary>
        public const string LineClass = "welcome-line";

        /// <summary>The USS class of the scrim while the panel is closed.</summary>
        private const string HiddenClass = "welcome-scrim--hidden";

        /// <summary>The UI document of the panel; null before <see cref="Create"/> made it.</summary>
        private UIDocument? _document;

        /// <summary>The elements the panel writes to; null until <c>Start</c> found them.</summary>
        private Elements? _elements;

        /// <summary>What the panel shows, or null while it is closed and has never shown anything.</summary>
        private WelcomeBackModel? _model;

        /// <summary>Whether the report is open.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>What the panel shows now, or the last report after it was dismissed; null before the first <see cref="Show"/>.</summary>
        public WelcomeBackModel? Model => _model;

        /// <summary>Raised when the report goes from closed to open (a report shown while another one is open raises nothing), so the other menus can close (<see cref="MenuGroup"/>).</summary>
        public event Action? Opened;

        /// <summary>Raised when the player dismisses the open report.</summary>
        public event Action? Dismissed;

        /// <summary>The root of the panel's element tree; null when the layout was not loaded.</summary>
        public VisualElement? Root => _document == null ? null : _document.rootVisualElement;

        /// <summary>
        /// Makes the panel: a child object of <paramref name="parent"/> with a <see cref="UIDocument"/> of the shared panel settings. The elements
        /// are found in <c>Start</c>, when the document's tree exists. Returns null, after logging one error that names the assets, when one is
        /// missing.
        /// </summary>
        /// <param name="parent">The object the panel's object goes under.</param>
        public static WelcomeBackPanel? Create(Transform parent)
        {
            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsPath);
            var layout = Resources.Load<VisualTreeAsset>(LayoutPath);
            if (panelSettings == null || layout == null)
            {
                Debug.LogError(
                    "WelcomeBackPanel: cannot show the welcome-back report, an asset is missing under Assets/_Project/Resources: " +
                    $"{PanelSettingsPath} {(panelSettings == null ? "missing" : "found")}, " +
                    $"{LayoutPath} {(layout == null ? "missing" : "found")}.");
                return null;
            }

            // A UIDocument builds its tree when it is enabled, so it gets its settings while the object is still inactive.
            var panelObject = new GameObject("Welcome Back Panel");
            panelObject.transform.SetParent(parent, false);
            panelObject.SetActive(false);
            var panel = panelObject.AddComponent<WelcomeBackPanel>();
            panel._document = panelObject.AddComponent<UIDocument>();
            panel._document.panelSettings = panelSettings;
            panel._document.visualTreeAsset = layout;
            panel._document.sortingOrder = SortingOrder;
            panelObject.SetActive(true);
            return panel;
        }

        /// <summary>
        /// Shows <paramref name="model"/> and opens the panel. A report shown while another is open replaces it. Raises <see cref="Opened"/> when the
        /// panel was closed.
        /// </summary>
        /// <param name="model">The report.</param>
        /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
        public void Show(WelcomeBackModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            bool opens = !IsOpen;
            IsOpen = true;
            Refresh();
            if (opens)
            {
                Opened?.Invoke();
            }
        }

        /// <summary>Closes the report. Does nothing when it is closed; otherwise raises <see cref="Dismissed"/>.</summary>
        public void Dismiss()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            Refresh();
            Dismissed?.Invoke();
        }

        /// <summary>The player pressed Continue: what a finger on the button does, as a call.</summary>
        public void PressContinue()
        {
            Dismiss();
        }

        /// <summary>
        /// Whether a screen position (pixels, origin bottom left) is over the report: the whole screen while it is open, because the scrim takes the
        /// pointer; never while it is closed. False before the UI exists.
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
                Debug.LogError("WelcomeBackPanel: the layout lacks one of its elements (" + RootName + ", " + ScrimName + ", ... see the name constants).");
                return;
            }

            _elements = elements;
            elements.Continue.clicked += PressContinue;
            // The scrim fills the screen and holds the padding, so a press anywhere is the scrim's while the report is open.
            elements.Scrim.RegisterCallback<GeometryChangedEvent>(_ => UiRoot.PadBySafeArea(elements.Scrim, BasePadding));
            Refresh();
        }

        /// <summary>Writes the model into the elements and shows or hides the scrim; does nothing before the elements exist (<c>Start</c> calls it again).</summary>
        private void Refresh()
        {
            Elements? e = _elements;
            if (e == null)
            {
                return;
            }

            e.Scrim.EnableInClassList(HiddenClass, !IsOpen);
            WelcomeBackModel? model = _model;
            if (model == null)
            {
                return;
            }

            e.Title.text = model.Title;
            e.Sections.Clear();
            foreach (WelcomeBackSection section in model.Sections)
            {
                e.Sections.Add(MakeLabel(HeadingClass, section.Heading));
                foreach (string line in section.Lines)
                {
                    e.Sections.Add(MakeLabel(LineClass, line));
                }
            }

            SetLine(e.Empty, model.IsEmpty ? WelcomeBackModel.NothingHappenedText : string.Empty);
            SetLine(e.CapNote, model.CapNote);
        }

        /// <summary>A label with a USS class and a text.</summary>
        private static Label MakeLabel(string className, string text)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }

        /// <summary>Sets a label's text and hides the label when the text is empty, so an empty line takes no room.</summary>
        private static void SetLine(Label label, string text)
        {
            label.text = text;
            label.style.display = text.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>The elements of the layout the panel writes to, found by name once.</summary>
        private sealed class Elements
        {
            /// <summary>The scrim that is shown and hidden.</summary>
            public VisualElement Scrim = null!;

            /// <summary>The element the sections go into.</summary>
            public VisualElement Sections = null!;

            /// <summary>The labels the panel writes.</summary>
            public Label Title = null!, Empty = null!, CapNote = null!;

            /// <summary>The Continue button.</summary>
            public Button Continue = null!;

            /// <summary>Finds every element in <paramref name="root"/>; null when one is missing.</summary>
            public static Elements? Find(VisualElement root)
            {
                var found = new Elements
                {
                    Scrim = root.Q<VisualElement>(ScrimName),
                    Sections = root.Q<VisualElement>(SectionsName),
                    Title = root.Q<Label>(TitleName),
                    Empty = root.Q<Label>(EmptyName),
                    CapNote = root.Q<Label>(CapNoteName),
                    Continue = root.Q<Button>(ContinueName),
                };
                bool complete = found.Scrim != null && found.Sections != null && found.Title != null && found.Empty != null
                    && found.CapNote != null && found.Continue != null;
                return complete ? found : null;
            }
        }
    }
}
