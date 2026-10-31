#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.IO;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Build;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Tests.PlayMode
{
    /// <summary>
    /// The build menu in a running scene (M0-45): given the real content and a city, there is one card per building type with the model's
    /// texts, a locked card shows its lock and ignores a tap, the cards follow a new city, and a tap on an open card marks it selected.
    /// </summary>
    public sealed class BuildMenuTests
    {
        /// <summary>The object the menu is made under.</summary>
        private GameObject _host = null!;

        /// <summary>The real content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>The menu under test, with its cards built (after the first frame).</summary>
        private BuildMenu _menu = null!;

        /// <summary>Before each test: loads the content and makes a menu for a new game's city; waits a frame so <c>Start</c> builds the cards.</summary>
        [UnitySetUp]
        public IEnumerator CreateMenu()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Tests/Fixtures/content.json"));
            ContentResponseDto dto = new ApiJson().Deserialize<ContentResponseDto>(json)!;
            GameContentResult built = GameContentFactory.Create(ContentMapper.ToData(dto));
            Assert.That(built.IsValid, Is.True);
            _content = built.Content!;
            _host = new GameObject("Test menu host");
            BuildMenu? menu = BuildMenu.Create(_host.transform, new BuildMenuModel(_content, City(700, 40, 10, new string[0], 0)));
            Assert.That(menu, Is.Not.Null, "the menu's assets must be found under Resources");
            _menu = menu!;
            yield return null;
        }

        /// <summary>After each test: destroys the menu.</summary>
        [TearDown]
        public void DestroyMenu()
        {
            Object.DestroyImmediate(_host);
        }

        /// <summary>A city of Era I with the given stock, completed research and buildings under construction (each takes a builder).</summary>
        private static CityState City(long coins, long logs, long planks, string[] research, int constructing)
        {
            var stock = new List<KeyValuePair<ItemId, long>>
            {
                new KeyValuePair<ItemId, long>(new ItemId("coins"), coins),
                new KeyValuePair<ItemId, long>(new ItemId("logs"), logs),
                new KeyValuePair<ItemId, long>(new ItemId("planks"), planks),
            };
            var buildings = new List<Building>();
            for (int id = 1; id <= constructing; id++)
            {
                buildings.Add(new Building(id, new BuildingTypeId("house"), id, 0, 1, 0, BuildingPhase.Constructing, 0, 100, 0, 0, 0, 0));
            }

            var completed = new List<ResearchId>();
            foreach (string id in research)
            {
                completed.Add(new ResearchId(id));
            }

            return new CityState(0, 1, constructing + 1, stock, buildings, completed, null);
        }

        /// <summary>The element tree's card of the building type at <paramref name="index"/> of the content.</summary>
        private VisualElement CardAt(int index)
        {
            return _menu.Root!.Query<VisualElement>(BuildMenu.CardName).ToList()[index];
        }

        /// <summary>The label called <paramref name="name"/> under the card at <paramref name="index"/>.</summary>
        private Label LabelAt(int index, string name)
        {
            return CardAt(index).Q<Label>(name);
        }

        /// <summary>The index of the building type in the content, which is also the card's place on the screen.</summary>
        private int IndexOf(string typeId)
        {
            for (int index = 0; index < _content.Buildings.Count; index++)
            {
                if (_content.Buildings[index].Id.Value == typeId)
                {
                    return index;
                }
            }

            throw new AssertionException("No building type " + typeId);
        }

        /// <summary>There is one card per building type of the content, with its name and cost from the model.</summary>
        [Test]
        public void Menu_HasOneCardPerBuildingType()
        {
            Assert.That(_menu.Root!.Query<VisualElement>(BuildMenu.CardName).ToList().Count, Is.EqualTo(_content.Buildings.Count));
            for (int index = 0; index < _content.Buildings.Count; index++)
            {
                Assert.That(LabelAt(index, "card-name").text, Is.EqualTo(_content.Buildings[index].Name));
                Assert.That(LabelAt(index, "card-cost").text, Is.EqualTo(_menu.Model!.Cards[index].CostText));
            }
        }

        /// <summary>A producer's card shows its recipe with the rate and its workers; an open card shows no lock line.</summary>
        [Test]
        public void SawmillCard_ShowsTheRecipeAndNoLock()
        {
            int sawmill = IndexOf("sawmill");

            Assert.That(LabelAt(sawmill, "card-recipe").text, Is.EqualTo("2 Logs → 1 Planks · 60/h at 3 workers"));
            Assert.That(LabelAt(sawmill, "card-workers").text, Is.EqualTo("Max workers: 3"));
            Assert.That(LabelAt(sawmill, "card-lock").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(CardAt(sawmill).enabledSelf, Is.True);
        }

        /// <summary>The trading post's card says what it requires, is disabled, and a tap on it selects nothing.</summary>
        [Test]
        public void LockedCard_ShowsItsLock_AndIgnoresATap()
        {
            int post = IndexOf("trading_post");

            Assert.That(LabelAt(post, "card-lock").text, Is.EqualTo("Requires: Trade Charter"));
            Assert.That(LabelAt(post, "card-lock").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(CardAt(post).enabledSelf, Is.False);
            Assert.That(CardAt(post).ClassListContains("build-card--locked"), Is.True);
            Assert.That(_menu.Tap(new BuildingTypeId("trading_post")), Is.False);
            Assert.That(_menu.Model!.Selected, Is.Null);
            Assert.That(CardAt(post).ClassListContains("build-card--selected"), Is.False);
        }

        /// <summary>A tap on an open card selects it and marks it; a second tap clears the mark.</summary>
        [Test]
        public void Tap_OnAnOpenCard_MarksItSelected_AndTheSecondTapClearsIt()
        {
            int sawmill = IndexOf("sawmill");

            Assert.That(_menu.Tap(new BuildingTypeId("sawmill")), Is.True);
            Assert.That(_menu.Model!.Selected, Is.EqualTo(new BuildingTypeId("sawmill")));
            Assert.That(CardAt(sawmill).ClassListContains("build-card--selected"), Is.True);
            Assert.That(CardAt(IndexOf("house")).ClassListContains("build-card--selected"), Is.False);

            _menu.Tap(new BuildingTypeId("sawmill"));
            Assert.That(CardAt(sawmill).ClassListContains("build-card--selected"), Is.False);
        }

        /// <summary>A new city updates the cards: a finished research opens the lock; spent coins show a hint that does not block selecting.</summary>
        [Test]
        public void Cards_FollowANewCity()
        {
            int post = IndexOf("trading_post");
            int sawmill = IndexOf("sawmill");
            Assert.That(LabelAt(sawmill, "card-hint").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));

            _menu.Model!.Update(City(100, 40, 10, new[] { "trade_charter" }, 0));

            Assert.That(LabelAt(post, "card-lock").style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(CardAt(post).enabledSelf, Is.True);
            Assert.That(LabelAt(sawmill, "card-hint").text, Is.EqualTo("Not enough Coins"));
            Assert.That(LabelAt(sawmill, "card-hint").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_menu.Tap(new BuildingTypeId("sawmill")), Is.True, "a hint never blocks the selection");
        }

        /// <summary>The "Build" button's sheet starts closed; opening and closing it follows <see cref="BuildMenu.SetOpen"/>.</summary>
        [Test]
        public void Sheet_StartsClosed_AndSetOpenShowsIt()
        {
            VisualElement panel = _menu.Root!.Q<VisualElement>(BuildMenu.PanelName);
            Button toggle = _menu.Root.Q<Button>(BuildMenu.ToggleName);
            Assert.That(_menu.IsOpen, Is.False);
            Assert.That(panel.ClassListContains("build-panel--hidden"), Is.True);
            Assert.That(toggle.text, Is.EqualTo("Build"));

            _menu.SetOpen(true);
            Assert.That(panel.ClassListContains("build-panel--hidden"), Is.False);
            Assert.That(toggle.text, Is.EqualTo("Close"));

            _menu.SetOpen(false);
            Assert.That(panel.ClassListContains("build-panel--hidden"), Is.True);
        }

        /// <summary>
        /// The menu answers "over the UI" for the screen position of its button and "not over the UI" for an empty top edge. The button's place is
        /// read from the laid-out tree, so the test needs a frame. It does not prove the y flip alone (another document in the shared panel,
        /// such as the status label of a <c>GameRoot</c> that another test left, can cover the unflipped spot); <c>ScreenPointTests</c> pins that.
        /// </summary>
        [UnityTest]
        public IEnumerator IsOverUi_IsTrueOnTheButton_AndFalseInTheEmptyTopCorner()
        {
            yield return null;
            VisualElement root = _menu.Root!;
            Rect button = root.Q<Button>(BuildMenu.ToggleName).worldBound;
            Assume.That(float.IsNaN(button.center.x), Is.False, "the panel has not been laid out");
            float scaleX = Screen.width / root.worldBound.width;
            float scaleY = Screen.height / root.worldBound.height;

            var onButton = new Vector2(button.center.x * scaleX, Screen.height - button.center.y * scaleY);
            var topCorner = new Vector2(Screen.width * 0.5f, Screen.height - 2f);

            Assert.That(_menu.IsOverUi(onButton), Is.True, "the build button takes the pointer");
            Assert.That(_menu.IsOverUi(topCorner), Is.False, "the empty space passes the pointer to the ground");
        }

        /// <summary>Destroying the menu takes its listeners off the model, so a later update does not touch destroyed elements.</summary>
        [Test]
        public void Destroyed_Menu_NoLongerListensToTheModel()
        {
            BuildMenuModel model = _menu.Model!;
            Object.DestroyImmediate(_menu.gameObject);

            Assert.DoesNotThrow(() => model.Update(City(0, 0, 0, new string[0], 2)));
        }
    }
}
