#nullable enable

using System.Collections.Generic;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Presentation.Hud
{
    /// <summary>
    /// The top HUD on the screen (M0-49): a thin component over a <see cref="HudModel"/>. It loads <c>UI/Hud</c> and <c>UI/HudChip</c> from
    /// <c>Resources</c>, makes one chip per <see cref="ItemChipModel"/> and writes the model's texts into the labels by name. The research ring is
    /// drawn with a painter from the ring's permille. It refreshes only when the model says something changed (at most once a second, with the
    /// displayed city), never in <c>Update</c>, and it takes no pointer: a drag that starts on the HUD still pans the ground. It decides nothing and
    /// sends nothing. <c>GameRoot</c> creates it once the start has connected, with <see cref="Create"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudView : MonoBehaviour
    {
        /// <summary>The HUD layout (UXML), loaded by name from <c>Assets/_Project/Resources</c>.</summary>
        private const string LayoutPath = "UI/Hud";

        /// <summary>The layout of one item chip, cloned once per item.</summary>
        private const string ChipLayoutPath = "UI/HudChip";

        /// <summary>The panel settings the UI shares with the other screens.</summary>
        private const string PanelSettingsPath = "UI/PanelSettings";

        /// <summary>The root's padding in panel units before the safe area is added.</summary>
        private const float BasePadding = 16f;

        /// <summary>Drawn under the build menu (sort order 1), the placement bar (2) and the toasts (3).</summary>
        private const float SortingOrder = 0.5f;

        /// <summary>The width of the ring's stroke in panel units.</summary>
        private const float RingWidth = 7f;

        /// <summary>The name of the root element in <c>Hud.uxml</c>.</summary>
        public const string RootName = "hud-root";

        /// <summary>The name of the row the chips go into.</summary>
        public const string ChipListName = "hud-chips";

        /// <summary>The name of the label with the free and provided workers.</summary>
        public const string WorkersName = "hud-workers";

        /// <summary>The name of the label with the busy builders and the slots.</summary>
        public const string BuildersName = "hud-builders";

        /// <summary>The name of the workers icon.</summary>
        public const string WorkersIconName = "hud-workers-icon";

        /// <summary>The name of the builders icon.</summary>
        public const string BuildersIconName = "hud-builders-icon";

        /// <summary>The name of the research icon in the middle of the ring.</summary>
        public const string RingIconName = "hud-ring-icon";

        /// <summary>The name of the element the research ring is drawn on.</summary>
        public const string RingName = "hud-ring";

        /// <summary>The name of the label with the running research's name.</summary>
        public const string ResearchNameName = "hud-research-name";

        /// <summary>The name of the label with the time left.</summary>
        public const string ResearchTimeName = "hud-research-time";

        /// <summary>The name of the era badge.</summary>
        public const string EraName = "hud-era";

        /// <summary>The name of the connection indicator's dot.</summary>
        public const string ConnectionDotName = "hud-connection-dot";

        /// <summary>The name of the connection indicator's word.</summary>
        public const string ConnectionTextName = "hud-connection-text";

        /// <summary>The name of a chip's root in <c>HudChip.uxml</c>.</summary>
        public const string ChipName = "hud-chip";

        /// <summary>The name of a chip's icon placeholder.</summary>
        public const string ChipIconName = "hud-chip-icon";

        /// <summary>The name of a chip's stock label.</summary>
        public const string ChipStockName = "hud-chip-stock";

        /// <summary>The name of a chip's cap label.</summary>
        public const string ChipCapName = "hud-chip-cap";

        /// <summary>The name of a chip's rate label.</summary>
        public const string ChipRateName = "hud-chip-rate";

        /// <summary>The USS class of a chip icon that shows a picture instead of the item's first letter.</summary>
        public const string ChipIconPictureClass = "hud-chip-icon--picture";

        /// <summary>The USS class of a chip whose item is at its cap.</summary>
        public const string ChipFullClass = "hud-chip--full";

        /// <summary>The USS class of a rate that rises.</summary>
        public const string RateRisingClass = "hud-rate--rising";

        /// <summary>The USS class of a rate that falls.</summary>
        public const string RateFallingClass = "hud-rate--falling";

        /// <summary>The USS class of the connection dot while connected.</summary>
        public const string DotOkClass = "hud-dot--ok";

        /// <summary>The USS class of the connection dot while starting.</summary>
        public const string DotPendingClass = "hud-dot--pending";

        /// <summary>The USS class of the connection dot while the connection is down.</summary>
        public const string DotProblemClass = "hud-dot--problem";

        /// <summary>The colour of the ring's track.</summary>
        private static readonly Color RingTrackColor = new Color(1f, 1f, 1f, 0.2f);

        /// <summary>The colour of the ring's progress.</summary>
        private static readonly Color RingProgressColor = new Color(0.47f, 0.76f, 1f, 1f);

        /// <summary>The model the screen shows; null before <see cref="Create"/>.</summary>
        private HudModel? _model;

        /// <summary>The UI document of the HUD; null before <see cref="Create"/> made it.</summary>
        private UIDocument? _document;

        /// <summary>The chip layout to clone; null before <see cref="Create"/>.</summary>
        private VisualTreeAsset? _chipLayout;

        /// <summary>The row the chips go into; null until <c>Start</c> found it.</summary>
        private VisualElement? _chipList;

        /// <summary>The chips on the screen, in the model's order.</summary>
        private readonly List<ChipElements> _chips = new List<ChipElements>();

        /// <summary>The workers label.</summary>
        private Label? _workers;

        /// <summary>The builders label.</summary>
        private Label? _builders;

        /// <summary>The element the ring is drawn on.</summary>
        private VisualElement? _ring;

        /// <summary>The research name label.</summary>
        private Label? _researchName;

        /// <summary>The time-left label.</summary>
        private Label? _researchTime;

        /// <summary>The era badge.</summary>
        private Label? _era;

        /// <summary>The connection dot.</summary>
        private VisualElement? _connectionDot;

        /// <summary>The connection word.</summary>
        private Label? _connectionText;

        /// <summary>The ring's progress as last written, in permille; the painter reads it.</summary>
        private int _ringPermille;

        /// <summary>The model the screen shows (<c>09</c> HUD); null before <see cref="Create"/>.</summary>
        public HudModel? Model => _model;

        /// <summary>The root of the HUD's element tree; null when the layout was not loaded.</summary>
        public VisualElement? Root => _document == null ? null : _document.rootVisualElement;

        /// <summary>
        /// Makes the HUD: a child object of <paramref name="parent"/> with a <see cref="UIDocument"/> of the shared panel settings. The elements
        /// are found in <c>Start</c>, before the first frame, when the document's tree exists. Returns null, after logging one error that names
        /// the assets, when one is missing.
        /// </summary>
        /// <param name="parent">The object the HUD's object goes under.</param>
        /// <param name="model">The state the HUD shows.</param>
        public static HudView? Create(Transform parent, HudModel model)
        {
            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsPath);
            var layout = Resources.Load<VisualTreeAsset>(LayoutPath);
            var chipLayout = Resources.Load<VisualTreeAsset>(ChipLayoutPath);
            if (panelSettings == null || layout == null || chipLayout == null)
            {
                Debug.LogError(
                    "HudView: cannot show the HUD, an asset is missing under Assets/_Project/Resources: " +
                    $"{PanelSettingsPath} {(panelSettings == null ? "missing" : "found")}, " +
                    $"{LayoutPath} {(layout == null ? "missing" : "found")}, " +
                    $"{ChipLayoutPath} {(chipLayout == null ? "missing" : "found")}.");
                return null;
            }

            // A UIDocument builds its tree when it is enabled, so it gets its settings while the object is still inactive.
            var hudObject = new GameObject("HUD");
            hudObject.transform.SetParent(parent, false);
            hudObject.SetActive(false);
            var view = hudObject.AddComponent<HudView>();
            view._document = hudObject.AddComponent<UIDocument>();
            view._document.panelSettings = panelSettings;
            view._document.visualTreeAsset = layout;
            view._document.sortingOrder = SortingOrder;
            view._chipLayout = chipLayout;
            view._model = model;
            hudObject.SetActive(true);
            return view;
        }

        /// <summary>Unity calls it before the first frame: the document's tree exists now, so the elements are found and the model is shown.</summary>
        private void Start()
        {
            VisualElement? root = Root;
            _chipList = root?.Q<VisualElement>(ChipListName);
            _workers = root?.Q<Label>(WorkersName);
            _builders = root?.Q<Label>(BuildersName);
            _ring = root?.Q<VisualElement>(RingName);
            VisualElement? workersIcon = root?.Q<VisualElement>(WorkersIconName);
            VisualElement? buildersIcon = root?.Q<VisualElement>(BuildersIconName);
            VisualElement? ringIcon = root?.Q<VisualElement>(RingIconName);
            _researchName = root?.Q<Label>(ResearchNameName);
            _researchTime = root?.Q<Label>(ResearchTimeName);
            _era = root?.Q<Label>(EraName);
            _connectionDot = root?.Q<VisualElement>(ConnectionDotName);
            _connectionText = root?.Q<Label>(ConnectionTextName);
            if (_model == null || root == null || _chipList == null || _workers == null || _builders == null || _ring == null
                || _researchName == null || _researchTime == null || _era == null || _connectionDot == null || _connectionText == null
                || workersIcon == null || buildersIcon == null || ringIcon == null)
            {
                Debug.LogError("HudView: the layout lacks one of its named elements (see the names in HudView).");
                return;
            }

            ShowFixedIcon(workersIcon, UiIcons.Workers);
            ShowFixedIcon(buildersIcon, UiIcons.Builders);
            ShowFixedIcon(ringIcon, UiIcons.Research);
            _ring.generateVisualContent += PaintRing;
            root.RegisterCallback<GeometryChangedEvent>(_ => UiRoot.PadBySafeArea(root, BasePadding));
            _model.Changed += Refresh;
            Refresh();
        }

        /// <summary>Puts one of the fixed pictures on its element; an element whose picture is missing is hidden, so it leaves no empty square.</summary>
        private static void ShowFixedIcon(VisualElement element, Texture2D? icon)
        {
            UiIcons.Show(element, icon);
            element.style.display = icon == null ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>Unity calls it when the object is destroyed: the model no longer reports to a screen that is gone.</summary>
        private void OnDestroy()
        {
            if (_model != null)
            {
                _model.Changed -= Refresh;
            }
        }

        /// <summary>Writes the model into the elements: the chips, the two counters, the ring, the era and the connection.</summary>
        private void Refresh()
        {
            if (_model == null || _chipList == null || _workers == null || _builders == null || _ring == null || _researchName == null
                || _researchTime == null || _era == null || _connectionDot == null || _connectionText == null)
            {
                return;
            }

            ShowChips(_model.Chips);
            _workers.text = _model.WorkersText;
            _builders.text = _model.BuildersText;
            _researchName.text = _model.Research.Name;
            _researchTime.text = _model.Research.TimeLeftText;
            _researchTime.style.display = _model.Research.TimeLeftText.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            _ringPermille = _model.Research.Permille;
            _ring.MarkDirtyRepaint();
            _era.text = _model.EraName;
            _connectionText.text = _model.ConnectionText;
            _connectionDot.EnableInClassList(DotOkClass, _model.ConnectionLevel == ConnectionLevel.Ok);
            _connectionDot.EnableInClassList(DotPendingClass, _model.ConnectionLevel == ConnectionLevel.Pending);
            _connectionDot.EnableInClassList(DotProblemClass, _model.ConnectionLevel == ConnectionLevel.Problem);
        }

        /// <summary>Makes the chips match <paramref name="models"/>: new elements only when the count differs, otherwise the texts are rewritten in place.</summary>
        private void ShowChips(IReadOnlyList<ItemChipModel> models)
        {
            if (_chips.Count != models.Count)
            {
                _chipList!.Clear();
                _chips.Clear();
                for (int index = 0; index < models.Count; index++)
                {
                    ChipElements chip = ChipElements.From(_chipLayout!.Instantiate());
                    _chipList.Add(chip.Root);
                    _chips.Add(chip);
                }
            }

            for (int index = 0; index < models.Count; index++)
            {
                _chips[index].Show(models[index]);
            }
        }

        /// <summary>Draws the ring on its element: a faint full circle and, from the top and clockwise, the progress.</summary>
        private void PaintRing(MeshGenerationContext context)
        {
            Rect rect = context.visualElement.contentRect;
            float radius = Mathf.Min(rect.width, rect.height) / 2f - RingWidth;
            if (radius <= 0f)
            {
                return;
            }

            var center = new Vector2(rect.width / 2f, rect.height / 2f);
            Painter2D painter = context.painter2D;
            painter.lineWidth = RingWidth;
            painter.lineCap = LineCap.Butt;
            painter.strokeColor = RingTrackColor;
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Stroke();
            if (_ringPermille <= 0)
            {
                return;
            }

            // A full circle needs an end just short of 360 degrees: an arc whose end equals its start draws nothing.
            float sweep = Mathf.Min(_ringPermille * 360f / IntegerMath.PermilleScale, 359.9f);
            painter.strokeColor = RingProgressColor;
            painter.BeginPath();
            painter.Arc(center, radius, -90f, -90f + sweep);
            painter.Stroke();
        }

        /// <summary>The elements of one chip on the screen, found once when the chip is made.</summary>
        private sealed class ChipElements
        {
            /// <summary>Creates the holder; use <see cref="From"/>.</summary>
            private ChipElements(VisualElement root, Label icon, Label stock, Label cap, Label rate)
            {
                Root = root;
                Icon = icon;
                Stock = stock;
                Cap = cap;
                Rate = rate;
            }

            /// <summary>The chip's root element.</summary>
            public VisualElement Root { get; }

            /// <summary>The item's picture; its first letter when the item has no picture.</summary>
            private Label Icon { get; }

            /// <summary>The stock label.</summary>
            private Label Stock { get; }

            /// <summary>The cap label, hidden when the item has none.</summary>
            private Label Cap { get; }

            /// <summary>The rate label.</summary>
            private Label Rate { get; }

            /// <summary>Finds the named elements under a freshly cloned chip.</summary>
            public static ChipElements From(VisualElement clone)
            {
                VisualElement root = clone.Q<VisualElement>(ChipName);
                return new ChipElements(
                    root,
                    clone.Q<Label>(ChipIconName),
                    clone.Q<Label>(ChipStockName),
                    clone.Q<Label>(ChipCapName),
                    clone.Q<Label>(ChipRateName));
            }

            /// <summary>Writes a chip model into the elements.</summary>
            public void Show(ItemChipModel chip)
            {
                Texture2D? picture = UiIcons.ForItem(chip.Item);
                UiIcons.Show(Icon, picture);
                Icon.EnableInClassList(ChipIconPictureClass, picture != null);
                Icon.text = picture != null || chip.Name.Length == 0 ? string.Empty : chip.Name.Substring(0, 1);
                Stock.text = chip.StockText;
                Cap.text = chip.CapText.Length == 0 ? string.Empty : "/ " + chip.CapText;
                Cap.style.display = chip.CapText.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
                Rate.text = chip.RateText;
                Rate.EnableInClassList(RateRisingClass, chip.Trend == RateTrend.Rising);
                Rate.EnableInClassList(RateFallingClass, chip.Trend == RateTrend.Falling);
                Root.EnableInClassList(ChipFullClass, chip.IsFull);
            }
        }
    }
}
