#nullable enable

using System;
using CityBuilder.Client.Presentation.Build;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Build
{
    /// <summary>
    /// The placement hints on the Era I map (M1-03): for the type picked in the build menu each tile is free, occupied, blocked (the type can
    /// never stand there: water, forest, rock, or its site unmet) or closed (land under mist), in the server's order of checks; a tile that is
    /// not free cannot be confirmed, and the bar says why in the words of the server's refusal.
    /// </summary>
    public sealed class PlacementHintsTests
    {
        /// <summary>The woodcutter's hut: it needs forest on one of its four sides.</summary>
        private static readonly BuildingTypeId Woodcutter = new BuildingTypeId("woodcutter");

        /// <summary>The sawmill: it may stand on any open grass or soil.</summary>
        private static readonly BuildingTypeId Sawmill = new BuildingTypeId("sawmill");

        /// <summary>The flow under test, over the map's 18 × 14 grid.</summary>
        private PlacementFlowModel _flow = null!;

        /// <summary>Before each test: a flow over the real content and its map, and the start city (only area A open).</summary>
        [SetUp]
        public void CreateFlow()
        {
            GameContent content = BuildMenuTestData.Content();
            _flow = new PlacementFlowModel(content, new CityGrid(content.Rules.GridWidth, content.Rules.GridHeight));
            _flow.Update(BuildMenuTestData.StartCity());
        }

        /// <summary>
        /// For the woodcutter: a grass tile with forest beside it in the open area is free; open grass with no forest beside it, the river and
        /// a forest tile are blocked; grass with forest beside it in a closed area is closed.
        /// </summary>
        [Test]
        public void Hints_ForTheWoodcutter_FollowItsSiteAndTheOpenLand()
        {
            _flow.SetType(Woodcutter);

            Assert.That(_flow.HintAt(new GridCoordinate(4, 2)), Is.EqualTo(TileHint.Free), "forest at (3, 2)");
            Assert.That(_flow.HintAt(new GridCoordinate(6, 6)), Is.EqualTo(TileHint.Blocked), "no forest beside it");
            Assert.That(_flow.HintAt(new GridCoordinate(12, 5)), Is.EqualTo(TileHint.Blocked), "the river");
            Assert.That(_flow.HintAt(new GridCoordinate(2, 3)), Is.EqualTo(TileHint.Blocked), "a forest tile");
            Assert.That(_flow.HintAt(new GridCoordinate(15, 5)), Is.EqualTo(TileHint.Closed), "forest at (16, 5), area C");
        }

        /// <summary>The hints follow the picked type: open grass is free for the sawmill and blocked for the woodcutter.</summary>
        [Test]
        public void SetType_ChangesTheHints()
        {
            int changed = 0;
            _flow.SetType(Sawmill);
            _flow.Changed += () => changed++;

            Assert.That(_flow.HintAt(new GridCoordinate(6, 6)), Is.EqualTo(TileHint.Free));
            _flow.SetType(Woodcutter);

            Assert.That(_flow.HintAt(new GridCoordinate(6, 6)), Is.EqualTo(TileHint.Blocked));
            Assert.That(changed, Is.GreaterThan(0));
        }

        /// <summary>Once a research opens area C, its tiles are no longer closed: the woodcutter may stand at (15, 5).</summary>
        [Test]
        public void Update_AfterTheLandOpens_ClosedTilesBecomeFree()
        {
            _flow.SetType(Woodcutter);

            _flow.Update(BuildMenuTestData.City(800, 0, 0, new[] { "open_fields" }, 0));

            Assert.That(_flow.HintAt(new GridCoordinate(15, 5)), Is.EqualTo(TileHint.Free));
        }

        /// <summary>
        /// A blocked or closed tile can be picked, so the bar can say why, but not confirmed: the bar gives the server's sentence for the code the
        /// server would refuse it with (the site, the terrain, the mist).
        /// </summary>
        [Test]
        public void HintText_OnATileThatIsNotFree_SaysWhyAndPlaceIsOff()
        {
            _flow.SetType(Woodcutter);
            var cases = new[]
            {
                (new GridCoordinate(6, 6), "A Woodcutter's Hut needs forest next to it"),
                (new GridCoordinate(12, 5), "Nothing can be built on water, forest or rock"),
                (new GridCoordinate(15, 5), "That land is still unexplored. Research opens it"),
            };
            foreach ((GridCoordinate tile, string reason) in cases)
            {
                _flow.SetTile(tile);

                Assert.That(_flow.CanConfirm, Is.False, tile.ToString());
                Assert.That(_flow.HintText, Is.EqualTo(reason), tile.ToString());
            }

            _flow.SetTile(new GridCoordinate(4, 2));
            Assert.That((_flow.CanConfirm, _flow.HintText), Is.EqualTo((true, string.Empty)));
        }

        /// <summary>With no type picked, only buildings and roads mark tiles: every other tile is free.</summary>
        [Test]
        public void Hints_WithoutAType_MarkOnlyWhatStandsOnTheTiles()
        {
            Assert.That(_flow.Hints, Has.All.EqualTo(TileHint.Free));
            Assert.That(Array.IndexOf(_flow.Hints, TileHint.Blocked), Is.EqualTo(-1));
        }
    }
}
