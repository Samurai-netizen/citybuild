#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using CityBuilder.Client.Core;
using CityBuilder.Client.Presentation.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Presentation.Routes
{
    /// <summary>
    /// The courier route editor sheet (CR-0008-T2 part C): a parchment sheet above the dock with the courier's title and slot button, the
    /// summary, one row of stop cards (<c>UI/RouteStop</c>, each with its action rows from <c>UI/RouteAction</c>) that scrolls sideways only and
    /// is scaled down until it fits (OD-17), the reason Save is off, Revert and Save. It writes what <see cref="RouteEditorModel"/> says and
    /// raises one event per button; <see cref="RouteEditorFlow"/> turns the events into model calls. The cards are made again only when
    /// the words change, so the periodic city updates do not reset the scroll position. The sheet stays low so the map stays tappable.
    /// </summary>
    public sealed class RouteEditorScreen : MonoBehaviour
    {
        /// <summary>The screen layout (UXML), loaded by name from <c>Assets/_Project/Resources</c>.</summary>
        private const string LayoutPath = "UI/RouteEditor";

        /// <summary>The layout of one stop, cloned once per stop.</summary>
        private const string StopLayoutPath = "UI/RouteStop";

        /// <summary>The layout of one action, cloned once per action.</summary>
        private const string ActionLayoutPath = "UI/RouteAction";

        /// <summary>The panel settings the UI shares with the other screens.</summary>
        private const string PanelSettingsPath = "UI/PanelSettings";

        /// <summary>The root's padding in panel units before the safe area is added.</summary>
        private const float BasePadding = 8f;

        /// <summary>Drawn over the trip bar (1.2) and under the placement bar (2) and the toasts (3); it is never open together with them.</summary>
        private const float SortingOrder = 1.3f;

        /// <summary>The name of the root element in <c>RouteEditor.uxml</c>.</summary>
        public const string RootName = "route-root";

        /// <summary>The name of the sheet.</summary>
        public const string PanelName = "route-panel";

        /// <summary>The name of the title label.</summary>
        public const string TitleName = "route-title";

        /// <summary>The name of the button that switches to the station's next courier.</summary>
        public const string SlotName = "route-slot";

        /// <summary>The name of the close button.</summary>
        public const string CloseName = "route-close";

        /// <summary>The name of the summary label.</summary>
        public const string SummaryName = "route-summary";

        /// <summary>The name of the label with the last refused edit.</summary>
        public const string NoteName = "route-note";

        /// <summary>The name of the sideways scroll view.</summary>
        public const string ScrollName = "route-scroll";

        /// <summary>The name of the row that holds the stop cards.</summary>
        public const string StopsName = "route-stops";

        /// <summary>The name of the label with the note on locked amounts.</summary>
        public const string AmountNoteName = "route-amount-note";

        /// <summary>The name of the label with the reason Save is off.</summary>
        public const string HintName = "route-hint";

        /// <summary>The name of the Revert button.</summary>
        public const string RevertName = "route-revert";

        /// <summary>The name of the Save button.</summary>
        public const string ConfirmName = "route-confirm";

        /// <summary>The name of a stop card.</summary>
        public const string StopName = "route-stop";

        /// <summary>The name of a stop card's title label.</summary>
        public const string StopTitleName = "route-stop-title";

        /// <summary>The name of a stop card's walk label.</summary>
        public const string StopWalkName = "route-stop-walk";

        /// <summary>The name of the row that holds a stop's action rows.</summary>
        public const string StopActionsName = "route-stop-actions";

        /// <summary>The name of a stop card's add-action button.</summary>
        public const string StopAddActionName = "route-stop-add-action";

        /// <summary>The name of a stop card's move-earlier button.</summary>
        public const string StopLeftName = "route-stop-left";

        /// <summary>The name of a stop card's move-later button.</summary>
        public const string StopRightName = "route-stop-right";

        /// <summary>The name of a stop card's remove button.</summary>
        public const string StopRemoveName = "route-stop-remove";

        /// <summary>The name of an action row.</summary>
        public const string ActionName = "route-action";

        /// <summary>The name of an action's take or put button.</summary>
        public const string ActionKindName = "route-action-kind";

        /// <summary>The name of an action's item button.</summary>
        public const string ActionItemName = "route-action-item";

        /// <summary>The name of an action's amount button.</summary>
        public const string ActionAmountName = "route-action-amount";

        /// <summary>The name of an action's remove button.</summary>
        public const string ActionRemoveName = "route-action-remove";

        /// <summary>The USS class that hides the sheet.</summary>
        private const string HiddenClass = "route-panel--hidden";

        /// <summary>The stop layout; null before <see cref="Create"/>.</summary>
        private VisualTreeAsset? _stopLayout;

        /// <summary>The action layout; null before <see cref="Create"/>.</summary>
        private VisualTreeAsset? _actionLayout;

        /// <summary>The UI document of the screen; null before <see cref="Create"/> made it.</summary>
        private UIDocument? _document;

        /// <summary>The model the screen shows; null before <see cref="Show"/>.</summary>
        private RouteEditorModel? _model;

        /// <summary>The elements the screen writes to; null until <c>Start</c> found them.</summary>
        private Elements? _elements;

        /// <summary>The stop cards, in the order of the model's lines.</summary>
        private readonly List<VisualElement> _cards = new List<VisualElement>();

        /// <summary>What the cards show now; they are made again when it changes.</summary>
        private string _signature = string.Empty;

        /// <summary>Whether the sheet is open.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Raised when the sheet goes from closed to open, so the other menus can close (<see cref="MenuGroup"/>).</summary>
        public event Action? Opened;

        /// <summary>Raised when the player presses the courier slot button.</summary>
        public event Action? SlotRequested;

        /// <summary>Raised when the player presses Revert.</summary>
        public event Action? RevertRequested;

        /// <summary>Raised when the player presses Save (or Clear route) while the model allows it.</summary>
        public event Action? ConfirmRequested;

        /// <summary>Raised when the player presses the close button.</summary>
        public event Action? CloseRequested;

        /// <summary>Raised with a stop's index and −1 or +1 when the player moves the stop.</summary>
        public event Action<int, int>? StopMoveRequested;

        /// <summary>Raised with a stop's index when the player removes the stop.</summary>
        public event Action<int>? StopRemoveRequested;

        /// <summary>Raised with a stop's index when the player adds an action to it.</summary>
        public event Action<int>? ActionAddRequested;

        /// <summary>Raised with a stop's and an action's index when the player flips take and put.</summary>
        public event Action<int, int>? ActionKindRequested;

        /// <summary>Raised with a stop's and an action's index when the player changes the item.</summary>
        public event Action<int, int>? ActionItemRequested;

        /// <summary>Raised with a stop's and an action's index when the player changes the amount.</summary>
        public event Action<int, int>? ActionAmountRequested;

        /// <summary>Raised with a stop's and an action's index when the player removes the action.</summary>
        public event Action<int, int>? ActionRemoveRequested;

        /// <summary>The root of the screen's UI, or null before the document built its tree.</summary>
        public VisualElement? Root => _document == null ? null : _document.rootVisualElement;

        /// <summary>
        /// Makes the screen under <paramref name="parent"/>. Returns null (and logs an error) when an asset is missing under
        /// <c>Assets/_Project/Resources</c>.
        /// </summary>
        public static RouteEditorScreen? Create(Transform parent)
        {
            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsPath);
            var layout = Resources.Load<VisualTreeAsset>(LayoutPath);
            var stopLayout = Resources.Load<VisualTreeAsset>(StopLayoutPath);
            var actionLayout = Resources.Load<VisualTreeAsset>(ActionLayoutPath);
            if (panelSettings == null || layout == null || stopLayout == null || actionLayout == null)
            {
                ClientLog.Error(
                    "RouteEditorScreen: cannot show the route editor, an asset is missing under Assets/_Project/Resources: " +
                    $"{PanelSettingsPath} {(panelSettings == null ? "missing" : "found")}, " +
                    $"{LayoutPath} {(layout == null ? "missing" : "found")}, " +
                    $"{StopLayoutPath} {(stopLayout == null ? "missing" : "found")}, " +
                    $"{ActionLayoutPath} {(actionLayout == null ? "missing" : "found")}.");
                return null;
            }

            // A UIDocument builds its tree when it is enabled, so it gets its settings while the object is still inactive.
            var screenObject = new GameObject("Route Editor");
            screenObject.transform.SetParent(parent, false);
            screenObject.SetActive(false);
            var screen = screenObject.AddComponent<RouteEditorScreen>();
            screen._stopLayout = stopLayout;
            screen._actionLayout = actionLayout;
            screen._document = screenObject.AddComponent<UIDocument>();
            screen._document.panelSettings = panelSettings;
            screen._document.visualTreeAsset = layout;
            screen._document.sortingOrder = SortingOrder;
            screenObject.SetActive(true);
            return screen;
        }

        /// <summary>Sets the model the screen shows and writes it.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
        public void Show(RouteEditorModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            Refresh();
        }

        /// <summary>Writes the model's words into the elements again (cards only when their words changed).</summary>
        public void Refresh()
        {
            Elements? e = _elements;
            RouteEditorModel? model = _model;
            if (e == null || model == null)
            {
                return;
            }

            e.Title.text = model.TitleText;
            e.Slot.style.display = model.HasSeveralSlots ? DisplayStyle.Flex : DisplayStyle.None;
            SetLine(e.Summary, model.SummaryText);
            SetLine(e.Note, model.NoteText);
            SetLine(e.AmountNote, model.AmountNote);
            SetLine(e.Hint, model.HintText);
            e.Confirm.text = model.ConfirmText;
            e.Confirm.SetEnabled(model.CanConfirm);
            e.Revert.SetEnabled(model.Phase == RouteEditorPhase.Editing);
            string signature = SignatureOf(model);
            if (signature != _signature)
            {
                _signature = signature;
                BuildCards(e, model);
            }
        }

        /// <summary>Opens or closes the sheet; going from closed to open raises <see cref="Opened"/>.</summary>
        public void SetOpen(bool open)
        {
            bool opens = open && !IsOpen;
            IsOpen = open;
            _elements?.Panel.EnableInClassList(HiddenClass, !open);
            if (opens)
            {
                Opened?.Invoke();
            }
        }

        /// <summary>The player pressed the courier slot button: what a finger does, as a call.</summary>
        public void PressSlot() => SlotRequested?.Invoke();

        /// <summary>The player pressed Revert: what a finger does, as a call.</summary>
        public void PressRevert() => RevertRequested?.Invoke();

        /// <summary>The player pressed Save: what a finger does, as a call. Does nothing unless the model allows it.</summary>
        public void PressConfirm()
        {
            if (_model != null && _model.CanConfirm)
            {
                ConfirmRequested?.Invoke();
            }
        }

        /// <summary>The player pressed the close button: what a finger does, as a call.</summary>
        public void PressClose() => CloseRequested?.Invoke();

        /// <summary>The player pressed the remove button of stop <paramref name="index"/>: what a finger does, as a call.</summary>
        public void PressRemoveStop(int index) => StopRemoveRequested?.Invoke(index);

        /// <summary>The player pressed the add-action button of stop <paramref name="index"/>: what a finger does, as a call.</summary>
        public void PressAddAction(int index) => ActionAddRequested?.Invoke(index);

        /// <summary>Whether a screen position (pixels, origin bottom left) is over the sheet.</summary>
        public bool IsOverUi(Vector2 screenPosition) => UiRoot.IsOver(Root, screenPosition);

        /// <summary>Finds the elements and hooks the buttons once the document has built its tree.</summary>
        private void Start()
        {
            VisualElement? root = Root;
            Elements? elements = root == null ? null : Elements.Find(root);
            if (root == null || elements == null)
            {
                ClientLog.Error("RouteEditorScreen: the layout lacks one of its elements (" + RootName + ", " + PanelName + ", ... see the name constants).");
                return;
            }

            _elements = elements;
            elements.Slot.clicked += PressSlot;
            elements.Revert.clicked += PressRevert;
            elements.Confirm.clicked += PressConfirm;
            elements.Close.clicked += PressClose;
            root.RegisterCallback<GeometryChangedEvent>(_ => UiRoot.PadBySafeArea(root, BasePadding));
            elements.Scroll.RegisterCallback<GeometryChangedEvent>(_ => FitCards(elements));
            elements.Stops.RegisterCallback<GeometryChangedEvent>(_ => FitCards(elements));
            elements.Panel.EnableInClassList(HiddenClass, !IsOpen);
            Refresh();
        }

        /// <summary>Scales the row of cards down until the tallest card fits the scroll view's height (OD-17).</summary>
        private void FitCards(Elements e)
        {
            float needed = 0f;
            for (int index = 0; index < _cards.Count; index++)
            {
                VisualElement card = _cards[index];
                float bottom = card.layout.yMax + (card.parent == null ? 0f : card.parent.layout.y);
                if (bottom > needed)
                {
                    needed = bottom;
                }
            }

            ScaleToFit.Height(e.Stops, new Vector2(e.Stops.layout.width, needed), e.Scroll.contentViewport.layout.height);
        }

        /// <summary>Makes the stop cards again from the model's lines.</summary>
        private void BuildCards(Elements e, RouteEditorModel model)
        {
            e.Stops.Clear();
            _cards.Clear();
            for (int index = 0; index < model.Lines.Count; index++)
            {
                VisualElement card = MakeCard(index, model.Lines[index]);
                e.Stops.Add(card);
                _cards.Add(card);
            }
        }

        /// <summary>Clones the stop layout for stop <paramref name="stop"/>, writes its words, makes its action rows and hooks its buttons.</summary>
        private VisualElement MakeCard(int stop, RouteStopLine line)
        {
            VisualElement clone = _stopLayout!.Instantiate();
            VisualElement card = clone.Q<VisualElement>(StopName);
            clone.Q<Label>(StopTitleName).text = line.Title;
            SetLine(clone.Q<Label>(StopWalkName), line.WalkText);
            VisualElement actions = clone.Q<VisualElement>(StopActionsName);
            for (int action = 0; action < line.Parts.Count; action++)
            {
                actions.Add(MakeAction(stop, action, line.Parts[action]));
            }

            clone.Q<Button>(StopAddActionName).clicked += () => ActionAddRequested?.Invoke(stop);
            clone.Q<Button>(StopLeftName).clicked += () => StopMoveRequested?.Invoke(stop, -1);
            clone.Q<Button>(StopRightName).clicked += () => StopMoveRequested?.Invoke(stop, 1);
            clone.Q<Button>(StopRemoveName).clicked += () => StopRemoveRequested?.Invoke(stop);
            return card;
        }

        /// <summary>Clones the action layout for action <paramref name="action"/> of stop <paramref name="stop"/>, writes its words and hooks its buttons.</summary>
        private VisualElement MakeAction(int stop, int action, RouteActionLine line)
        {
            VisualElement clone = _actionLayout!.Instantiate();
            VisualElement row = clone.Q<VisualElement>(ActionName);
            Button kind = clone.Q<Button>(ActionKindName);
            Button item = clone.Q<Button>(ActionItemName);
            Button amount = clone.Q<Button>(ActionAmountName);
            kind.text = line.KindText;
            item.text = line.ItemText;
            amount.text = line.AmountText;
            kind.clicked += () => ActionKindRequested?.Invoke(stop, action);
            item.clicked += () => ActionItemRequested?.Invoke(stop, action);
            amount.clicked += () => ActionAmountRequested?.Invoke(stop, action);
            clone.Q<Button>(ActionRemoveName).clicked += () => ActionRemoveRequested?.Invoke(stop, action);
            return row;
        }

        /// <summary>Every word of every card, joined; two models with the same signature draw the same cards.</summary>
        private static string SignatureOf(RouteEditorModel model)
        {
            var text = new StringBuilder();
            for (int index = 0; index < model.Lines.Count; index++)
            {
                RouteStopLine line = model.Lines[index];
                text.Append(line.Title).Append('|').Append(line.WalkText);
                for (int action = 0; action < line.Actions.Count; action++)
                {
                    text.Append('|').Append(line.Actions[action]);
                }

                text.Append('\n');
            }

            return text.ToString();
        }

        /// <summary>Writes a line and hides the label while the line is empty.</summary>
        private static void SetLine(Label label, string text)
        {
            label.text = text;
            label.style.display = text.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>The elements the screen writes to.</summary>
        private sealed class Elements
        {
            /// <summary>The sheet and the row of cards.</summary>
            public VisualElement Panel = null!, Stops = null!;

            /// <summary>The sideways scroll view.</summary>
            public ScrollView Scroll = null!;

            /// <summary>The labels.</summary>
            public Label Title = null!, Summary = null!, Note = null!, AmountNote = null!, Hint = null!;

            /// <summary>The buttons.</summary>
            public Button Slot = null!, Close = null!, Revert = null!, Confirm = null!;

            /// <summary>Finds every element in <paramref name="root"/>; null when one is missing.</summary>
            public static Elements? Find(VisualElement root)
            {
                var found = new Elements
                {
                    Panel = root.Q<VisualElement>(PanelName),
                    Stops = root.Q<VisualElement>(StopsName),
                    Scroll = root.Q<ScrollView>(ScrollName),
                    Title = root.Q<Label>(TitleName),
                    Summary = root.Q<Label>(SummaryName),
                    Note = root.Q<Label>(NoteName),
                    AmountNote = root.Q<Label>(AmountNoteName),
                    Hint = root.Q<Label>(HintName),
                    Slot = root.Q<Button>(SlotName),
                    Close = root.Q<Button>(CloseName),
                    Revert = root.Q<Button>(RevertName),
                    Confirm = root.Q<Button>(ConfirmName),
                };
                bool complete = found.Panel != null && found.Stops != null && found.Scroll != null && found.Title != null && found.Summary != null
                    && found.Note != null && found.AmountNote != null && found.Hint != null && found.Slot != null && found.Close != null
                    && found.Revert != null && found.Confirm != null;
                return complete ? found : null;
            }
        }
    }
}
