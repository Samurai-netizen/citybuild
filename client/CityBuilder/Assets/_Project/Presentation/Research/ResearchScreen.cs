#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Presentation.Research
{
    /// <summary>
    /// The research screen on the screen (M0-52): a thin component over a <see cref="ResearchScreenModel"/>. It loads <c>UI/Research</c> and
    /// <c>UI/ResearchNode</c> from <c>Resources</c>, makes one node element per node of the era in the column the model gives it, writes the
    /// model's words into the elements by name, draws the progress ring of the running node, and enables each Finance button by what the model
    /// allows. A press on Finance only raises <see cref="FinanceRequested"/> with the node's id: what is sent, and when, is the flow's, and
    /// nothing here decides or calls the server. A button at the bottom left opens and closes the panel; opening it raises
    /// <see cref="Opened"/> so the other menus close (<see cref="MenuGroup"/>). <c>GameRoot</c> creates it once the start has connected, with
    /// <see cref="Create"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResearchScreen : MonoBehaviour
    {
        /// <summary>The screen layout (UXML), loaded by name from <c>Assets/_Project/Resources</c>.</summary>
        private const string LayoutPath = "UI/Research";

        /// <summary>The layout of one node, cloned once per node.</summary>
        private const string NodeLayoutPath = "UI/ResearchNode";

        /// <summary>The panel settings the UI shares with the other screens.</summary>
        private const string PanelSettingsPath = "UI/PanelSettings";

        /// <summary>The root's padding in panel units before the safe area is added.</summary>
        private const float BasePadding = 8f;

        /// <summary>Drawn over the inspector (0.8) and under the build menu (1), the placement bar (2) and the toasts (3); it is never open together with the first two.</summary>
        private const float SortingOrder = 0.9f;

        /// <summary>The width of a node ring's stroke in panel units.</summary>
        private const float RingWidth = 3f;

        /// <summary>The name of the root element in <c>Research.uxml</c>.</summary>
        public const string RootName = "research-root";

        /// <summary>The name of the panel that is shown and hidden.</summary>
        public const string PanelName = "research-panel";

        /// <summary>The name of the label with the era header.</summary>
        public const string EraName = "research-era";

        /// <summary>The name of the label with the rule of the era.</summary>
        public const string RuleName = "research-rule";

        /// <summary>The name of the element the columns go into.</summary>
        public const string ColumnsName = "research-columns";

        /// <summary>The name of the button that opens and closes the panel.</summary>
        public const string ToggleName = "research-toggle";

        /// <summary>The name of the panel's close button.</summary>
        public const string CloseName = "research-close";

        /// <summary>The name of the root element of one node in <c>ResearchNode.uxml</c>.</summary>
        public const string NodeName = "research-node";

        /// <summary>The name of a node's picture.</summary>
        public const string NodeIconName = "research-node-icon";

        /// <summary>The name of a node's name label.</summary>
        public const string NodeNameName = "research-node-name";

        /// <summary>The name of a node's progress ring.</summary>
        public const string NodeRingName = "research-node-ring";

        /// <summary>The name of a node's effect label.</summary>
        public const string NodeEffectName = "research-node-effect";

        /// <summary>The name of a node's cost label.</summary>
        public const string NodeCostName = "research-node-cost";

        /// <summary>The name of a node's duration label.</summary>
        public const string NodeTimeName = "research-node-time";

        /// <summary>The name of a node's status label.</summary>
        public const string NodeStatusName = "research-node-status";

        /// <summary>The name of a node's note label (why Finance is off).</summary>
        public const string NodeNoteName = "research-node-note";

        /// <summary>The name of a node's Finance button.</summary>
        public const string NodeFinanceName = "research-node-finance";

        /// <summary>The USS class of the panel while it is closed.</summary>
        private const string HiddenClass = "research-panel--hidden";

        /// <summary>The USS class of the rule line while the era leads nowhere (M0).</summary>
        private const string RuleOffClass = "research-rule--off";

        /// <summary>The track of a node's ring.</summary>
        private static readonly Color RingTrackColor = new Color(0.48f, 0.31f, 0.18f, 0.28f);

        /// <summary>The finished part of a node's ring.</summary>
        private static readonly Color RingProgressColor = new Color(0.23f, 0.49f, 0.20f, 1f);

        /// <summary>The node elements, in the order of the model's nodes.</summary>
        private readonly List<NodeElements> _nodes = new List<NodeElements>();

        /// <summary>The node layout; null before <see cref="Create"/>.</summary>
        private VisualTreeAsset? _nodeLayout;

        /// <summary>The UI document of the screen; null before <see cref="Create"/> made it.</summary>
        private UIDocument? _document;

        /// <summary>What the screen shows now, or null before the first <see cref="Show"/>.</summary>
        private ResearchScreenModel? _model;

        /// <summary>The ids of the shown nodes with their columns, such as <c>0:sharp_axes|1:frame_saw</c>; the elements are made again when it changes.</summary>
        private string _structure = string.Empty;

        /// <summary>The elements the screen writes to; null until <c>Start</c> found them.</summary>
        private Elements? _elements;

        /// <summary>Whether the panel is open.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Raised when the player presses Finance on a node whose button works; the argument is the node's id.</summary>
        public event Action<string>? FinanceRequested;

        /// <summary>Raised when the panel goes from closed to open (never for a call that changes nothing), so the other menus can close (<see cref="MenuGroup"/>).</summary>
        public event Action? Opened;

        /// <summary>What the screen shows now; null before the first <see cref="Show"/>.</summary>
        public ResearchScreenModel? Model => _model;

        /// <summary>The root of the screen's element tree; null when the layout was not loaded.</summary>
        public VisualElement? Root => _document == null ? null : _document.rootVisualElement;

        /// <summary>
        /// Makes the screen: a child object of <paramref name="parent"/> with a <see cref="UIDocument"/> of the shared panel settings. The
        /// elements are found in <c>Start</c>, when the document's tree exists. Returns null, after logging one error that names the assets,
        /// when one is missing.
        /// </summary>
        /// <param name="parent">The object the screen's object goes under.</param>
        public static ResearchScreen? Create(Transform parent)
        {
            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsPath);
            var layout = Resources.Load<VisualTreeAsset>(LayoutPath);
            var nodeLayout = Resources.Load<VisualTreeAsset>(NodeLayoutPath);
            if (panelSettings == null || layout == null || nodeLayout == null)
            {
                Debug.LogError(
                    "ResearchScreen: cannot show the research screen, an asset is missing under Assets/_Project/Resources: " +
                    $"{PanelSettingsPath} {(panelSettings == null ? "missing" : "found")}, " +
                    $"{LayoutPath} {(layout == null ? "missing" : "found")}, " +
                    $"{NodeLayoutPath} {(nodeLayout == null ? "missing" : "found")}.");
                return null;
            }

            // A UIDocument builds its tree when it is enabled, so it gets its settings while the object is still inactive.
            var screenObject = new GameObject("Research Screen");
            screenObject.transform.SetParent(parent, false);
            screenObject.SetActive(false);
            var screen = screenObject.AddComponent<ResearchScreen>();
            screen._nodeLayout = nodeLayout;
            screen._document = screenObject.AddComponent<UIDocument>();
            screen._document.panelSettings = panelSettings;
            screen._document.visualTreeAsset = layout;
            screen._document.sortingOrder = SortingOrder;
            screenObject.SetActive(true);
            return screen;
        }

        /// <summary>
        /// Shows <paramref name="model"/>. A model equal to the one shown changes nothing, so the once-a-second refresh costs nothing while the
        /// words stay the same. The node elements are made again only when the set of nodes or their columns differ.
        /// </summary>
        /// <param name="model">What to show.</param>
        /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
        public void Show(ResearchScreenModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (Equals(_model, model))
            {
                return;
            }

            _model = model;
            Refresh();
        }

        /// <summary>Opens or closes the panel. Opening a closed panel raises <see cref="Opened"/>.</summary>
        public void SetOpen(bool open)
        {
            bool opens = open && !IsOpen;
            IsOpen = open;
            Elements? e = _elements;
            if (e != null)
            {
                e.Panel.EnableInClassList(HiddenClass, !open);
                e.Toggle.text = open ? "Close" : "Research";
            }

            if (opens)
            {
                Opened?.Invoke();
            }
        }

        /// <summary>The player pressed Finance on the node <paramref name="id"/>: what a finger on the button does, as a call. Does nothing unless the model allows it.</summary>
        /// <returns>True when <see cref="FinanceRequested"/> was raised.</returns>
        public bool PressFinance(string id)
        {
            ResearchNodeModel? node = FindNode(id);
            if (node == null || !node.ButtonEnabled)
            {
                return false;
            }

            FinanceRequested?.Invoke(id);
            return true;
        }

        /// <summary>
        /// Whether a screen position (pixels, origin bottom left) is over the panel or the button. The ground ignores a press there. False before
        /// the UI exists.
        /// </summary>
        public bool IsOverUi(Vector2 screenPosition)
        {
            return UiRoot.IsOver(Root, screenPosition);
        }

        /// <summary>The Finance button of the node <paramref name="id"/>, for a test to read; null when there is no such node.</summary>
        public Button? FinanceButton(string id)
        {
            for (int index = 0; index < _nodes.Count; index++)
            {
                if (_nodes[index].Id == id)
                {
                    return _nodes[index].Finance;
                }
            }

            return null;
        }

        /// <summary>Unity calls it before the first frame: the document's tree exists now, so the elements are found and the model is shown.</summary>
        private void Start()
        {
            VisualElement? root = Root;
            Elements? elements = root == null ? null : Elements.Find(root);
            if (root == null || elements == null)
            {
                Debug.LogError("ResearchScreen: the layout lacks one of its elements (" + RootName + ", " + PanelName + ", ... see the name constants).");
                return;
            }

            _elements = elements;
            elements.Toggle.clicked += () => SetOpen(!IsOpen);
            elements.Close.clicked += () => SetOpen(false);
            root.RegisterCallback<GeometryChangedEvent>(_ => UiRoot.PadBySafeArea(root, BasePadding));
            SetOpen(IsOpen);
            Refresh();
        }

        /// <summary>Writes the model's words into the elements; makes the node elements again when the set of nodes or the columns changed.</summary>
        private void Refresh()
        {
            Elements? e = _elements;
            ResearchScreenModel? model = _model;
            if (e == null || model == null)
            {
                return;
            }

            e.Era.text = model.HeaderText;
            e.Rule.text = ResearchScreenModel.RuleText;
            e.Rule.EnableInClassList(RuleOffClass, !model.RuleIsActive);
            string structure = StructureOf(model);
            if (structure != _structure)
            {
                _structure = structure;
                BuildNodes(e, model);
            }

            for (int index = 0; index < _nodes.Count && index < model.Nodes.Count; index++)
            {
                Write(_nodes[index], model.Nodes[index]);
            }
        }

        /// <summary>A text of the nodes' ids and columns, equal for two models that need the same elements.</summary>
        private static string StructureOf(ResearchScreenModel model)
        {
            var text = new StringBuilder();
            for (int index = 0; index < model.Nodes.Count; index++)
            {
                text.Append(index == 0 ? string.Empty : "|").Append(model.Nodes[index].Column).Append(':').Append(model.Nodes[index].Id);
            }

            return text.ToString();
        }

        /// <summary>Empties the columns and makes one column element per column and one node element per node.</summary>
        private void BuildNodes(Elements e, ResearchScreenModel model)
        {
            e.Columns.Clear();
            _nodes.Clear();
            var columns = new List<VisualElement>();
            for (int column = 0; column < model.ColumnCount; column++)
            {
                var element = new VisualElement();
                element.AddToClassList("research-column");
                e.Columns.Add(element);
                columns.Add(element);
            }

            for (int index = 0; index < model.Nodes.Count; index++)
            {
                NodeElements node = MakeNode(model.Nodes[index].Id);
                columns[model.Nodes[index].Column].Add(node.Root);
                _nodes.Add(node);
            }
        }

        /// <summary>Clones the node layout for the node <paramref name="id"/>, sets its picture and hooks its Finance button to <see cref="PressFinance"/>.</summary>
        private NodeElements MakeNode(string id)
        {
            VisualElement clone = _nodeLayout!.Instantiate();
            var node = new NodeElements
            {
                Id = id,
                Root = clone.Q<VisualElement>(NodeName),
                Icon = clone.Q<VisualElement>(NodeIconName),
                Ring = clone.Q<VisualElement>(NodeRingName),
                Name = clone.Q<Label>(NodeNameName),
                Effect = clone.Q<Label>(NodeEffectName),
                Cost = clone.Q<Label>(NodeCostName),
                Time = clone.Q<Label>(NodeTimeName),
                Status = clone.Q<Label>(NodeStatusName),
                Note = clone.Q<Label>(NodeNoteName),
                Finance = clone.Q<Button>(NodeFinanceName),
            };
            // The picture belongs to the node and never changes, so it is set once, here.
            Texture2D? icon = UiIcons.ForResearch(new ResearchId(id));
            UiIcons.Show(node.Icon, icon);
            node.Icon.style.display = icon == null ? DisplayStyle.None : DisplayStyle.Flex;
            node.Finance.clicked += () => PressFinance(id);
            node.Ring.generateVisualContent += context => PaintRing(context, node.Permille);
            return node;
        }

        /// <summary>Writes one node's words into its elements, marks its status, and shows or hides the ring, the note and the button.</summary>
        private static void Write(NodeElements node, ResearchNodeModel model)
        {
            node.Name.text = model.Name;
            node.Effect.text = model.EffectText;
            node.Cost.text = model.CostText;
            node.Time.text = model.TimeText;
            node.Status.text = model.StatusText;
            SetLine(node.Note, model.ButtonNote);
            node.Root.EnableInClassList("research-node--locked", model.Status == ResearchStatus.Locked);
            node.Root.EnableInClassList("research-node--available", model.Status == ResearchStatus.Available);
            node.Root.EnableInClassList("research-node--running", model.Status == ResearchStatus.InProgress);
            node.Root.EnableInClassList("research-node--done", model.Status == ResearchStatus.Done);
            node.Finance.style.display = model.ShowsButton ? DisplayStyle.Flex : DisplayStyle.None;
            node.Finance.SetEnabled(model.ButtonEnabled);
            node.Ring.style.display = model.Status == ResearchStatus.InProgress ? DisplayStyle.Flex : DisplayStyle.None;
            if (node.Permille != model.ProgressPermille)
            {
                node.Permille = model.ProgressPermille;
                node.Ring.MarkDirtyRepaint();
            }
        }

        /// <summary>Sets a label's text and hides the label when the text is empty, so an empty line takes no room.</summary>
        private static void SetLine(Label label, string text)
        {
            label.text = text;
            label.style.display = text.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>The model of the node <paramref name="id"/>, or null when the screen shows no such node.</summary>
        private ResearchNodeModel? FindNode(string id)
        {
            if (_model == null)
            {
                return null;
            }

            for (int index = 0; index < _model.Nodes.Count; index++)
            {
                if (_model.Nodes[index].Id == id)
                {
                    return _model.Nodes[index];
                }
            }

            return null;
        }

        /// <summary>Draws a node's ring: a faint full circle and, from the top, an arc of <paramref name="permille"/> thousandths of it.</summary>
        private static void PaintRing(MeshGenerationContext context, int permille)
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
            if (permille <= 0)
            {
                return;
            }

            // A full circle needs an end just short of 360 degrees: an arc whose end equals its start draws nothing.
            float sweep = Mathf.Min(permille * 360f / IntegerMath.PermilleScale, 359.9f);
            painter.strokeColor = RingProgressColor;
            painter.BeginPath();
            painter.Arc(center, radius, -90f, -90f + sweep);
            painter.Stroke();
        }

        /// <summary>The elements of one node on the screen, found once when the node is made.</summary>
        private sealed class NodeElements
        {
            /// <summary>The node's id.</summary>
            public string Id = string.Empty;

            /// <summary>The node's root element.</summary>
            public VisualElement Root = null!;

            /// <summary>The node's picture and its progress ring.</summary>
            public VisualElement Icon = null!, Ring = null!;

            /// <summary>The labels the screen writes.</summary>
            public Label Name = null!, Effect = null!, Cost = null!, Time = null!, Status = null!, Note = null!;

            /// <summary>The Finance button.</summary>
            public Button Finance = null!;

            /// <summary>The progress the ring was last drawn with, in permille.</summary>
            public int Permille;
        }

        /// <summary>The elements of the screen layout the screen writes to, found by name once.</summary>
        private sealed class Elements
        {
            /// <summary>The panel that is shown and hidden.</summary>
            public VisualElement Panel = null!;

            /// <summary>The element the columns go into.</summary>
            public VisualElement Columns = null!;

            /// <summary>The era header and the rule.</summary>
            public Label Era = null!, Rule = null!;

            /// <summary>The button that opens and closes the panel, and the panel's close button.</summary>
            public Button Toggle = null!, Close = null!;

            /// <summary>Finds every element in <paramref name="root"/>; null when one is missing.</summary>
            public static Elements? Find(VisualElement root)
            {
                var found = new Elements
                {
                    Panel = root.Q<VisualElement>(PanelName),
                    Columns = root.Q<VisualElement>(ColumnsName),
                    Era = root.Q<Label>(EraName),
                    Rule = root.Q<Label>(RuleName),
                    Toggle = root.Q<Button>(ToggleName),
                    Close = root.Q<Button>(CloseName),
                };
                bool complete = found.Panel != null && found.Columns != null && found.Era != null && found.Rule != null && found.Toggle != null
                    && found.Close != null;
                return complete ? found : null;
            }
        }
    }
}
