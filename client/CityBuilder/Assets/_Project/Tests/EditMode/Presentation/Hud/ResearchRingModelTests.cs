#nullable enable

using CityBuilder.Client.Presentation.Hud;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Hud
{
    /// <summary>The research ring (M0-49): progress, the node's name and the time left at the displayed clock.</summary>
    public sealed class ResearchRingModelTests
    {
        /// <summary>The real content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>Before each test: the content.</summary>
        [SetUp]
        public void LoadContent()
        {
            _content = HudTestData.Content();
        }

        /// <summary>With no research the ring is empty and says so.</summary>
        [Test]
        public void NoResearch_IsIdle()
        {
            ResearchRingModel ring = ResearchRingModel.Create(_content, HudTestData.City(0, 0, 0, 0, new Building[0]));

            Assert.That(ring.IsActive, Is.False);
            Assert.That(ring.Permille, Is.EqualTo(0));
            Assert.That(ring.Name, Is.EqualTo("No research"));
            Assert.That(ring.TimeLeftText, Is.Empty);
        }

        /// <summary>A quarter of the way through "Sharpened Axes" (1800 s) shows 250 permille, its name and 22m 30s left.</summary>
        [Test]
        public void RunningResearch_ShowsProgressNameAndTimeLeft()
        {
            CityState city = HudTestData.City(1450, 0, 0, 0, new Building[0], null, HudTestData.Research("sharp_axes", 1000, 2800));

            ResearchRingModel ring = ResearchRingModel.Create(_content, city);

            Assert.That(ring.IsActive, Is.True);
            Assert.That(ring.Permille, Is.EqualTo(250));
            Assert.That(ring.Name, Is.EqualTo("Sharpened Axes"));
            Assert.That(ring.TimeLeftText, Is.EqualTo("22m 30s"));
        }

        /// <summary>After the end and before the next snapshot the ring is full and the time left is 0s.</summary>
        [Test]
        public void PastTheEnd_IsFullWithNoTimeLeft()
        {
            CityState city = HudTestData.City(3000, 0, 0, 0, new Building[0], null, HudTestData.Research("sharp_axes", 1000, 2800));

            ResearchRingModel ring = ResearchRingModel.Create(_content, city);

            Assert.That(ring.Permille, Is.EqualTo(1000));
            Assert.That(ring.TimeLeftText, Is.EqualTo("0s"));
        }

        /// <summary>A node the content does not know shows its id, so the ring never throws.</summary>
        [Test]
        public void UnknownNode_ShowsItsId()
        {
            CityState city = HudTestData.City(0, 0, 0, 0, new Building[0], null, HudTestData.Research("mystery", 0, 100));

            Assert.That(ResearchRingModel.Create(_content, city).Name, Is.EqualTo("mystery"));
        }

        /// <summary>Two rings that say the same are equal; a later clock makes them differ.</summary>
        [Test]
        public void Equality_FollowsTheValues()
        {
            ActiveResearch active = HudTestData.Research("sharp_axes", 0, 1800);
            ResearchRingModel a = ResearchRingModel.Create(_content, HudTestData.City(100, 0, 0, 0, new Building[0], null, active));
            ResearchRingModel b = ResearchRingModel.Create(_content, HudTestData.City(100, 0, 0, 0, new Building[0], null, active));
            ResearchRingModel c = ResearchRingModel.Create(_content, HudTestData.City(101, 0, 0, 0, new Building[0], null, active));

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a, Is.Not.EqualTo(c));
        }
    }
}
