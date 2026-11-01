#nullable enable

using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Tests.EditMode.Presentation.Hud;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>What a building's status shows (M0-50): the ring by phase and clock, the bubble by stall reason, the stalled look and the pips.</summary>
    public sealed class BuildingStatusModelTests
    {
        /// <summary>The real content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>Before each test: the content.</summary>
        [SetUp]
        public void LoadContent()
        {
            _content = HudTestData.Content();
        }

        /// <summary>Describes <paramref name="building"/> in a city at <paramref name="clock"/> with the given logs and planks.</summary>
        private BuildingStatus Describe(Building building, long clock = 0, long logs = 40, long planks = 10)
        {
            CityState city = HudTestData.City(clock, 700, logs, planks, new[] { building });
            return BuildingStatusModel.Describe(city, _content, building);
        }

        /// <summary>An idle building of <paramref name="typeId"/> with <paramref name="workers"/> workers.</summary>
        private static Building Idle(string typeId, int workers)
        {
            return new Building(1, new BuildingTypeId(typeId), 0, 0, 1, workers, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
        }

        /// <summary>A building being built from second 0 to second 100 has a constructing ring that follows the clock.</summary>
        [Test]
        public void Describe_Constructing_HasARingThatFollowsTheClock()
        {
            BuildingStatus status = Describe(HudTestData.Constructing(1, 0), clock: 25);

            Assert.That(status.Ring, Is.EqualTo(StatusRingKind.Constructing));
            Assert.That(status.ProgressPermille, Is.EqualTo(250));
            Assert.That(status.Bubble, Is.EqualTo(StatusBubbleKind.None));
            Assert.That(status.Stalled, Is.False);
            Assert.That(status.Pips, Is.EqualTo(0));
        }

        /// <summary>An upgrade has its own ring and keeps the workers as pips.</summary>
        [Test]
        public void Describe_Upgrading_HasAnUpgradeRingAndKeepsThePips()
        {
            var building = new Building(1, new BuildingTypeId("woodcutter"), 0, 0, 1, 2, BuildingPhase.Upgrading, 0, 200, 2, 0, 0, 0);

            BuildingStatus status = Describe(building, clock: 100);

            Assert.That(status.Ring, Is.EqualTo(StatusRingKind.Upgrading));
            Assert.That(status.ProgressPermille, Is.EqualTo(500));
            Assert.That(status.Bubble, Is.EqualTo(StatusBubbleKind.None));
            Assert.That(status.Pips, Is.EqualTo(2));
        }

        /// <summary>A cycle of 60 worker-seconds with two workers takes 30 s, so it is half done at second 15.</summary>
        [Test]
        public void Describe_Producing_ShowsTheCycleProgressAndOnePipPerWorker()
        {
            BuildingStatus status = Describe(HudTestData.Producer(1, 0, "woodcutter", 2, 60), clock: 15);

            Assert.That(status.Ring, Is.EqualTo(StatusRingKind.Producing));
            Assert.That(status.ProgressPermille, Is.EqualTo(500));
            Assert.That(status.Stalled, Is.False);
            Assert.That(status.Pips, Is.EqualTo(2));
        }

        /// <summary>A cycle with its workers taken away is frozen: the ring stays where it was and a no-workers bubble says why.</summary>
        [Test]
        public void Describe_FrozenCycle_KeepsTheRingAndShowsNoWorkers()
        {
            var frozen = new Building(1, new BuildingTypeId("woodcutter"), 0, 0, 1, 0, BuildingPhase.Producing, 0, null, 0, 60, 30, 10);

            BuildingStatus status = Describe(frozen, clock: 500);

            Assert.That(status.Ring, Is.EqualTo(StatusRingKind.Producing));
            Assert.That(status.ProgressPermille, Is.EqualTo(500));
            Assert.That(status.Bubble, Is.EqualTo(StatusBubbleKind.NoWorkers));
            Assert.That(status.Stalled, Is.True);
            Assert.That(status.Pips, Is.EqualTo(0));
        }

        /// <summary>An idle producer without workers has a bubble and no ring.</summary>
        [Test]
        public void Describe_IdleWithoutWorkers_ShowsNoWorkers()
        {
            BuildingStatus status = Describe(Idle("woodcutter", 0));

            Assert.That(status.Ring, Is.EqualTo(StatusRingKind.None));
            Assert.That(status.ProgressPermille, Is.EqualTo(0));
            Assert.That(status.Bubble, Is.EqualTo(StatusBubbleKind.NoWorkers));
            Assert.That(status.BubbleItem, Is.EqualTo(default(ItemId)));
        }

        /// <summary>A sawmill with workers and no logs waits for logs, and the bubble names them.</summary>
        [Test]
        public void Describe_IdleWithoutInput_ShowsMissingInputAndTheItem()
        {
            BuildingStatus status = Describe(Idle("sawmill", 3), logs: 0);

            Assert.That(status.Bubble, Is.EqualTo(StatusBubbleKind.MissingInput));
            Assert.That(status.BubbleItem, Is.EqualTo(new ItemId("logs")));
            Assert.That(status.Stalled, Is.True);
            Assert.That(status.Pips, Is.EqualTo(3));
        }

        /// <summary>A woodcutter with workers whose logs are at the cap waits for room, and the bubble names the logs.</summary>
        [Test]
        public void Describe_IdleWithFullOutput_ShowsStorageFullAndTheItem()
        {
            BuildingStatus status = Describe(Idle("woodcutter", 2), logs: 500);

            Assert.That(status.Bubble, Is.EqualTo(StatusBubbleKind.StorageFull));
            Assert.That(status.BubbleItem, Is.EqualTo(new ItemId("logs")));
            Assert.That(status.Stalled, Is.True);
        }

        /// <summary>An idle producer that can start its next cycle shows nothing: it is not stalled.</summary>
        [Test]
        public void Describe_IdleAndReady_ShowsNothing()
        {
            BuildingStatus status = Describe(Idle("woodcutter", 2));

            Assert.That(status.Ring, Is.EqualTo(StatusRingKind.None));
            Assert.That(status.Bubble, Is.EqualTo(StatusBubbleKind.None));
            Assert.That(status.Stalled, Is.False);
        }

        /// <summary>A cottage has no recipe and no workers; it is never shown as stalled.</summary>
        [Test]
        public void Describe_Cottage_ShowsNothing()
        {
            BuildingStatus status = Describe(HudTestData.House(1, 0));

            Assert.That(status, Is.EqualTo(new BuildingStatus(StatusRingKind.None, 0, StatusBubbleKind.None, default, 0)));
        }

        /// <summary>One pip per assigned worker, and never more than six.</summary>
        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(6, 6)]
        [TestCase(7, 6)]
        [TestCase(40, 6)]
        public void Pips_AreTheWorkersCappedAtSix(int workers, int expected)
        {
            var status = new BuildingStatus(StatusRingKind.None, 0, StatusBubbleKind.None, default, workers);

            Assert.That(status.Pips, Is.EqualTo(expected));
        }

        /// <summary>A building of a type the content does not know (newer content) gets its ring and pips and no bubble, and no exception.</summary>
        [Test]
        public void Describe_UnknownType_HasNoBubbleAndDoesNotThrow()
        {
            var unknown = new Building(1, new BuildingTypeId("windmill"), 0, 0, 1, 2, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);

            BuildingStatus status = Describe(unknown);

            Assert.That(status.Bubble, Is.EqualTo(StatusBubbleKind.None));
            Assert.That(status.Pips, Is.EqualTo(2));
        }

        /// <summary>Two statuses are equal exactly when they draw the same picture, so a view can skip a redraw.</summary>
        [Test]
        public void Equals_ComparesEveryField()
        {
            var logs = new ItemId("logs");
            var a = new BuildingStatus(StatusRingKind.Producing, 500, StatusBubbleKind.MissingInput, logs, 2);

            Assert.That(a, Is.EqualTo(new BuildingStatus(StatusRingKind.Producing, 500, StatusBubbleKind.MissingInput, logs, 2)));
            Assert.That(a, Is.Not.EqualTo(new BuildingStatus(StatusRingKind.Producing, 501, StatusBubbleKind.MissingInput, logs, 2)));
            Assert.That(a, Is.Not.EqualTo(new BuildingStatus(StatusRingKind.Producing, 500, StatusBubbleKind.StorageFull, logs, 2)));
            Assert.That(a, Is.Not.EqualTo(new BuildingStatus(StatusRingKind.Producing, 500, StatusBubbleKind.MissingInput, new ItemId("planks"), 2)));
            Assert.That(a, Is.Not.EqualTo(new BuildingStatus(StatusRingKind.Producing, 500, StatusBubbleKind.MissingInput, logs, 3)));
            Assert.That(a == new BuildingStatus(StatusRingKind.Producing, 500, StatusBubbleKind.MissingInput, logs, 2), Is.True);
        }
    }
}
