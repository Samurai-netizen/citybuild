#nullable enable

using CityBuilder.Client.Presentation.Ui;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Art
{
    /// <summary>
    /// The placeholders made in code for assets that have not arrived (<c>core-instructions/27-asset-pipeline.md</c> → Placeholders, M1-02):
    /// a tint chosen by the id, the id's initials, the icon tile that shows them, and the one log line per missing id.
    /// </summary>
    public sealed class PlaceholdersTests
    {
        /// <summary>The same id always gets the same tint, and it is opaque.</summary>
        [Test]
        public void TintOf_SameId_GivesTheSameOpaqueColour()
        {
            Color first = Placeholders.TintOf("quarry");

            Assert.That(Placeholders.TintOf("quarry"), Is.EqualTo(first));
            Assert.That(first.a, Is.EqualTo(1f));
        }

        /// <summary>Different ids of the Era I content get different tints, so two placeholders side by side can be told apart.</summary>
        [Test]
        public void TintOf_TheNewEraITypes_AreAllDifferent()
        {
            string[] ids = { "quarry", "stonemason", "farm", "windmill", "bakery", "town_hall" };
            var seen = new System.Collections.Generic.HashSet<Color>();

            foreach (string id in ids)
            {
                Assert.That(seen.Add(Placeholders.TintOf(id)), Is.True, id);
            }
        }

        /// <summary>The initials are the first letters of the id's first two words, or the first two letters of a one-word id, in capitals.</summary>
        [TestCase("cut_stone", "CS")]
        [TestCase("town_hall", "TH")]
        [TestCase("guild_of_masons", "GO")]
        [TestCase("stone", "ST")]
        [TestCase("q", "Q")]
        [TestCase("", "")]
        public void InitialsOf_AnId_GivesItsInitials(string id, string expected)
        {
            Assert.That(Placeholders.InitialsOf(id), Is.EqualTo(expected));
        }

        /// <summary>The icon tile is a square of the id's tint with light pixels where the initials are drawn.</summary>
        [Test]
        public void CreateIconTile_DrawsTheInitialsOnTheTint()
        {
            Texture2D tile = Placeholders.CreateIconTile("cut_stone");
            try
            {
                Assert.That((tile.width, tile.height), Is.EqualTo((Placeholders.IconTileSize, Placeholders.IconTileSize)));
                Color tint = Placeholders.TintOf("cut_stone");
                Assert.That(tile.GetPixel(0, 0), Is.EqualTo(tint), "a corner is the tint");
                int letters = 0;
                foreach (Color pixel in tile.GetPixels())
                {
                    if (pixel == Placeholders.InitialsColor)
                    {
                        letters++;
                    }
                }

                Assert.That(letters, Is.GreaterThan(0), "the initials are drawn");
                Assert.That(tile.name, Does.Contain("cut_stone"));
            }
            finally
            {
                Object.DestroyImmediate(tile);
            }
        }

        /// <summary>A missing asset is logged the first time it is reported, never again.</summary>
        [Test]
        public void ReportMissing_TheSameIdTwice_LogsOnlyTheFirstTime()
        {
            string id = "placeholder-test-" + System.Guid.NewGuid().ToString("N");

            Assert.That(Placeholders.ReportMissing("model", id), Is.True);
            Assert.That(Placeholders.ReportMissing("model", id), Is.False);
        }
    }
}
