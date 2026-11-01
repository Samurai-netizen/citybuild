#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.IO;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Hud;
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
    /// The HUD in a running scene (M0-49): given the real content and a city, one chip per item with the model's texts, the counters, the era badge,
    /// the research labels and the connection dot follow the model when it changes, and nothing on the HUD takes the pointer.
    /// </summary>
    public sealed class HudViewTests
    {
        /// <summary>The object the HUD is made under.</summary>
        private GameObject _host = null!;

        /// <summary>The real content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>The HUD under test, with its elements found (after the first frame).</summary>
        private HudView _hud = null!;

        /// <summary>Before each test: loads the content and makes a HUD for a new game's city; waits a frame so <c>Start</c> finds the elements.</summary>
        [UnitySetUp]
        public IEnumerator CreateHud()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Tests/Fixtures/content.json"));
            ContentResponseDto dto = new ApiJson().Deserialize<ContentResponseDto>(json)!;
            GameContentResult built = GameContentFactory.Create(ContentMapper.ToData(dto));
            Assert.That(built.IsValid, Is.True);
            _content = built.Content!;
            _host = new GameObject("Test HUD host");
            HudView? hud = HudView.Create(_host.transform, new HudModel(_content, City(0, 700, null), ConnectionState.Connected));
            Assert.That(hud, Is.Not.Null, "the HUD's assets must be found under Resources");
            _hud = hud!;
            yield return null;
        }

        /// <summary>After each test: destroys the HUD.</summary>
        [TearDown]
        public void DestroyHud()
        {
            Object.DestroyImmediate(_host);
        }

        /// <summary>A city of Era I at <paramref name="clock"/> with a cottage, <paramref name="coins"/>, 40 logs and 10 planks.</summary>
        private static CityState City(long clock, long coins, ActiveResearch? research)
        {
            var stock = new List<KeyValuePair<ItemId, long>>
            {
                new KeyValuePair<ItemId, long>(new ItemId("coins"), coins),
                new KeyValuePair<ItemId, long>(new ItemId("logs"), 40),
                new KeyValuePair<ItemId, long>(new ItemId("planks"), 10),
            };
            var buildings = new List<Building>
            {
                new Building(1, new BuildingTypeId("house"), 0, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0),
            };
            return new CityState(clock, 1, 2, stock, buildings, new List<ResearchId>(), research);
        }

        /// <summary>The chip elements on the screen, in order.</summary>
        private List<VisualElement> Chips() => _hud.Root!.Query<VisualElement>(HudView.ChipName).ToList();

        /// <summary>There is one chip per item, each with its icon letter, stock and (for a capped item) cap and rate.</summary>
        [Test]
        public void Chips_ShowTheItems()
        {
            List<VisualElement> chips = Chips();

            Assert.That(chips.Count, Is.EqualTo(3));
            Assert.That(chips[0].Q<Label>(HudView.ChipIconName).text, Is.EqualTo("C"));
            Assert.That(chips[0].Q<Label>(HudView.ChipStockName).text, Is.EqualTo("700"));
            Assert.That(chips[0].Q<Label>(HudView.ChipCapName).style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(chips[1].Q<Label>(HudView.ChipStockName).text, Is.EqualTo("40"));
            Assert.That(chips[1].Q<Label>(HudView.ChipCapName).text, Is.EqualTo("/ 500"));
            Assert.That(chips[1].Q<Label>(HudView.ChipRateName).text, Is.EqualTo("0/h"));
        }

        /// <summary>The counters, the era badge and the connection indicator carry the model's words.</summary>
        [Test]
        public void Counters_Era_AndConnection_ShowTheModel()
        {
            VisualElement root = _hud.Root!;

            Assert.That(root.Q<Label>(HudView.WorkersName).text, Is.EqualTo("4 / 4"));
            Assert.That(root.Q<Label>(HudView.BuildersName).text, Is.EqualTo("0 / 2"));
            Assert.That(root.Q<Label>(HudView.EraName).text, Is.EqualTo("Early Middle Ages"));
            Assert.That(root.Q<Label>(HudView.ConnectionTextName).text, Is.EqualTo("Online"));
            Assert.That(root.Q<VisualElement>(HudView.ConnectionDotName).ClassListContains(HudView.DotOkClass), Is.True);
            Assert.That(root.Q<Label>(HudView.ResearchNameName).text, Is.EqualTo("No research"));
            Assert.That(root.Q<Label>(HudView.ResearchTimeName).style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        /// <summary>A new city and a lost connection are written into the same elements: new stock, the research name and time, and the problem dot.</summary>
        [Test]
        public void Update_WritesTheNewValues()
        {
            var research = new ActiveResearch(new ResearchId("sharp_axes"), 0, 1800);

            _hud.Model!.Update(City(450, 900, research), ConnectionState.Offline);

            VisualElement root = _hud.Root!;
            Assert.That(Chips()[0].Q<Label>(HudView.ChipStockName).text, Is.EqualTo("900"));
            Assert.That(root.Q<Label>(HudView.ResearchNameName).text, Is.EqualTo("Sharpened Axes"));
            Assert.That(root.Q<Label>(HudView.ResearchTimeName).text, Is.EqualTo("22m 30s"));
            Assert.That(root.Q<Label>(HudView.ResearchTimeName).style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q<Label>(HudView.ConnectionTextName).text, Is.EqualTo("Offline"));
            VisualElement dot = root.Q<VisualElement>(HudView.ConnectionDotName);
            Assert.That(dot.ClassListContains(HudView.DotProblemClass), Is.True);
            Assert.That(dot.ClassListContains(HudView.DotOkClass), Is.False);
        }

        /// <summary>The chips are made once: a second update rewrites their texts in the same elements instead of making new ones.</summary>
        [Test]
        public void Update_KeepsTheSameChipElements()
        {
            VisualElement first = Chips()[0];

            _hud.Model!.Update(City(0, 123, null), ConnectionState.Connected);

            Assert.That(Chips()[0], Is.SameAs(first));
            Assert.That(first.Q<Label>(HudView.ChipStockName).text, Is.EqualTo("123"));
        }

        /// <summary>No element of the shown HUD, chips included, takes the pointer.</summary>
        [Test]
        public void EveryElement_IgnoresThePointer()
        {
            foreach (VisualElement element in _hud.Root!.Query<VisualElement>().ToList())
            {
                if (element is TemplateContainer || element == _hud.Root)
                {
                    continue;
                }

                Assert.That(element.pickingMode, Is.EqualTo(PickingMode.Ignore), element.name + " (" + element.GetType().Name + ")");
            }
        }
    }
}
