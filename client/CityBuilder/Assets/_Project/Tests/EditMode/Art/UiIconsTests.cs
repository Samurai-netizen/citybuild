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
    /// content id added without an icon fails here instead of showing an empty square, and the fixed icons exist. An id on the pending list
    /// (<see cref="PendingAssets.Icons"/>, M1-02) shows its code-made initials tile instead (<c>27</c> → Placeholders), and only such an id may.
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

        /// <summary>Every item has its picture, or its initials tile while it is pending.</summary>
        [TestCaseSource(nameof(ItemIds))]
        public void EveryItem_HasAnIcon(string id)
        {
            AssertPictureOrPending(UiIcons.ForItem(new ItemId(id)), UiIcons.ItemPrefix + id);
        }

        /// <summary>Every building type has its picture, or its initials tile while it is pending.</summary>
        [TestCaseSource(nameof(BuildingTypeIds))]
        public void EveryBuildingType_HasAnIcon(string id)
        {
            AssertPictureOrPending(UiIcons.ForBuilding(new BuildingTypeId(id)), UiIcons.BuildingPrefix + id);
        }

        /// <summary>Every research node has its picture for the research screen (M0-52), or its initials tile while it is pending.</summary>
        [TestCaseSource(nameof(ResearchIds))]
        public void EveryResearchNode_HasAnIcon(string id)
        {
            AssertPictureOrPending(UiIcons.ForResearch(new ResearchId(id)), UiIcons.ResearchPrefix + id);
        }

        /// <summary>The pending list names only icons of the content's ids, each once (the tests above fail on one whose file has arrived).</summary>
        [Test]
        public void ThePendingIcons_AreIconsOfTheContent()
        {
            var names = new HashSet<string>();
            foreach (string id in ItemIds())
            {
                names.Add(UiIcons.ItemPrefix + id);
            }

            foreach (string id in BuildingTypeIds())
            {
                names.Add(UiIcons.BuildingPrefix + id);
            }

            foreach (string id in ResearchIds())
            {
                names.Add(UiIcons.ResearchPrefix + id);
            }

            Assert.That(PendingAssets.Icons, Is.Unique);
            foreach (string name in PendingAssets.Icons)
            {
                Assert.That(names.Contains(name), Is.True, name);
            }
        }

        /// <summary>An icon is the file's picture unless its name is pending; a pending one is the initials tile, never a file.</summary>
        /// <param name="icon">The icon found.</param>
        /// <param name="name">The icon's file name without the extension.</param>
        private static void AssertPictureOrPending(Texture2D icon, string name)
        {
            Assert.That(icon, Is.Not.Null, name);
            bool pending = PendingAssets.Contains(PendingAssets.Icons, name);
            Assert.That(UiIcons.IsPlaceholder(icon), Is.EqualTo(pending), pending ? name + " has arrived: take it off the pending list" : name + " has no picture");
        }

        /// <summary>The icons of workers, builders, couriers and research exist.</summary>
        [Test]
        public void TheFixedIcons_Exist()
        {
            Assert.That(UiIcons.Workers, Is.Not.Null);
            Assert.That(UiIcons.Builders, Is.Not.Null);
            Assert.That(UiIcons.Research, Is.Not.Null);
            Assert.That(UiIcons.Couriers, Is.Not.Null);
        }

        /// <summary>
        /// A content id with no file gives its initials tile made in code (<c>27</c> → Placeholders), not null or an exception; asking again
        /// gives the same tile. M1-02 changed this test: before, a missing file gave null and the view kept its text.
        /// </summary>
        [Test]
        public void UnknownId_GivesItsInitialsTile()
        {
            Texture2D item = UiIcons.ForItem(new ItemId("no_such_item"));

            Assert.That(UiIcons.IsPlaceholder(item), Is.True);
            Assert.That(item.name, Does.Contain("no_such_item"));
            Assert.That(UiIcons.ForItem(new ItemId("no_such_item")), Is.SameAs(item));
            Assert.That(UiIcons.IsPlaceholder(UiIcons.ForBuilding(new BuildingTypeId("no_such_type"))), Is.True);
            Assert.That(UiIcons.IsPlaceholder(UiIcons.ForResearch(new ResearchId("no_such_node"))), Is.True);
            Assert.That(UiIcons.IsPlaceholder(UiIcons.ForItem(new ItemId("coins"))), Is.False, "a picture that exists is the file");
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
