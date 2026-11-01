#nullable enable

using System.Collections.Generic;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Tests.EditMode.Net;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Art
{
    /// <summary>
    /// The models the client draws (CR-0005-T3): every building type of the content has a model for each level it can reach, the construction
    /// site and the ground tiles exist, and each building model carries the anchors the views look for. The models are found the way a player
    /// build finds them, by name from <c>Resources</c>, so a model outside that folder fails here instead of missing from a build.
    /// </summary>
    public sealed class ArtModelsTests
    {
        /// <summary>The content of a real <c>GET /content</c> answer (<c>Tests/Fixtures/content.json</c>), through the factory the client uses.</summary>
        private static GameContent LoadContent()
        {
            var dto = new ApiJson().Deserialize<ContentResponseDto>(Fixtures.Read("content.json"))!;
            GameContentResult result = GameContentFactory.Create(ContentMapper.ToData(dto));
            Assert.That(result.IsValid, Is.True, "the fixture is valid content");
            return result.Content!;
        }

        /// <summary>The building types of the content, one test case each.</summary>
        private static IEnumerable<string> BuildingTypeIds()
        {
            foreach (BuildingDefinition building in LoadContent().Buildings)
            {
                yield return building.Id.Value;
            }
        }

        /// <summary>Every level a building type can reach has a model in the type's own era.</summary>
        [TestCaseSource(nameof(BuildingTypeIds))]
        public void EveryReachableLevel_HasAModel_InTheTypesEra(string typeId)
        {
            Assert.That(LoadContent().TryGetBuilding(new BuildingTypeId(typeId), out BuildingDefinition? building), Is.True);

            for (int level = 1; level <= building!.MaxLevel; level++)
            {
                GameObject? model = Resources.Load<GameObject>(BuildingLooks.ModelPath(typeId, building.Era, level));
                Assert.That(model, Is.Not.Null, typeId + " level " + level + " (" + BuildingLooks.ModelPath(typeId, building.Era, level) + ")");
            }
        }

        /// <summary>Every building model has the anchors for the status bubble and for the door.</summary>
        [TestCaseSource(nameof(BuildingTypeIds))]
        public void EveryBuildingModel_HasTheStatusAndDoorAnchors(string typeId)
        {
            Assert.That(LoadContent().TryGetBuilding(new BuildingTypeId(typeId), out BuildingDefinition? building), Is.True);

            for (int level = 1; level <= building!.MaxLevel; level++)
            {
                GameObject model = BuildingLooks.FindModel(typeId, building.Era, level)!;
                Assert.That(BuildingViewFactory.FindByName(model.transform, BuildingView.StatusAnchorName), Is.Not.Null, typeId + " level " + level);
                Assert.That(BuildingViewFactory.FindByName(model.transform, BuildingView.DoorAnchorName), Is.Not.Null, typeId + " level " + level);
            }
        }

        /// <summary>The Sawmill, which the view will turn the wheel of, has a <c>Wheel</c> at both levels.</summary>
        [TestCase(1)]
        [TestCase(2)]
        public void TheSawmill_HasAWheel(int level)
        {
            GameObject model = BuildingLooks.FindModel(BuildingLooks.Sawmill, 1, level)!;

            Assert.That(BuildingViewFactory.FindByName(model.transform, BuildingView.WheelName), Is.Not.Null);
        }

        /// <summary>The construction site exists for Era I and has the status and door anchors, like a finished building.</summary>
        [Test]
        public void TheConstructionSite_ExistsForEraI_WithTheAnchors()
        {
            GameObject? site = BuildingLooks.FindConstructionSite(1);

            Assert.That(site, Is.Not.Null);
            Assert.That(BuildingViewFactory.FindByName(site!.transform, BuildingView.StatusAnchorName), Is.Not.Null);
            Assert.That(BuildingViewFactory.FindByName(site.transform, BuildingView.DoorAnchorName), Is.Not.Null);
        }

        /// <summary>The ground tiles exist.</summary>
        [Test]
        public void TheGroundTiles_Exist()
        {
            Assert.That(Resources.Load<GameObject>(BuildingLooks.GrassTilePath), Is.Not.Null, "grass");
            Assert.That(Resources.Load<GameObject>(BuildingLooks.DirtTilePath), Is.Not.Null, "dirt");
        }

        /// <summary>A later era with no models of its own finds the Era I ones; a type or a level without a model finds none.</summary>
        [Test]
        public void FindModel_ForALaterEra_KeepsTheEarlierModel_AndForAMissingOneIsNull()
        {
            Assert.That(BuildingLooks.FindModel(BuildingLooks.House, 3, 1), Is.SameAs(BuildingLooks.FindModel(BuildingLooks.House, 1, 1)));
            Assert.That(BuildingLooks.FindConstructionSite(3), Is.SameAs(BuildingLooks.FindConstructionSite(1)));
            Assert.That(BuildingLooks.FindModel("castle", 1, 1), Is.Null);
            Assert.That(BuildingLooks.FindModel(BuildingLooks.House, 1, 9), Is.Null);
            Assert.That(BuildingLooks.FindModel(BuildingLooks.House, 0, 1), Is.Null);
        }

        /// <summary>The names of the model paths follow the art's rule <c>Models/era&lt;N&gt;/&lt;typeId&gt;_l&lt;level&gt;</c>.</summary>
        [Test]
        public void ModelPath_FollowsTheArtsNamingRule()
        {
            Assert.That(BuildingLooks.ModelPath("trading_post", 1, 2), Is.EqualTo("Models/era1/trading_post_l2"));
            Assert.That(BuildingLooks.ConstructionSitePath(1), Is.EqualTo("Models/era1/construction_site"));
        }

        /// <summary>A building under construction or upgrade is drawn as the site; a finished or producing one is not.</summary>
        [TestCase(CityBuilder.Simulation.State.BuildingPhase.Constructing, true)]
        [TestCase(CityBuilder.Simulation.State.BuildingPhase.Upgrading, true)]
        [TestCase(CityBuilder.Simulation.State.BuildingPhase.Idle, false)]
        [TestCase(CityBuilder.Simulation.State.BuildingPhase.Producing, false)]
        public void ShowsConstructionSite_IsTrueOnlyWhileBuildingOrUpgrading(CityBuilder.Simulation.State.BuildingPhase phase, bool expected)
        {
            Assert.That(BuildingLooks.ShowsConstructionSite(phase), Is.EqualTo(expected));
        }
    }
}
