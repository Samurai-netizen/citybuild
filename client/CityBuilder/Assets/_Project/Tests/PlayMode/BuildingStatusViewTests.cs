#nullable enable

using System.Collections.Generic;
using System.IO;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using UnityEngine.TestTools.Utils;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Tests.PlayMode
{
    /// <summary>
    /// The status of the buildings in a running scene (M0-50): given the real content and a city, the ring, the bubble and the pips above a
    /// building follow its phase, its clock and its stall reason, stand at the model's status anchor, face the camera, and change again when
    /// the next city says so; a showing where nothing changed allocates nothing.
    /// </summary>
    public sealed class BuildingStatusViewTests
    {
        /// <summary>The object that holds the world under test.</summary>
        private GameObject _host = null!;

        /// <summary>The world under test, showing an 8 by 8 grid.</summary>
        private CityWorld _world = null!;

        /// <summary>The real content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>Before each test: loads the content and makes a world with an 8 by 8 grid.</summary>
        [SetUp]
        public void CreateWorld()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Tests/Fixtures/content.json"));
            ContentResponseDto dto = new ApiJson().Deserialize<ContentResponseDto>(json)!;
            GameContentResult built = GameContentFactory.Create(ContentMapper.ToData(dto));
            Assert.That(built.IsValid, Is.True);
            _content = built.Content!;
            _host = new GameObject("Test world host");
            _world = _host.AddComponent<CityWorld>();
            _world.ShowGrid(new CityGrid(8, 8));
        }

        /// <summary>After each test: destroys the world.</summary>
        [TearDown]
        public void DestroyWorld()
        {
            Object.DestroyImmediate(_host);
        }

        /// <summary>A producer running its cycle (60 worker-seconds) with two workers is half done at second 15, and the ring is half full.</summary>
        [Test]
        public void Ring_OfAProducer_FollowsTheClock()
        {
            Show(15, 40, Producer(1, "woodcutter", 2));
            BuildingStatusView status = StatusOf(1)!;
            Assert.That(status.RingStep, Is.EqualTo(StatusArt.StepOf(500)));

            Show(30, 40, Producer(1, "woodcutter", 2));

            Assert.That(status.RingStep, Is.EqualTo(32), "a finished cycle fills the ring");
        }

        /// <summary>A building under construction gets a ring at the construction site's status anchor, and the ring follows the clock.</summary>
        [Test]
        public void Ring_OfAConstructingBuilding_StandsAtTheSitesAnchor()
        {
            Show(25, 40, new Building(1, new BuildingTypeId("house"), 1, 1, 1, 0, BuildingPhase.Constructing, 0, 100, 0, 0, 0, 0));

            _world.Buildings!.TryGetView(1, out BuildingView? view);
            BuildingStatusView status = StatusOf(1)!;
            Assert.That(view!.StatusAnchor, Is.Not.Null, "the construction site has the anchor");
            Assert.That(status.transform.position, Is.EqualTo(view.StatusAnchor!.position).Using(Vector3EqualityComparer.Instance));
            Assert.That(status.RingStep, Is.EqualTo(StatusArt.StepOf(250)));
        }

        /// <summary>When a building is finished the ring goes and the status follows the new body's anchor.</summary>
        [Test]
        public void Ring_WhenTheBuildingIsFinished_GoesAndTheStatusMovesToTheNewAnchor()
        {
            Show(25, 40, new Building(1, new BuildingTypeId("woodcutter"), 1, 1, 1, 0, BuildingPhase.Constructing, 0, 100, 0, 0, 0, 0));
            BuildingStatusView status = StatusOf(1)!;

            Show(100, 40, new Building(1, new BuildingTypeId("woodcutter"), 1, 1, 1, 2, BuildingPhase.Idle, 0, null, 0, 0, 0, 0));

            _world.Buildings!.TryGetView(1, out BuildingView? view);
            Assert.That(status.RingStep, Is.EqualTo(-1));
            Assert.That(status.PipCount, Is.EqualTo(2));
            Assert.That(status.transform.position, Is.EqualTo(view!.StatusAnchor!.position).Using(Vector3EqualityComparer.Instance));
        }

        /// <summary>A sawmill with workers and no logs shows the red bubble with the logs and a strike across them.</summary>
        [Test]
        public void Bubble_OfAMissingInput_IsRedWithThePictureOfTheItemAndAStrike()
        {
            Show(0, 0, Idle(1, "sawmill", 3));

            BuildingStatusView status = StatusOf(1)!;
            Assert.That(status.BubbleVisible, Is.True);
            Assert.That(status.BubbleColor, Is.EqualTo(StatusArt.BubbleColor(StatusBubbleKind.MissingInput)));
            Assert.That(status.BubbleIcon, Is.Not.Null, "the logs icon");
            Assert.That(status.BubbleStruck, Is.True);
        }

        /// <summary>A woodcutter without workers shows the amber bubble with the workers picture and no strike.</summary>
        [Test]
        public void Bubble_OfNoWorkers_IsAmberWithTheWorkersPicture()
        {
            Show(0, 40, Idle(1, "woodcutter", 0));

            BuildingStatusView status = StatusOf(1)!;
            Assert.That(status.BubbleVisible, Is.True);
            Assert.That(status.BubbleColor, Is.EqualTo(StatusArt.BubbleColor(StatusBubbleKind.NoWorkers)));
            Assert.That(status.BubbleIcon, Is.Not.Null, "the workers icon");
            Assert.That(status.BubbleStruck, Is.False);
        }

        /// <summary>A woodcutter whose logs are at the cap shows the blue bubble with the logs and no strike.</summary>
        [Test]
        public void Bubble_OfFullStorage_IsBlueWithThePictureOfTheItem()
        {
            Show(0, 500, Idle(1, "woodcutter", 2));

            BuildingStatusView status = StatusOf(1)!;
            Assert.That(status.BubbleColor, Is.EqualTo(StatusArt.BubbleColor(StatusBubbleKind.StorageFull)));
            Assert.That(status.BubbleIcon, Is.Not.Null);
            Assert.That(status.BubbleStruck, Is.False);
        }

        /// <summary>The bubble goes when the reason is gone (the logs arrive), and comes back with another reason.</summary>
        [Test]
        public void Bubble_FollowsTheNextCity()
        {
            Show(0, 0, Idle(1, "sawmill", 3));
            BuildingStatusView status = StatusOf(1)!;
            Assert.That(status.BubbleVisible, Is.True);

            Show(0, 40, Idle(1, "sawmill", 3));
            Assert.That(status.BubbleVisible, Is.False, "the logs are there");

            Show(0, 40, Idle(1, "sawmill", 0));
            Assert.That(status.BubbleColor, Is.EqualTo(StatusArt.BubbleColor(StatusBubbleKind.NoWorkers)));
        }

        /// <summary>One pip per assigned worker, at most six, each of them drawn.</summary>
        [TestCase(1, 1)]
        [TestCase(3, 3)]
        [TestCase(6, 6)]
        [TestCase(9, 6)]
        public void Pips_AreOnePerWorker_AtMostSix(int workers, int expected)
        {
            Show(0, 40, Producer(1, "woodcutter", workers));

            BuildingStatusView status = StatusOf(1)!;
            Assert.That(status.PipCount, Is.EqualTo(expected));
            Assert.That(DrawnPips(status), Is.EqualTo(expected));
        }

        /// <summary>Fewer workers hide the pips that are no longer needed.</summary>
        [Test]
        public void Pips_ShrinkWhenWorkersLeave()
        {
            Show(0, 40, Producer(1, "woodcutter", 4));
            BuildingStatusView status = StatusOf(1)!;

            Show(0, 40, Producer(1, "woodcutter", 1));

            Assert.That(DrawnPips(status), Is.EqualTo(1));
        }

        /// <summary>A cottage has nothing to show, so it gets no status object at all.</summary>
        [Test]
        public void ACottage_HasNoStatusObject()
        {
            Show(0, 40, Idle(1, "house", 0));

            _world.Buildings!.TryGetView(1, out BuildingView? view);
            Assert.That(view!.Status, Is.Null);
            Assert.That(view.transform.Find(BuildingStatusView.RootName), Is.Null);
        }

        /// <summary>The status faces the camera: it has the camera's rotation.</summary>
        [Test]
        public void TheStatus_FacesTheCamera()
        {
            Show(15, 40, Producer(1, "woodcutter", 2));

            BuildingStatusView status = StatusOf(1)!;
            Assert.That(status.transform.rotation, Is.EqualTo(_world.Camera.transform.rotation).Using(QuaternionEqualityComparer.Instance));
        }

        /// <summary>A building of a type the content does not know is drawn as the grey box, with its pips at the fixed height and no exception.</summary>
        [Test]
        public void ABuildingFromNewerContent_GetsPipsAtTheFallbackHeight()
        {
            Show(0, 40, Idle(1, "windmill", 2));

            BuildingStatusView status = StatusOf(1)!;
            Assert.That(status.PipCount, Is.EqualTo(2));
            Assert.That(status.transform.localPosition.y, Is.EqualTo(BuildingStatusView.FallbackAnchorHeight).Within(0.001f));
        }

        /// <summary>A status of a building that is removed goes with it.</summary>
        [Test]
        public void ARemovedBuilding_TakesItsStatusWithIt()
        {
            Show(0, 40, Producer(1, "woodcutter", 2));
            _world.Buildings!.TryGetView(1, out BuildingView? view);
            BuildingStatusView status = StatusOf(1)!;

            Show(0, 40);

            Assert.That(_world.Buildings.Count, Is.EqualTo(0));
            Assert.That(view!.transform.parent, Is.Null, "out of the tree at once");
            Assert.That(status.transform.IsChildOf(view.transform), Is.True);
        }

        /// <summary>Showing a city again where nothing changed allocates nothing (a producing and a constructing building; a stalled one makes the engine's small reason object).</summary>
        [Test]
        public void Show_WithNothingChanged_AllocatesNothing()
        {
            CityState city = City(
                15,
                40,
                Producer(1, "woodcutter", 2),
                new Building(2, new BuildingTypeId("house"), 3, 3, 1, 0, BuildingPhase.Constructing, 0, 100, 0, 0, 0, 0));
            _world.ShowCity(city, _content);

            Assert.That(() => _world.Buildings!.Show(city, _content), Is.Not.AllocatingGCMemory());
        }

        /// <summary>How many pips under <paramref name="status"/> are drawn.</summary>
        private static int DrawnPips(BuildingStatusView status)
        {
            int drawn = 0;
            foreach (SpriteRenderer renderer in status.GetComponentsInChildren<SpriteRenderer>())
            {
                if (renderer.name.StartsWith("Pip ") && renderer.enabled)
                {
                    drawn++;
                }
            }

            return drawn;
        }

        /// <summary>The status object of building <paramref name="id"/>, or null.</summary>
        private BuildingStatusView? StatusOf(int id)
        {
            return _world.Buildings!.TryGetView(id, out BuildingView? view) ? view!.Status : null;
        }

        /// <summary>Shows a city at <paramref name="clock"/> with <paramref name="logs"/> logs and the given buildings, with the content.</summary>
        private void Show(long clock, long logs, params Building[] buildings)
        {
            _world.ShowCity(City(clock, logs, buildings), _content);
        }

        /// <summary>A city of Era I at <paramref name="clock"/> with 700 coins, <paramref name="logs"/> logs and 10 planks.</summary>
        private static CityState City(long clock, long logs, params Building[] buildings)
        {
            var stock = new List<KeyValuePair<ItemId, long>>
            {
                new KeyValuePair<ItemId, long>(new ItemId("coins"), 700),
                new KeyValuePair<ItemId, long>(new ItemId("logs"), logs),
                new KeyValuePair<ItemId, long>(new ItemId("planks"), 10),
            };
            return new CityState(clock, 1, 100, stock, new List<Building>(buildings), new List<ResearchId>(), null);
        }

        /// <summary>An idle building of <paramref name="typeId"/> on tile (1, 1) with <paramref name="workers"/> workers.</summary>
        private static Building Idle(int id, string typeId, int workers)
        {
            return new Building(id, new BuildingTypeId(typeId), 1, 1, 1, workers, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
        }

        /// <summary>A building of <paramref name="typeId"/> running its first cycle of 60 worker-seconds, started at second 0, with <paramref name="workers"/> workers.</summary>
        private static Building Producer(int id, string typeId, int workers)
        {
            long cycleSeconds = (60 + workers - 1) / workers;
            return new Building(id, new BuildingTypeId(typeId), 1, 1, 1, workers, BuildingPhase.Producing, 0, cycleSeconds, 0, 60, 60, 0);
        }
    }
}
