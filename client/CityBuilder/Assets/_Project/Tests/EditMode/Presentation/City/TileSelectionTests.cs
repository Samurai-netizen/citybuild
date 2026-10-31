#nullable enable

using CityBuilder.Client.Presentation.City;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>The hovered and selected tile (M0-43): what a hover, a tap and a tap outside do, and when the change event fires.</summary>
    public sealed class TileSelectionTests
    {
        /// <summary>A selection over the 8 by 8 grid; a new one for each test, because NUnit uses one fixture object for all of them.</summary>
        private TileSelection _selection = null!;

        /// <summary>How many times <see cref="TileSelection.Changed"/> was raised.</summary>
        private int _changes;

        /// <summary>Before each test: creates the selection and counts its change events.</summary>
        [SetUp]
        public void CreateSelection()
        {
            _selection = new TileSelection(new CityGrid(8, 8));
            _changes = 0;
            _selection.Changed += () => _changes++;
        }

        /// <summary>A new selection has no hovered and no selected tile.</summary>
        [Test]
        public void New_HasNothing()
        {
            Assert.That(_selection.Hovered, Is.Null);
            Assert.That(_selection.Selected, Is.Null);
        }

        /// <summary>Hovering a tile sets it, raises the event once, and hovering the same tile again raises nothing.</summary>
        [Test]
        public void SetHovered_SetsTheTile_AndRaisesOnlyOnAChange()
        {
            _selection.SetHovered(new GridCoordinate(2, 3));
            _selection.SetHovered(new GridCoordinate(2, 3));

            Assert.That(_selection.Hovered, Is.EqualTo(new GridCoordinate(2, 3)));
            Assert.That(_changes, Is.EqualTo(1));
        }

        /// <summary>Hovering nothing, or a tile outside the grid, clears the hover.</summary>
        [Test]
        public void SetHovered_WithNullOrOutside_Clears()
        {
            _selection.SetHovered(new GridCoordinate(1, 1));
            _selection.SetHovered(new GridCoordinate(8, 1));
            Assert.That(_selection.Hovered, Is.Null);

            _selection.SetHovered(new GridCoordinate(1, 1));
            _selection.SetHovered(null);
            Assert.That(_selection.Hovered, Is.Null);
        }

        /// <summary>A tap on a tile selects it and moves the selection from one tile to another.</summary>
        [Test]
        public void Tap_SelectsTheTile_AndMovesTheSelection()
        {
            _selection.Tap(new GridCoordinate(1, 1));
            Assert.That(_selection.Selected, Is.EqualTo(new GridCoordinate(1, 1)));

            _selection.Tap(new GridCoordinate(4, 5));
            Assert.That(_selection.Selected, Is.EqualTo(new GridCoordinate(4, 5)));
            Assert.That(_changes, Is.EqualTo(2));
        }

        /// <summary>Tapping the selected tile again clears the selection.</summary>
        [Test]
        public void Tap_OnTheSelectedTile_Clears()
        {
            _selection.Tap(new GridCoordinate(1, 1));

            _selection.Tap(new GridCoordinate(1, 1));

            Assert.That(_selection.Selected, Is.Null);
        }

        /// <summary>Tapping outside the grid clears the selection; with nothing selected it changes nothing and raises nothing.</summary>
        [Test]
        public void Tap_Outside_ClearsTheSelection()
        {
            _selection.Tap(null);
            Assert.That(_changes, Is.EqualTo(0));

            _selection.Tap(new GridCoordinate(1, 1));
            _selection.Tap(new GridCoordinate(-1, 3));

            Assert.That(_selection.Selected, Is.Null);
            Assert.That(_changes, Is.EqualTo(2));
        }

        /// <summary>ClearSelection clears the selection and leaves the hover alone.</summary>
        [Test]
        public void ClearSelection_LeavesTheHover()
        {
            _selection.SetHovered(new GridCoordinate(0, 0));
            _selection.Tap(new GridCoordinate(1, 1));

            _selection.ClearSelection();

            Assert.That(_selection.Selected, Is.Null);
            Assert.That(_selection.Hovered, Is.EqualTo(new GridCoordinate(0, 0)));
        }
    }
}
