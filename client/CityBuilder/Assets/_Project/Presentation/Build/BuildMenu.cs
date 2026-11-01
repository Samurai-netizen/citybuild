#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Presentation.Build
{
    /// <summary>
    /// The build menu on the screen (M0-45): a thin component over a <see cref="BuildMenuModel"/>. It loads <c>UI/BuildMenu</c> and one
    /// <c>UI/BuildCard</c> per building type from <c>Resources</c>, writes the texts of the model's cards into the labels by name, puts the type's picture on each card, and keeps
    /// them current when the model's cards or selection change. A "Build" button opens and closes the sheet of cards. A tap on a card goes to
    /// <see cref="BuildMenuModel.TryToggle"/>; a locked card is disabled and ignores it. Nothing here decides anything or calls the server.
    /// <c>GameRoot</c> creates it once the start has connected, with <see cref="Create"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildMenu : MonoBehaviour
    {
        /// <summary>The menu layout (UXML), loaded by name from <c>Assets/_Project/Resources</c>.</summary>
        private const string LayoutPath = "UI/BuildMenu";

        /// <summary>The layout of one card (UXML), cloned once per building type.</summary>
        private const string CardLayoutPath = "UI/BuildCard";

        /// <summary>The panel settings the UI shares with the status screens.</summary>
        private const string PanelSettingsPath = "UI/PanelSettings";

        /// <summary>The root's padding in panel units before the safe area is added.</summary>
        private const float BasePadding = 16f;

        /// <summary>Drawn above the status screens, which use sort order 0.</summary>
        private const float SortingOrder = 1f;

        /// <summary>The name of the menu's root element in <c>BuildMenu.uxml</c>.</summary>
        public const string RootName = "build-root";

        /// <summary>The name of the sheet that holds the cards.</summary>
        public const string PanelName = "build-panel";

        /// <summary>The name of the scroll view the cards are put in.</summary>
        public const string CardsName = "build-cards";

        /// <summary>The name of the button that opens and closes the sheet.</summary>
        public const string ToggleName = "build-toggle";

        /// <summary>The name of a card's picture element in <c>BuildCard.uxml</c>.</summary>
        public const string CardIconName = "card-icon";

        /// <summary>The name of a card's root element in <c>BuildCard.uxml</c>.</summary>
        public const string CardName = "card";

        /// <summary>The USS class of the sheet while it is closed.</summary>
        private const string HiddenClass = "build-panel--hidden";

        /// <summary>The USS class of the selected card.</summary>
        private const string SelectedClass = "build-card--selected";

        /// <summary>The USS class of a locked card.</summary>
        private const string LockedClass = "build-card--locked";

        /// <summary>The labels of one card, by the names in <c>BuildCard.uxml</c>, in the order of the cards in the model.</summary>
        private sealed class CardElements
        {
            /// <summary>The card's root, the thing the player taps.</summary>
            public VisualElement Root = null!;

            /// <summary>The building's picture; hidden when the type has none.</summary>
            public VisualElement Icon = null!;

            /// <summary>The building's name.</summary>
            public Label Name = null!;

            /// <summary>The cost line.</summary>
            public Label Cost = null!;

            /// <summary>The build time line.</summary>
            public Label Time = null!;

            /// <summary>The workers line; hidden when empty.</summary>
            public Label Workers = null!;

            /// <summary>The recipe line.</summary>
            public Label Recipe = null!;

            /// <summary>The lock line; hidden when the card is open.</summary>
            public Label Lock = null!;

            /// <summary>The hint line; hidden when there is no hint.</summary>
            public Label Hint = null!;
        }

        /// <summary>The cards on the screen, in the model's order; empty until <c>Start</c> has built them.</summary>
        private readonly List<CardElements> _cards = new List<CardElements>();

        /// <summary>The model the screen shows; null before <see cref="Create"/>.</summary>
        private BuildMenuModel? _model;

        /// <summary>The layout of one card; null before <see cref="Create"/>.</summary>
        private VisualTreeAsset? _cardLayout;

        /// <summary>The UI document of the menu; null before <see cref="Create"/> made it.</summary>
        private UIDocument? _document;

        /// <summary>The sheet that is shown and hidden.</summary>
        private VisualElement? _panel;

        /// <summary>The button that opens and closes the sheet.</summary>
        private Button? _toggle;

        /// <summary>Whether the sheet of cards is open.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Raised when the sheet goes from closed to open (never for a call that changes nothing), so the other menus can close (<see cref="Ui.MenuGroup"/>).</summary>
        public event Action? Opened;

        /// <summary>The model the screen shows; null before <see cref="Create"/> succeeded.</summary>
        public BuildMenuModel? Model => _model;

        /// <summary>The root of the menu's element tree; null when the layout was not loaded.</summary>
        public VisualElement? Root => _document == null ? null : _document.rootVisualElement;

        /// <summary>
        /// Makes the menu: a child object of <paramref name="parent"/> with a <see cref="UIDocument"/> of the shared panel settings. The
        /// cards of <paramref name="model"/> are built in <c>Start</c>, before the first frame, when the document's tree exists. Returns null,
        /// after logging one error that names the assets, when one is missing.
        /// </summary>
        /// <param name="parent">The object the menu's object goes under.</param>
        /// <param name="model">The menu's state.</param>
        public static BuildMenu? Create(Transform parent, BuildMenuModel model)
        {
            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsPath);
            var layout = Resources.Load<VisualTreeAsset>(LayoutPath);
            var cardLayout = Resources.Load<VisualTreeAsset>(CardLayoutPath);
            if (panelSettings == null || layout == null || cardLayout == null)
            {
                Debug.LogError(
                    "BuildMenu: cannot show the build menu, an asset is missing under Assets/_Project/Resources: " +
                    $"{PanelSettingsPath} {(panelSettings == null ? "missing" : "found")}, " +
                    $"{LayoutPath} {(layout == null ? "missing" : "found")}, " +
                    $"{CardLayoutPath} {(cardLayout == null ? "missing" : "found")}.");
                return null;
            }

            // A UIDocument builds its tree when it is enabled, so it gets its settings while the object is still inactive.
            var menuObject = new GameObject("Build Menu");
            menuObject.transform.SetParent(parent, false);
            menuObject.SetActive(false);
            var menu = menuObject.AddComponent<BuildMenu>();
            menu._document = menuObject.AddComponent<UIDocument>();
            menu._document.panelSettings = panelSettings;
            menu._document.visualTreeAsset = layout;
            menu._document.sortingOrder = SortingOrder;
            menu._model = model;
            menu._cardLayout = cardLayout;
            menuObject.SetActive(true);
            return menu;
        }

        /// <summary>
        /// A tap on the card of <paramref name="typeId"/>: what the player's finger does, as a call. A locked card ignores it.
        /// </summary>
        /// <returns>True when the selection changed.</returns>
        public bool Tap(BuildingTypeId typeId)
        {
            return _model != null && _model.TryToggle(typeId);
        }

        /// <summary>Opens or closes the sheet of cards. Opening a closed sheet raises <see cref="Opened"/>.</summary>
        public void SetOpen(bool open)
        {
            bool opens = open && !IsOpen;
            IsOpen = open;
            if (_panel != null)
            {
                _panel.EnableInClassList(HiddenClass, !open);
            }

            if (_toggle != null)
            {
                _toggle.text = open ? "Close" : "Build";
            }

            if (opens)
            {
                Opened?.Invoke();
            }
        }

        /// <summary>
        /// Whether a screen position (pixels, origin bottom left) is over the menu or any other element that takes the pointer: the build
        /// button, the open sheet. The ground ignores a press there. False before the UI exists.
        /// </summary>
        public bool IsOverUi(Vector2 screenPosition)
        {
            return UiRoot.IsOver(Root, screenPosition);
        }

        /// <summary>Unity calls it before the first frame: the document's tree exists now, so the cards are built into it.</summary>
        private void Start()
        {
            Show();
        }

        /// <summary>Unity calls it when the object is destroyed: the model no longer reports to a screen that is gone.</summary>
        private void OnDestroy()
        {
            if (_model != null)
            {
                _model.CardsChanged -= RefreshCards;
                _model.SelectionChanged -= RefreshSelection;
            }
        }

        /// <summary>Builds the cards from the model, hooks up the button and the model's events, and fills the texts.</summary>
        private void Show()
        {
            BuildMenuModel? model = _model;
            VisualTreeAsset? cardLayout = _cardLayout;
            VisualElement? root = Root;
            var cards = root?.Q<ScrollView>(CardsName);
            _panel = root?.Q<VisualElement>(PanelName);
            _toggle = root?.Q<Button>(ToggleName);
            if (model == null || cardLayout == null || root == null || cards == null || _panel == null || _toggle == null)
            {
                Debug.LogError($"BuildMenu: the layout lacks '{RootName}', '{PanelName}', '{CardsName}' or '{ToggleName}'.");
                return;
            }

            _toggle.clicked += () => SetOpen(!IsOpen);
            root.RegisterCallback<GeometryChangedEvent>(_ => UiRoot.PadBySafeArea(root, BasePadding));
            for (int index = 0; index < model.Cards.Count; index++)
            {
                BuildingTypeId typeId = model.Cards[index].TypeId;
                CardElements card = MakeCard(cardLayout, typeId);
                cards.Add(card.Root);
                _cards.Add(card);
            }

            model.CardsChanged += RefreshCards;
            model.SelectionChanged += RefreshSelection;
            SetOpen(false);
            RefreshCards();
            RefreshSelection();
        }

        /// <summary>Clones the card layout for <paramref name="typeId"/> and makes a tap on it call <see cref="Tap"/>.</summary>
        private CardElements MakeCard(VisualTreeAsset cardLayout, BuildingTypeId typeId)
        {
            VisualElement clone = cardLayout.Instantiate();
            VisualElement root = clone.Q<VisualElement>(CardName);
            var card = new CardElements
            {
                Root = root,
                Icon = clone.Q<VisualElement>(CardIconName),
                Name = clone.Q<Label>("card-name"),
                Cost = clone.Q<Label>("card-cost"),
                Time = clone.Q<Label>("card-time"),
                Workers = clone.Q<Label>("card-workers"),
                Recipe = clone.Q<Label>("card-recipe"),
                Lock = clone.Q<Label>("card-lock"),
                Hint = clone.Q<Label>("card-hint"),
            };
            // The picture belongs to the type and never changes, so it is set once, here.
            Texture2D? icon = UiIcons.ForBuilding(typeId);
            UiIcons.Show(card.Icon, icon);
            card.Icon.style.display = icon == null ? DisplayStyle.None : DisplayStyle.Flex;
            root.AddManipulator(new Clickable(() => Tap(typeId)));
            return card;
        }

        /// <summary>Writes the model's card texts into the labels and shows or hides the lock and hint lines; a card whose text is the same is not touched twice.</summary>
        private void RefreshCards()
        {
            if (_model == null)
            {
                return;
            }

            for (int index = 0; index < _cards.Count && index < _model.Cards.Count; index++)
            {
                BuildCardModel model = _model.Cards[index];
                CardElements card = _cards[index];
                card.Name.text = model.Name;
                card.Cost.text = model.CostText;
                card.Time.text = model.BuildTimeText;
                SetLine(card.Workers, model.WorkersText);
                SetLine(card.Recipe, model.RecipeText);
                SetLine(card.Lock, model.LockText);
                SetLine(card.Hint, model.HintText);
                card.Root.EnableInClassList(LockedClass, model.IsLocked);
                card.Root.SetEnabled(!model.IsLocked);
            }
        }

        /// <summary>Marks the selected card.</summary>
        private void RefreshSelection()
        {
            if (_model == null)
            {
                return;
            }

            for (int index = 0; index < _cards.Count && index < _model.Cards.Count; index++)
            {
                bool selected = _model.Selected.HasValue && _model.Selected.Value.Equals(_model.Cards[index].TypeId);
                _cards[index].Root.EnableInClassList(SelectedClass, selected);
            }
        }

        /// <summary>Sets a label's text and hides the label when the text is empty, so an empty line takes no room.</summary>
        private static void SetLine(Label label, string text)
        {
            label.text = text;
            label.style.display = text.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
