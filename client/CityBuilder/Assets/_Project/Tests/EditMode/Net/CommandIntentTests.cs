#nullable enable

using CityBuilder.Client.Net;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary><see cref="CommandIntent"/>: what each factory holds, when two intents are the same one, and the ids it refuses.</summary>
    public sealed class CommandIntentTests
    {
        /// <summary>Each factory sets its kind and its own arguments and leaves the others at their defaults.</summary>
        [Test]
        public void EachFactory_HoldsItsKindAndArguments()
        {
            CommandIntent place = CommandIntent.PlaceBuilding("sawmill", 4, 7);
            CommandIntent upgrade = CommandIntent.UpgradeBuilding(3);
            CommandIntent workers = CommandIntent.AssignWorkers(3, 2);
            CommandIntent research = CommandIntent.StartResearch("trade_charter");

            Assert.That(place.Kind, Is.EqualTo(CommandKind.PlaceBuilding));
            Assert.That((place.ContentId, place.X, place.Y), Is.EqualTo(("sawmill", 4, 7)));
            Assert.That((upgrade.Kind, upgrade.BuildingId), Is.EqualTo((CommandKind.UpgradeBuilding, 3)));
            Assert.That((workers.Kind, workers.BuildingId, workers.Workers), Is.EqualTo((CommandKind.AssignWorkers, 3, 2)));
            Assert.That((research.Kind, research.ContentId), Is.EqualTo((CommandKind.StartResearch, "trade_charter")));
        }

        /// <summary>Intents with the same kind and arguments are equal; any difference makes them different.</summary>
        [Test]
        public void TwoIntents_AreEqualWhenKindAndArgumentsMatch()
        {
            Assert.That(CommandIntent.PlaceBuilding("sawmill", 4, 7), Is.EqualTo(CommandIntent.PlaceBuilding("sawmill", 4, 7)));
            Assert.That(CommandIntent.PlaceBuilding("sawmill", 4, 7).GetHashCode(), Is.EqualTo(CommandIntent.PlaceBuilding("sawmill", 4, 7).GetHashCode()));
            Assert.That(CommandIntent.PlaceBuilding("sawmill", 4, 7), Is.Not.EqualTo(CommandIntent.PlaceBuilding("sawmill", 4, 8)));
            Assert.That(CommandIntent.PlaceBuilding("sawmill", 4, 7), Is.Not.EqualTo(CommandIntent.PlaceBuilding("house", 4, 7)));
            Assert.That(CommandIntent.AssignWorkers(3, 2), Is.Not.EqualTo(CommandIntent.AssignWorkers(3, 1)));
            Assert.That(CommandIntent.UpgradeBuilding(3), Is.Not.EqualTo(CommandIntent.AssignWorkers(3, 0)));
        }

        /// <summary>A blank content id is refused when the intent is made: no content has such an id.</summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void ABlankContentId_IsRefused(string? id)
        {
            Assert.That(() => CommandIntent.PlaceBuilding(id!, 0, 0), Throws.ArgumentException);
            Assert.That(() => CommandIntent.StartResearch(id!), Throws.ArgumentException);
        }

        /// <summary>The log form names the kind and the arguments.</summary>
        [Test]
        public void ToString_NamesTheKindAndArguments()
        {
            Assert.That(CommandIntent.PlaceBuilding("sawmill", 4, 7).ToString(), Is.EqualTo("PlaceBuilding(sawmill at 4,7)"));
            Assert.That(CommandIntent.UpgradeBuilding(3).ToString(), Is.EqualTo("UpgradeBuilding(building 3)"));
            Assert.That(CommandIntent.AssignWorkers(3, 2).ToString(), Is.EqualTo("AssignWorkers(building 3, workers 2)"));
            Assert.That(CommandIntent.StartResearch("trade_charter").ToString(), Is.EqualTo("StartResearch(trade_charter)"));
        }
    }
}
