#nullable enable

using System;
using CityBuilder.Client.Presentation.City;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>The grid's world and tile conversions and bounds (M0-43), on the 8 by 8 grid of the M0 content and on a grid that is not square.</summary>
    public sealed class CityGridTests
    {
        /// <summary>A grid with the M0 content's size.</summary>
        private readonly CityGrid _grid = new CityGrid(8, 8);

        /// <summary>A tile's world centre is half a tile in from its west and south corner, on the ground.</summary>
        [TestCase(0, 0, 0.5f, 0.5f)]
        [TestCase(3, 5, 3.5f, 5.5f)]
        [TestCase(7, 7, 7.5f, 7.5f)]
        public void ToWorldCenter_GivesTheMiddleOfTheTile(int x, int y, float worldX, float worldZ)
        {
            Vector3 center = _grid.ToWorldCenter(new GridCoordinate(x, y));

            Assert.That(center, Is.EqualTo(new Vector3(worldX, 0f, worldZ)));
        }

        /// <summary>Converting a tile's centre back gives the same tile, for every tile of the grid.</summary>
        [Test]
        public void TryGetCoordinate_OfEveryTileCentre_GivesTheSameTile()
        {
            var grid = new CityGrid(5, 3);

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var tile = new GridCoordinate(x, y);

                    Assert.That(grid.TryGetCoordinate(grid.ToWorldCenter(tile), out GridCoordinate found), Is.True, tile.ToString());
                    Assert.That(found, Is.EqualTo(tile));
                }
            }
        }

        /// <summary>Any point of a tile, its corners included on the west and south sides, belongs to that tile.</summary>
        [TestCase(0f, 0f, 0, 0)]
        [TestCase(0.999f, 0.999f, 0, 0)]
        [TestCase(1f, 0f, 1, 0)]
        [TestCase(2.25f, 6.75f, 2, 6)]
        [TestCase(7.999f, 7.999f, 7, 7)]
        public void TryGetCoordinate_InsideTheGrid_FindsTheTile(float worldX, float worldZ, int x, int y)
        {
            Assert.That(_grid.TryGetCoordinate(new Vector3(worldX, 12f, worldZ), out GridCoordinate found), Is.True);
            Assert.That(found, Is.EqualTo(new GridCoordinate(x, y)));
        }

        /// <summary>Outside the outline there is no tile: the east and north edges are outside, and so is anything negative or not a number.</summary>
        [TestCase(-0.001f, 1f)]
        [TestCase(1f, -0.001f)]
        [TestCase(8f, 1f)]
        [TestCase(1f, 8f)]
        [TestCase(100f, 100f)]
        [TestCase(float.NaN, 1f)]
        [TestCase(1f, float.NaN)]
        public void TryGetCoordinate_OutsideTheGrid_FindsNothing(float worldX, float worldZ)
        {
            Assert.That(_grid.TryGetCoordinate(new Vector3(worldX, 0f, worldZ), out GridCoordinate found), Is.False);
            Assert.That(found, Is.EqualTo(default(GridCoordinate)));
        }

        /// <summary>A tile is inside when both numbers are from 0 up to, not including, the size.</summary>
        [TestCase(0, 0, true)]
        [TestCase(7, 7, true)]
        [TestCase(8, 0, false)]
        [TestCase(0, 8, false)]
        [TestCase(-1, 0, false)]
        [TestCase(0, -1, false)]
        public void Contains_FollowsTheSize(int x, int y, bool expected)
        {
            Assert.That(_grid.Contains(new GridCoordinate(x, y)), Is.EqualTo(expected));
        }

        /// <summary>The width runs along x and the height along z, so a grid that is not square keeps them apart.</summary>
        [Test]
        public void NonSquareGrid_KeepsWidthAndHeightApart()
        {
            var grid = new CityGrid(6, 4);

            Assert.That(grid.WorldSize, Is.EqualTo(new Vector2(6f, 4f)));
            Assert.That(grid.WorldCenter, Is.EqualTo(new Vector2(3f, 2f)));
            Assert.That(grid.Contains(new GridCoordinate(5, 3)), Is.True);
            Assert.That(grid.Contains(new GridCoordinate(3, 5)), Is.False);
        }

        /// <summary>A position inside stays, one outside moves to the nearest point of the outline.</summary>
        [TestCase(3f, 4f, 3f, 4f)]
        [TestCase(-5f, 4f, 0f, 4f)]
        [TestCase(3f, 20f, 3f, 8f)]
        [TestCase(-1f, -1f, 0f, 0f)]
        [TestCase(9f, 9f, 8f, 8f)]
        public void ClampToBounds_KeepsThePositionInsideTheOutline(float x, float z, float expectedX, float expectedZ)
        {
            Assert.That(_grid.ClampToBounds(new Vector2(x, z)), Is.EqualTo(new Vector2(expectedX, expectedZ)));
        }

        /// <summary>A grid needs at least one column and one row.</summary>
        [TestCase(0, 8)]
        [TestCase(8, 0)]
        [TestCase(-1, -1)]
        public void Constructor_RefusesAnEmptyGrid(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityGrid(width, height));
        }

        /// <summary>Two coordinates are equal when both numbers are, and they work as dictionary keys.</summary>
        [Test]
        public void GridCoordinate_IsAValue()
        {
            var a = new GridCoordinate(2, 3);

            Assert.That(a, Is.EqualTo(new GridCoordinate(2, 3)));
            Assert.That(a == new GridCoordinate(2, 3), Is.True);
            Assert.That(a != new GridCoordinate(3, 2), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(new GridCoordinate(2, 3).GetHashCode()));
            Assert.That(a.ToString(), Is.EqualTo("(2, 3)"));
        }
    }
}
