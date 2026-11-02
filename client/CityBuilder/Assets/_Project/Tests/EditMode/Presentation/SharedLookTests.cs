#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.EditMode.Presentation
{
    /// <summary>
    /// The shared look of the UI (CR-0007), held by its rules: every layout that has a stylesheet imports <c>Common.uss</c>; no screen stylesheet writes a
    /// colour or a font size of its own (they come from the tokens of <c>Common.uss</c>); every button uses the shared button classes; the touch target
    /// is 48 and the positions under the HUD come from one token; the panel scale is the one the sizes are written for. The files are read as text, so a
    /// change to a stylesheet that breaks the look fails here before anyone opens the Editor.
    /// </summary>
    public sealed class SharedLookTests
    {
        /// <summary>The folder of the layouts and stylesheets: <c>Assets/_Project/Resources/UI</c>.</summary>
        private static string UiFolder => Path.Combine(Application.dataPath, "_Project", "Resources", "UI");

        /// <summary>The shared stylesheet that holds every colour, size and shared class.</summary>
        private const string CommonFile = "Common.uss";

        /// <summary>The text of a file of the UI folder.</summary>
        private static string Read(string name) => File.ReadAllText(Path.Combine(UiFolder, name));

        /// <summary>The text of a stylesheet without its comments, which may name a colour or a size in words.</summary>
        private static string WithoutComments(string text) => Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        /// <summary>The names of the files with the given extension in the UI folder.</summary>
        private static List<string> Files(string extension)
        {
            var names = new List<string>();
            foreach (string path in Directory.GetFiles(UiFolder, "*" + extension))
            {
                names.Add(Path.GetFileName(path));
            }

            names.Sort();
            return names;
        }

        /// <summary>The screen stylesheets: every stylesheet except the shared one.</summary>
        private static List<string> ScreenStylesheets()
        {
            List<string> names = Files(".uss");
            names.Remove(CommonFile);
            return names;
        }

        /// <summary>The shared stylesheet is there, and there are screen stylesheets for it to serve (a guard against a test that checks nothing).</summary>
        [Test]
        public void TheFolder_HasTheSharedStylesheet_AndScreens()
        {
            Assert.That(Files(".uss"), Does.Contain(CommonFile));
            Assert.That(ScreenStylesheets().Count, Is.GreaterThanOrEqualTo(9));
        }

        /// <summary>Every layout that imports a stylesheet imports <c>Common.uss</c> too; the item layouts that the screens clone import none.</summary>
        [Test]
        public void EveryLayoutWithAStyle_ImportsTheSharedStylesheet()
        {
            int withStyle = 0;
            foreach (string name in Files(".uxml"))
            {
                string text = Read(name);
                if (!text.Contains("<Style"))
                {
                    continue;
                }

                withStyle++;
                Assert.That(text, Does.Contain("<Style src=\"Common.uss\""), name + " must import Common.uss");
            }

            Assert.That(withStyle, Is.GreaterThanOrEqualTo(9));
        }

        /// <summary>No screen stylesheet writes a colour: they use the tokens (<c>var(--c-...)</c>) or <c>transparent</c>.</summary>
        [Test]
        public void ScreenStylesheets_WriteNoColourLiteral()
        {
            var literal = new Regex(@"\brgba?\s*\(|#[0-9a-fA-F]{3,8}\b");
            foreach (string name in ScreenStylesheets())
            {
                Assert.That(literal.IsMatch(WithoutComments(Read(name))), Is.False, name + " writes a colour; use a token of Common.uss");
            }
        }

        /// <summary>No screen stylesheet writes a font size: they use the tokens (<c>var(--font-...)</c>).</summary>
        [Test]
        public void ScreenStylesheets_WriteNoFontSizeLiteral()
        {
            var literal = new Regex(@"font-size\s*:\s*\d");
            foreach (string name in ScreenStylesheets())
            {
                Assert.That(literal.IsMatch(WithoutComments(Read(name))), Is.False, name + " writes a font size; use a token of Common.uss");
            }
        }

        /// <summary>Every <c>var(--x)</c> a stylesheet uses is defined in the <c>:root</c> of <c>Common.uss</c>, so no token is mistyped.</summary>
        [Test]
        public void EveryTokenUsed_IsDefinedInTheSharedStylesheet()
        {
            string common = WithoutComments(Read(CommonFile));
            var defined = new HashSet<string>();
            foreach (Match match in Regex.Matches(common, @"(--[a-z0-9-]+)\s*:"))
            {
                defined.Add(match.Groups[1].Value);
            }

            Assert.That(defined.Count, Is.GreaterThan(20));
            foreach (string name in Files(".uss"))
            {
                foreach (Match match in Regex.Matches(WithoutComments(Read(name)), @"var\(\s*(--[a-z0-9-]+)"))
                {
                    Assert.That(defined, Does.Contain(match.Groups[1].Value), name + " uses an undefined token " + match.Groups[1].Value);
                }
            }
        }

        /// <summary>Every button of every layout carries the shared <c>btn</c> class, so its height, radius and look are the shared ones.</summary>
        [Test]
        public void EveryButton_UsesTheSharedButtonClasses()
        {
            int buttons = 0;
            foreach (string name in Files(".uxml"))
            {
                foreach (Match match in Regex.Matches(Read(name), @"<ui:Button\b[^>]*>"))
                {
                    buttons++;
                    Assert.That(match.Value, Does.Match(@"class=""[^""]*\bbtn\b"), name + ": " + match.Value);
                }
            }

            Assert.That(buttons, Is.GreaterThanOrEqualTo(13));
        }

        /// <summary>The touch target is 48 (units are dp, see <c>PanelSettings</c>) and the shared button is never lower or narrower than it (<c>09</c>: touch targets of at least 48 dp).</summary>
        [Test]
        public void TheSharedButton_IsAtLeastTheTouchTarget()
        {
            string common = WithoutComments(Read(CommonFile));
            Match touch = Regex.Match(common, @"--touch\s*:\s*(\d+)px");
            Assert.That(touch.Success, Is.True);
            Assert.That(int.Parse(touch.Groups[1].Value), Is.GreaterThanOrEqualTo(48));
            Match button = Regex.Match(common, @"\.btn\s*\{[^}]*\}");
            Assert.That(button.Success, Is.True);
            Assert.That(button.Value, Does.Contain("min-height: var(--touch)"));
            Assert.That(button.Value, Does.Contain("min-width: var(--touch)"));
        }

        /// <summary>
        /// The screens that sit under the HUD start from the one <c>--hud-height</c> token and not from a number written in each stylesheet (the old toast,
        /// inspector and banner offsets of 224, 124 and 108 depended on the HUD's size without saying so).
        /// </summary>
        [TestCase("Toasts.uss", false)]
        [TestCase("Inspector.uss", true)]
        [TestCase("Research.uss", true)]
        [TestCase("ConnectionBanner.uss", true)]
        public void ScreensUnderTheHud_UseTheHudHeightToken(string name, bool placedUnderTheHud)
        {
            string text = WithoutComments(Read(name));

            if (placedUnderTheHud)
            {
                Assert.That(text, Does.Contain("var(--hud-height)"), name);
            }

            Assert.That(text, Does.Not.Match(@"margin-top\s*:\s*(224|124|108)px"), name);
        }

        /// <summary>The panel is matched on its height at 400 units (880 by 400), so a unit is about one dp on a landscape phone and 48 is a 48 dp touch target.</summary>
        [Test]
        public void ThePanel_IsScaledByHeight_AtTheReferenceTheSizesAreWrittenFor()
        {
            var settings = Resources.Load<PanelSettings>("UI/PanelSettings");

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.scaleMode, Is.EqualTo(PanelScaleMode.ScaleWithScreenSize));
            Assert.That(settings.referenceResolution, Is.EqualTo(new Vector2Int(880, 400)));
            Assert.That(settings.screenMatchMode, Is.EqualTo(PanelScreenMatchMode.MatchWidthOrHeight));
            Assert.That(settings.match, Is.EqualTo(1f));
        }
    }
}
