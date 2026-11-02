#nullable enable

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CityBuilder.Client.Presentation.Ui;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.EditMode.Presentation
{
    /// <summary>
    /// Mobile-safe UI (M0-59): the panel sizes a phone from the iPhone XR on gets in both turns (owner directive OD-18), every screen padded by the
    /// safe area, scroll views that scroll one way only (OD-17), and the portrait rules of the stylesheets. The files are read as text, so a change that breaks a narrow or notched screen fails before anyone opens the Device
    /// Simulator; how it looks is the [MANUAL] check in <c>client/README.md</c>.
    /// </summary>
    public sealed class MobileLayoutTests
    {
        /// <summary>The folder of the layouts and stylesheets: <c>Assets/_Project/Resources/UI</c>.</summary>
        private static string UiFolder => Path.Combine(Application.dataPath, "_Project", "Resources", "UI");

        /// <summary>The text of a file of the UI folder.</summary>
        private static string Read(string name) => File.ReadAllText(Path.Combine(UiFolder, name));

        /// <summary>The text of a stylesheet without its comments, which may name a size in words.</summary>
        private static string WithoutComments(string text) => Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        /// <summary>The body of the first rule of <paramref name="stylesheet"/> whose selector is exactly <paramref name="selector"/>; fails when there is none.</summary>
        private static string Rule(string stylesheet, string selector)
        {
            Match match = Regex.Match(WithoutComments(Read(stylesheet)), @"(^|\})\s*" + Regex.Escape(selector) + @"\s*\{([^}]*)\}");
            Assert.That(match.Success, Is.True, stylesheet + " has no rule " + selector);
            return match.Groups[2].Value;
        }

        /// <summary>The number of pixels of <paramref name="property"/> in a rule body (<c>height: 80px</c> gives 80); fails when it is not there.</summary>
        private static int Pixels(string ruleBody, string property)
        {
            Match match = Regex.Match(ruleBody, @"(^|[;\s])" + Regex.Escape(property) + @"\s*:\s*(\d+)px");
            Assert.That(match.Success, Is.True, property + " in { " + ruleBody + " }");
            return int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The panel size Unity gives a screen in Expand mode, the mode <see cref="SharedLookTests"/> holds: the screen divided by the smaller of
        /// its ratios to the reference resolution (as Unity's <c>PanelSettingsUtility.ResolveScale</c> does).
        /// </summary>
        private static Vector2 ExpandedPanelSize(Vector2Int reference, int width, int height)
        {
            float ratio = Mathf.Min(width / (float)reference.x, height / (float)reference.y);
            return new Vector2(width / ratio, height / ratio);
        }

        /// <summary>
        /// A phone from the iPhone XR on (19.5:9 to 20:9, OD-18: an iPhone XR, a 13 mini or a Galaxy S at 2340 by 1080, an iPhone 15, a 20:9 Android)
        /// has 400 units on its short side and 865 to 889 on its long side, in landscape and in portrait.
        /// </summary>
        [TestCase(1792, 828)]
        [TestCase(2340, 1080)]
        [TestCase(2556, 1179)]
        [TestCase(2400, 1080)]
        [TestCase(828, 1792)]
        [TestCase(1080, 2340)]
        [TestCase(1179, 2556)]
        [TestCase(1080, 2400)]
        public void APhoneFromTheIPhoneXrOn_GetsAShortSideOf400_InEitherTurn(int width, int height)
        {
            var settings = Resources.Load<PanelSettings>("UI/PanelSettings");
            Assert.That(settings, Is.Not.Null);

            Vector2 size = ExpandedPanelSize(settings.referenceResolution, width, height);

            Assert.That(Mathf.Min(size.x, size.y), Is.EqualTo(400f).Within(0.5f));
            Assert.That(Mathf.Max(size.x, size.y), Is.InRange(865f, 889f));
        }

        /// <summary>
        /// Every scroll view of every layout scrolls one way only (owner directive OD-17): content that does not fit across it is scaled to fit
        /// (<c>ScaleToFit</c>), never given a second scroll direction.
        /// </summary>
        [Test]
        public void EveryScrollView_ScrollsOneWayOnly()
        {
            int scrollViews = 0;
            foreach (string path in Directory.GetFiles(UiFolder, "*.uxml"))
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(path), @"<ui:ScrollView\b[^>]*>"))
                {
                    scrollViews++;
                    Assert.That(match.Value, Does.Match(@"\bmode=""(Vertical|Horizontal)"""), Path.GetFileName(path) + ": " + match.Value);
                }
            }

            Assert.That(scrollViews, Is.GreaterThanOrEqualTo(3));
        }

        /// <summary>
        /// Every screen that makes a UI document of its own pads its root by the safe area (the spec of M0-59; the status label and the fatal error
        /// screen did not before it), so nothing sits under a notch or a rounded corner.
        /// </summary>
        [Test]
        public void EveryScreenWithAUiDocument_PadsByTheSafeArea()
        {
            string presentation = Path.Combine(Application.dataPath, "_Project", "Presentation");
            var screens = new List<string>();
            foreach (string path in Directory.GetFiles(presentation, "*.cs", SearchOption.AllDirectories))
            {
                string code = File.ReadAllText(path);
                if (!code.Contains("AddComponent<UIDocument>()"))
                {
                    continue;
                }

                screens.Add(Path.GetFileName(path));
                Assert.That(code, Does.Contain("PadBySafeArea"), Path.GetFileName(path) + " makes a UI document but does not pad it by the safe area");
            }

            Assert.That(screens, Does.Contain("ConnectionStatusView.cs"));
            Assert.That(screens.Count, Is.GreaterThanOrEqualTo(9));
        }

        /// <summary>
        /// A panel fixed wider than 300 units may shrink to the screen (<c>max-width: 100%</c>): a portrait root is 400 units wide and has 384
        /// inside its padding, less a side inset.
        /// </summary>
        [Test]
        public void EveryPanelWiderThan300_CanShrinkToTheScreen()
        {
            int wide = 0;
            foreach (string path in Directory.GetFiles(UiFolder, "*.uss"))
            {
                string name = Path.GetFileName(path);
                foreach (Match rule in Regex.Matches(WithoutComments(File.ReadAllText(path)), @"([^{}]+)\{([^}]*)\}"))
                {
                    Match width = Regex.Match(rule.Groups[2].Value, @"(^|[;\s])width\s*:\s*(\d+)px");
                    if (!width.Success || int.Parse(width.Groups[2].Value, CultureInfo.InvariantCulture) <= 300)
                    {
                        continue;
                    }

                    wide++;
                    Assert.That(rule.Groups[2].Value, Does.Contain("max-width: 100%"), name + ": " + rule.Groups[1].Value.Trim());
                }
            }

            Assert.That(wide, Is.GreaterThanOrEqualTo(3));
        }

        /// <summary>Every root is marked with the class the portrait rules use.</summary>
        [Test]
        public void ThePortraitClass_IsTheOneTheStylesheetsUse()
        {
            Assert.That(UiRoot.PortraitClass, Is.EqualTo("portrait"));
            Assert.That(Read("Common.uss"), Does.Contain("." + UiRoot.PortraitClass + " {"));
        }

        /// <summary>
        /// The HUD is one row in landscape and two in portrait, and in both turns the screens under it start where it ends: the bar's height and
        /// the 12 unit gap make <c>--hud-height</c>.
        /// </summary>
        [Test]
        public void TheHud_IsTwoRowsInPortrait_AndTheScreensUnderItFollow()
        {
            string portraitBar = Rule("Hud.uss", ".portrait .hud-panel");
            string rootTokens = Rule("Common.uss", ":root");
            string portraitTokens = Rule("Common.uss", ".portrait");

            Assert.That(portraitBar, Does.Contain("flex-direction: column"));
            Assert.That(Pixels(Rule("Hud.uss", ".hud-panel"), "height") + 12, Is.EqualTo(Pixels(rootTokens, "--hud-height")));
            Assert.That(Pixels(portraitBar, "height") + 12, Is.EqualTo(Pixels(portraitTokens, "--hud-height")));
        }

        /// <summary>
        /// In portrait the placement bar sits above the dock instead of beside it (it would cover the Research button), and the toasts stack up
        /// from higher than in landscape, above the build tray and the bar.
        /// </summary>
        [Test]
        public void InPortrait_ThePlacementBarSitsAboveTheDock_AndTheToastsAboveIt()
        {
            string rootTokens = Rule("Common.uss", ":root");

            Assert.That(Rule("PlacementBar.uss", ".portrait .placement-panel"), Does.Contain("margin-bottom: var(--dock-lift)"));
            Assert.That(Pixels(rootTokens, "--dock-lift"), Is.EqualTo(Pixels(rootTokens, "--touch") + Pixels(rootTokens, "--sp-2")));
            Assert.That(Pixels(Rule("Common.uss", ".portrait"), "--toast-lift"), Is.GreaterThan(Pixels(rootTokens, "--toast-lift")));
        }
    }
}
