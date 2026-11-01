#nullable enable

using CityBuilder.Client.Presentation.WelcomeBack;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.EditMode.Presentation
{
    /// <summary>
    /// The layout <c>WelcomeBackPanel</c> loads by name (M0-53) holds the elements it looks up. It logs an error when one is missing; these tests find a
    /// renamed element before a player does.
    /// </summary>
    public sealed class WelcomeBackLayoutTests
    {
        /// <summary>The layout has the root, the scrim, the card, the sections, the three labels and the Continue button, and the scrim starts hidden.</summary>
        [Test]
        public void Layout_HasEveryElementTheComponentLooksUp_AndStartsHidden()
        {
            VisualElement root = Layout();

            foreach (string name in new[] { WelcomeBackPanel.RootName, WelcomeBackPanel.ScrimName, WelcomeBackPanel.CardName, WelcomeBackPanel.SectionsName })
            {
                Assert.That(root.Q<VisualElement>(name), Is.Not.Null, name);
            }

            foreach (string name in new[] { WelcomeBackPanel.TitleName, WelcomeBackPanel.EmptyName, WelcomeBackPanel.CapNoteName })
            {
                Assert.That(root.Q<Label>(name), Is.Not.Null, name);
            }

            Assert.That(root.Q<Button>(WelcomeBackPanel.ContinueName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(WelcomeBackPanel.ScrimName).ClassListContains("welcome-scrim--hidden"), Is.True);
        }

        /// <summary>The root takes no pointer itself; only the scrim does, and it is hidden until a report is shown.</summary>
        [Test]
        public void Layout_RootIgnoresThePointer()
        {
            Assert.That(Layout().Q<VisualElement>(WelcomeBackPanel.RootName).pickingMode, Is.EqualTo(PickingMode.Ignore));
        }

        /// <summary>Loads the layout under <c>Resources</c> and builds its tree.</summary>
        private static VisualElement Layout()
        {
            var asset = Resources.Load<VisualTreeAsset>("UI/WelcomeBack");
            Assert.That(asset, Is.Not.Null, "Assets/_Project/Resources/UI/WelcomeBack.uxml is missing");
            return asset.Instantiate();
        }
    }
}
