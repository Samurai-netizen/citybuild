#nullable enable

using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Presentation.Roads;
using CityBuilder.Client.Tests.EditMode.Presentation.Build;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Roads
{
    /// <summary>
    /// The road tool on the Era I map (M1-03): it refuses in the order of <see cref="RoadRules.PlaceRoads"/> (forest or rock, then closed land,
    /// then water before bridges are researched), prices a water tile at the bridge's cost, and treats a bridge as a road of one level.
    /// </summary>
    public sealed class RoadFlowMapTests
    {
        /// <summary>The real content with its map.</summary>
        private GameContent _content = null!;

        /// <summary>The model under test, over the map's grid.</summary>
        private RoadFlowModel _model = null!;

        /// <summary>Before each test: the road tool in build mode over the start city.</summary>
        [SetUp]
        public void CreateModel()
        {
            _content = BuildMenuTestData.Content();
            _model = new RoadFlowModel(_content, new CityGrid(_content.Rules.GridWidth, _content.Rules.GridHeight));
            _model.Update(BuildMenuTestData.StartCity());
            _model.StartBuildMode();
        }

        /// <summary>A road on forest or rock is refused, naming the tile, before anything else the map checks.</summary>
        [Test]
        public void Place_OnForest_IsRefused()
        {
            _model.SelectLine(new GridCoordinate(2, 3), new GridCoordinate(4, 3));

            Assert.That(_model.CanPlace, Is.False);
            Assert.That(_model.HintText, Is.EqualTo(RoadFlowModel.TerrainBlockedReason + " at (2, 3)"));
        }

        /// <summary>A road on land under mist is refused, naming the tile.</summary>
        [Test]
        public void Place_OnClosedLand_IsRefused()
        {
            _model.SelectLine(new GridCoordinate(15, 4), new GridCoordinate(15, 4));

            Assert.That(_model.CanPlace, Is.False);
            Assert.That(_model.HintText, Is.EqualTo(RoadFlowModel.LandClosedReason + " at (15, 4)"));
        }

        /// <summary>Before Bridge Building, a road across the river is refused with the server's words for it.</summary>
        [Test]
        public void Place_OnWaterWithoutTheResearch_IsRefused()
        {
            _model.SelectLine(new GridCoordinate(11, 5), new GridCoordinate(12, 5));

            Assert.That(_model.CanPlace, Is.False);
            Assert.That(_model.HintText, Is.EqualTo("Research Bridge Building to build on water"));
        }

        /// <summary>After Bridge Building, a line onto the river costs one road tile and one bridge tile, and may be placed.</summary>
        [Test]
        public void Place_OnWaterAfterTheResearch_CostsTheBridge()
        {
            _model.Update(BuildMenuTestData.City(800, 0, 0, new[] { "bridge_building" }, 0));

            _model.SelectLine(new GridCoordinate(11, 5), new GridCoordinate(12, 5));

            Assert.That(_model.TotalCoins, Is.EqualTo(5 + 60));
            Assert.That((_model.CanPlace, _model.HintText), Is.EqualTo((true, string.Empty)));
        }

        /// <summary>A bridge has one level: inspecting it names the bridge, and Upgrade is off with the highest-level reason.</summary>
        [Test]
        public void Inspect_ABridge_NamesItAndCannotBeUpgraded()
        {
            CityState city = BuildMenuTestData.City(800, 0, 0, new[] { "bridge_building" }, 0);
            Assert.That(RoadRules.PlaceRoads(city, _content, new[] { (11, 5), (12, 5) }).IsOk, Is.True);
            _model.Update(city);
            _model.Cancel();

            Assert.That(_model.InspectRoadAt(new GridCoordinate(12, 5)), Is.True);

            Assert.That(_model.PromptText, Does.StartWith("Bridge · Level 1"));
            Assert.That(_model.CanUpgrade, Is.False);
            Assert.That(_model.HintText, Is.EqualTo(RoadFlowModel.MaxLevelReason));
        }
    }
}
