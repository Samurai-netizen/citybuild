#nullable enable

using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Tests.EditMode.Core;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>
    /// <see cref="WalkPlanner"/> (CR-0008-T2 part D): the walk between two buildings as the shared engine's path rule makes it, with the engine's
    /// seconds, and a crossing step that weighs more than a ground step.
    /// </summary>
    public sealed class WalkPlannerTests
    {
        /// <summary>The fixture content.</summary>
        private GameContent _content = null!;

        /// <summary>Before each test: the content.</summary>
        [SetUp]
        public void LoadContent()
        {
            _content = CityTestData.Content();
        }

        /// <summary>Three ground steps take 15 s (5 s a tile) and make a straight path of four tiles.</summary>
        [Test]
        public void Path_OnOpenGround_IsStraightAndTakesTheEnginesSeconds()
        {
            Building from = House(1, 0, 0);
            Building to = House(2, 3, 0);
            WalkPlanner planner = Planner(from, to);

            WalkPath path = planner.Path(from, to);

            Assert.That(path.TileCount, Is.EqualTo(4));
            Assert.That(planner.Seconds(from, to), Is.EqualTo(15));
        }

        /// <summary>A building in the way is walked around when a way around exists (the path rule): two more steps, 25 s.</summary>
        [Test]
        public void Path_WithABuildingInTheWay_GoesAround()
        {
            Building from = House(1, 0, 0);
            Building between = House(3, 1, 0);
            Building to = House(2, 3, 0);
            WalkPlanner planner = Planner(from, between, to);

            WalkPath path = planner.Path(from, to);

            Assert.That(path.StepCount, Is.EqualTo(5));
            Assert.That(planner.Seconds(from, to), Is.EqualTo(25));
        }

        /// <summary>Planning before the map was read is a mistake and says so.</summary>
        [Test]
        public void Path_BeforeLoad_Throws()
        {
            var planner = new WalkPlanner(_content);

            Assert.Throws<System.InvalidOperationException>(() => planner.Path(House(1, 0, 0), House(2, 3, 0)));
        }

        /// <summary>A planner loaded with a city of the given buildings.</summary>
        private WalkPlanner Planner(params Building[] buildings)
        {
            var planner = new WalkPlanner(_content);
            planner.Load(new CityState(0, 1, 100, 0, buildings, new ResearchId[0], null));
            return planner;
        }

        /// <summary>A finished cottage.</summary>
        private static Building House(int id, int x, int y)
        {
            return new Building(id, new BuildingTypeId("house"), x, y, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
        }
    }
}
