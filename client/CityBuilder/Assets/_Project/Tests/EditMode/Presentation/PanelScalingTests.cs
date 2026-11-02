#nullable enable

using System.IO;
using CityBuilder.Client.Presentation.Ui;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Presentation
{
    /// <summary>
    /// Mobile-safe UI (M0-59): the panel scaling for landscape and portrait, and the portrait rules of the stylesheets, held as text so a change
    /// that breaks a narrow screen fails before anyone opens the Device Simulator.
    /// </summary>
    public sealed class PanelScalingTests
    {
        /// <summary>The text of a stylesheet of <c>Assets/_Project/Resources/UI</c>.</summary>
        private static string Read(string name) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Resources", "UI", name));

        /// <summary>A 16:9 to 20:9 landscape screen is matched on its height at 880 by 400.</summary>
        [TestCase(1920, 1080)]
        [TestCase(2400, 1080)]
        [TestCase(1280, 720)]
        [TestCase(1000, 1000)]
        [TestCase(0, 0)]
        public void ALandscapeOrUnknownScreen_IsMatchedOnHeight(int width, int height)
        {
            PanelScaling scaling = PanelScaling.For(width, height);

            Assert.That(scaling.ReferenceResolution, Is.EqualTo(new Vector2Int(880, 400)));
            Assert.That(scaling.Match, Is.EqualTo(1f));
            Assert.That(scaling.IsPortrait, Is.False);
        }

        /// <summary>A portrait screen is matched on its width at 400 by 880, so the short side still has about 400 units.</summary>
        [TestCase(1080, 1920)]
        [TestCase(1080, 2400)]
        [TestCase(720, 1280)]
        public void APortraitScreen_IsMatchedOnWidth(int width, int height)
        {
            PanelScaling scaling = PanelScaling.For(width, height);

            Assert.That(scaling.ReferenceResolution, Is.EqualTo(new Vector2Int(400, 880)));
            Assert.That(scaling.Match, Is.EqualTo(0f));
            Assert.That(scaling.IsPortrait, Is.True);
        }

        /// <summary>Panels that are fixed in width must be allowed to shrink to the 400 unit wide portrait screen.</summary>
        [TestCase("Inspector.uss")]
        [TestCase("PlacementBar.uss")]
        [TestCase("ConnectionBanner.uss")]
        [TestCase("WelcomeBack.uss")]
        public void AFixedWidthPanel_CanShrinkToTheScreen(string stylesheet)
        {
            Assert.That(Read(stylesheet), Does.Contain("max-width: 100%"), stylesheet);
        }

        /// <summary>The HUD drops its secondary text in portrait and every screen root is marked, so the bar cannot overlap itself.</summary>
        [Test]
        public void TheHud_DropsSecondaryTextInPortrait()
        {
            string hud = Read("Hud.uss");

            Assert.That(hud, Does.Contain(".portrait .hud-research-text"));
            Assert.That(hud, Does.Contain("overflow: hidden"));
            Assert.That(UiRoot.PortraitClass, Is.EqualTo("portrait"));
        }
    }
}
