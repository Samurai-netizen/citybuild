#nullable enable

using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CityBuilder.Client.Presentation.Routes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.EditMode.Presentation
{
    /// <summary>
    /// The layouts <see cref="RouteEditorScreen"/> loads by name (<c>RouteEditor</c>, <c>RouteStop</c>, <c>RouteAction</c>) hold the elements it
    /// looks up, and keep the compact layout of the M0-C rework (smoke test S8, issue M0-C-1): the title alone on its row, a stop's tools on its
    /// title row, the walk between the cards, list buttons that read at body size, and a panel of half the landscape screen at most.
    /// </summary>
    public sealed class RouteEditorLayoutTests
    {
        /// <summary>The smallest a list button may be, the touch floor of the UI rules.</summary>
        private const int MinButtonSize = 48;

        /// <summary>The class every button in the stop list carries.</summary>
        private const string CompactClass = "route-compact";

        /// <summary>The editor layout has every element the screen looks up, the panel starts hidden and the root takes no pointer.</summary>
        [Test]
        public void Layout_HasEveryElementTheScreenLooksUp_AndStartsHidden()
        {
            VisualElement root = Load("RouteEditor");

            foreach (string name in new[] { RouteEditorScreen.RootName, RouteEditorScreen.PanelName, RouteEditorScreen.StopsName })
            {
                Assert.That(root.Q<VisualElement>(name), Is.Not.Null, name);
            }

            foreach (string name in new[]
            {
                RouteEditorScreen.TitleName, RouteEditorScreen.SummaryName, RouteEditorScreen.NoteName, RouteEditorScreen.AmountNoteName,
                RouteEditorScreen.HintName,
            })
            {
                Assert.That(root.Q<Label>(name), Is.Not.Null, name);
            }

            foreach (string name in new[] { RouteEditorScreen.SlotName, RouteEditorScreen.CloseName, RouteEditorScreen.RevertName, RouteEditorScreen.ConfirmName })
            {
                Assert.That(root.Q<Button>(name), Is.Not.Null, name);
            }

            Assert.That(root.Q<ScrollView>(RouteEditorScreen.ScrollName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(RouteEditorScreen.PanelName).ClassListContains("route-panel--hidden"), Is.True);
            Assert.That(root.Q<VisualElement>(RouteEditorScreen.RootName).pickingMode, Is.EqualTo(PickingMode.Ignore));
        }

        /// <summary>The stop and action layouts have every element the screen looks up.</summary>
        [Test]
        public void ItemLayouts_HaveEveryElementTheScreenLooksUp()
        {
            VisualElement stop = Load("RouteStop");
            Assert.That(stop.Q<VisualElement>(RouteEditorScreen.StopName), Is.Not.Null);
            Assert.That(stop.Q<VisualElement>(RouteEditorScreen.StopActionsName), Is.Not.Null);
            Assert.That(stop.Q<Label>(RouteEditorScreen.StopTitleName), Is.Not.Null);
            Assert.That(stop.Q<Label>(RouteEditorScreen.StopWalkName), Is.Not.Null);
            foreach (string name in new[]
            {
                RouteEditorScreen.StopAddActionName, RouteEditorScreen.StopUpName, RouteEditorScreen.StopDownName, RouteEditorScreen.StopRemoveName,
            })
            {
                Assert.That(stop.Q<Button>(name), Is.Not.Null, name);
            }

            VisualElement action = Load("RouteAction");
            Assert.That(action.Q<VisualElement>(RouteEditorScreen.ActionName), Is.Not.Null);
            foreach (string name in new[]
            {
                RouteEditorScreen.ActionKindName, RouteEditorScreen.ActionItemName, RouteEditorScreen.ActionAmountName, RouteEditorScreen.ActionRemoveName,
            })
            {
                Assert.That(action.Q<Button>(name), Is.Not.Null, name);
            }
        }

        /// <summary>The title shares its row only with the close button, so a long courier title never runs under the slot button or the summary.</summary>
        [Test]
        public void Header_TitleSharesItsRowOnlyWithClose()
        {
            VisualElement root = Load("RouteEditor");
            VisualElement row = root.Q<Label>(RouteEditorScreen.TitleName).parent;

            Assert.That(row.childCount, Is.EqualTo(2));
            Assert.That(row.Q<Button>(RouteEditorScreen.CloseName), Is.Not.Null);
            Assert.That(root.Q<Button>(RouteEditorScreen.SlotName).parent, Is.Not.SameAs(row));
        }

        /// <summary>A stop's up, down and remove buttons sit on its title row, not on a row of their own, so a card stays short.</summary>
        [Test]
        public void Stop_ToolsShareTheTitleRow()
        {
            VisualElement stop = Load("RouteStop");
            VisualElement row = stop.Q<Label>(RouteEditorScreen.StopTitleName).parent;

            foreach (string name in new[] { RouteEditorScreen.StopUpName, RouteEditorScreen.StopDownName, RouteEditorScreen.StopRemoveName })
            {
                Assert.That(row.Q<Button>(name), Is.Not.Null, name);
            }
        }

        /// <summary>The walk to the next stop sits under the card's box, between two cards, where the courier walks.</summary>
        [Test]
        public void Stop_WalkSitsOutsideTheCardBox()
        {
            VisualElement stop = Load("RouteStop");
            VisualElement title = stop.Q<Label>(RouteEditorScreen.StopTitleName);
            Label walk = stop.Q<Label>(RouteEditorScreen.StopWalkName);

            for (VisualElement? parent = walk.parent; parent != null; parent = parent.parent)
            {
                Assert.That(parent.ClassListContains("box"), Is.False, "the walk label is inside the card box");
            }

            Assert.That(title.parent.parent.ClassListContains("box"), Is.True, "the title row is in the card box");
        }

        /// <summary>Every button in the stop list is compact, and the compact rule keeps the touch floor and body-size words.</summary>
        [Test]
        public void ListButtons_AreCompact_AtBodySizeAndTheTouchFloor()
        {
            foreach (string layout in new[] { "RouteStop", "RouteAction" })
            {
                Load(layout).Query<Button>().ForEach(button => Assert.That(button.ClassListContains(CompactClass), Is.True, layout + " " + button.name));
            }

            string rule = Rule("." + CompactClass);
            Assert.That(rule, Does.Contain("font-size: var(--font-md)"));
            Assert.That(Pixels(rule, "min-width"), Is.GreaterThanOrEqualTo(MinButtonSize));
            Assert.That(Pixels(rule, "min-height"), Is.GreaterThanOrEqualTo(MinButtonSize));
        }

        /// <summary>
        /// The landscape panel is half the screen at most, with no fixed minimum that could push it over on a narrow screen (the map keeps the
        /// rest for its taps; the PlayMode test <c>EditRoutes_Open_LeavesTheMapFreeForTaps</c> checks it laid out), and the stop list takes
        /// the height the panel has left.
        /// </summary>
        [Test]
        public void Panel_IsHalfTheScreenAtMost_AndTheListTakesTheRestOfTheHeight()
        {
            string panel = Rule(".route-panel");
            Match width = Regex.Match(panel, @"(^|[;\s])width\s*:\s*(\d+)%");
            Assert.That(width.Success, Is.True, "width in % in { " + panel + " }");
            Assert.That(int.Parse(width.Groups[2].Value, CultureInfo.InvariantCulture), Is.LessThanOrEqualTo(50));
            Assert.That(Regex.IsMatch(panel, @"(^|[;\s])min-width\s*:\s*[1-9]"), Is.False, "no fixed minimum width in { " + panel + " }");
            Assert.That(Rule(".route-scroll"), Does.Contain("flex-grow: 1"));
        }

        /// <summary>Loads a layout by its <c>Resources</c> path and builds its tree.</summary>
        private static VisualElement Load(string name)
        {
            var asset = Resources.Load<VisualTreeAsset>("UI/" + name);
            Assert.That(asset, Is.Not.Null, "Assets/_Project/Resources/UI/" + name + ".uxml is missing");
            return asset.Instantiate();
        }

        /// <summary>The body of the rule of <c>RouteEditor.uss</c> whose selector is exactly <paramref name="selector"/>; fails when there is none.</summary>
        private static string Rule(string selector)
        {
            string text = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Resources", "UI", "RouteEditor.uss"));
            text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            Match match = Regex.Match(text, @"(^|\})\s*" + Regex.Escape(selector) + @"\s*\{([^}]*)\}");
            Assert.That(match.Success, Is.True, "RouteEditor.uss has no rule " + selector);
            return match.Groups[2].Value;
        }

        /// <summary>The number of pixels of <paramref name="property"/> in a rule body; fails when it is not there.</summary>
        private static int Pixels(string ruleBody, string property)
        {
            Match match = Regex.Match(ruleBody, @"(^|[;\s])" + Regex.Escape(property) + @"\s*:\s*(\d+)px");
            Assert.That(match.Success, Is.True, property + " in { " + ruleBody + " }");
            return int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        }
    }
}
