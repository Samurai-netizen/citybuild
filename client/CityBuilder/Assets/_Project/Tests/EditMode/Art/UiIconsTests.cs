#nullable enable

using System.Collections.Generic;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Client.Tests.EditMode.Net;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.EditMode.Art
{
    /// <summary>
    /// The UI icons (CR-0005-T4): every item, building type and research node of the content has a picture a player build finds by name, so a
    /// content id added without an icon fails here instead of showing an empty square, and the three fixed icons exist.
    /// </summary>
    public sealed class UiIconsTests
    {
        /// <summary>The content of a real <c>GET /content</c> answer (<c>Tests/Fixtures/content.json</c>), through the factory the client uses.</summary>
        private static GameContent LoadContent()
        {
            var dto = new ApiJson().Deserialize<ContentResponseDto>(Fixtures.Read("content.json"))!;
            GameContentResult result = GameContentFactory.Create(ContentMapper.ToData(dto));
            Assert.That(result.IsValid, Is.True, "the fixture is valid content");
            return result.Content!;
        }

        /// <summary>The items of the content, one test case each.</summary>
        private static IEnumerable<string> ItemIds()
        {
            foreach (ItemDefinition item in LoadContent().Items)
            {
                yield return item.Id.Value;
            }
        }

        /// <summary>The building types of the content, one test case each.</summary>
        private static IEnumerable<string> BuildingTypeIds()
        {
            foreach (BuildingDefinition building in LoadContent().Buildings)
            {
                yield return building.Id.Value;
            }
        }

        /// <summary>The research nodes of the content, one test case each.</summary>
        private static IEnumerable<string> ResearchIds()
        {
            foreach (ResearchDefinition research in LoadContent().Research)
            {
                yield return research.Id.Value;
            }
        }

        /// <summary>Every item has an icon.</summary>
        [TestCaseSource(nameof(ItemIds))]
        public void EveryItem_HasAnIcon(string id)
        {
            Assert.That(UiIcons.ForItem(new ItemId(id)), Is.Not.Null, id);
        }

        /// <summary>Every building type has an icon.</summary>
        [TestCaseSource(nameof(BuildingTypeIds))]
        public void EveryBuildingType_HasAnIcon(string id)
        {
            Assert.That(UiIcons.ForBuilding(new BuildingTypeId(id)), Is.Not.Null, id);
        }

        /// <summary>Every research node has an icon, for the research screen (M0-52).</summary>
        [TestCaseSource(nameof(ResearchIds))]
        public void EveryResearchNode_HasAnIcon(string id)
        {
            Assert.That(UiIcons.ForResearch(new ResearchId(id)), Is.Not.Null, id);
        }

        /// <summary>The icons of workers, builders and research exist.</summary>
        [Test]
        public void TheFixedIcons_Exist()
        {
            Assert.That(UiIcons.Workers, Is.Not.Null);
            Assert.That(UiIcons.Builders, Is.Not.Null);
            Assert.That(UiIcons.Research, Is.Not.Null);
        }

        /// <summary>A content id with no file gives null, not an exception, and asking again gives null again.</summary>
        [Test]
        public void UnknownId_GivesNull()
        {
            Assert.That(UiIcons.ForItem(new ItemId("no_such_item")), Is.Null);
            Assert.That(UiIcons.ForItem(new ItemId("no_such_item")), Is.Null);
            Assert.That(UiIcons.ForBuilding(new BuildingTypeId("no_such_type")), Is.Null);
            Assert.That(UiIcons.ForResearch(new ResearchId("no_such_node")), Is.Null);
        }

        /// <summary>The same icon comes back each time (one loaded picture, no copies).</summary>
        [Test]
        public void SameId_GivesTheSameTexture()
        {
            Assert.That(UiIcons.ForItem(new ItemId("coins")), Is.SameAs(UiIcons.ForItem(new ItemId("coins"))));
        }

        /// <summary>Show puts the picture on an element as its background and takes it away again for null.</summary>
        [Test]
        public void Show_SetsAndClearsTheBackground()
        {
            var element = new VisualElement();
            Texture2D? coins = UiIcons.ForItem(new ItemId("coins"));

            UiIcons.Show(element, coins);
            Assert.That(element.style.backgroundImage.value.texture, Is.SameAs(coins));

            UiIcons.Show(element, null);
            Assert.That(element.style.backgroundImage.keyword, Is.EqualTo(StyleKeyword.Null));
        }
    }
}
