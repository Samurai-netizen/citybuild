#nullable enable

using CityBuilder.Client.Presentation.Build;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.EditMode.Presentation
{
    /// <summary>
    /// The two layouts <c>BuildMenu</c> loads by name (M0-45) hold the elements it looks up. It logs an error when one is missing; this test
    /// finds a renamed element before a player does.
    /// </summary>
    public sealed class BuildMenuLayoutTests
    {
        /// <summary>The menu layout has the root, the sheet, the scroll view for the cards and the button, and the sheet starts closed.</summary>
        [Test]
        public void MenuLayout_HasTheRootTheSheetTheCardsAndTheButton()
        {
            VisualElement root = Layout("UI/BuildMenu");

            Assert.That(root.Q<VisualElement>(BuildMenu.RootName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(BuildMenu.PanelName), Is.Not.Null);
            Assert.That(root.Q<ScrollView>(BuildMenu.CardsName), Is.Not.Null);
            Assert.That(root.Q<Button>(BuildMenu.ToggleName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(BuildMenu.PanelName).ClassListContains("build-panel--hidden"), Is.True);
        }

        /// <summary>The card layout has the card and the seven labels the menu writes to.</summary>
        [Test]
        public void CardLayout_HasTheCardAndItsLabels()
        {
            VisualElement root = Layout("UI/BuildCard");

            Assert.That(root.Q<VisualElement>(BuildMenu.CardName), Is.Not.Null);
            foreach (string name in new[] { "card-name", "card-cost", "card-time", "card-workers", "card-recipe", "card-lock", "card-hint" })
            {
                Assert.That(root.Q<Label>(name), Is.Not.Null, name);
            }
        }

        /// <summary>The root takes no pointer itself, so only the button and the sheet block the ground.</summary>
        [Test]
        public void MenuLayout_RootIgnoresThePointer()
        {
            VisualElement root = Layout("UI/BuildMenu");

            Assert.That(root.Q<VisualElement>(BuildMenu.RootName).pickingMode, Is.EqualTo(PickingMode.Ignore));
        }

        /// <summary>Loads a layout by its <c>Resources</c> path and builds its tree.</summary>
        private static VisualElement Layout(string path)
        {
            var asset = Resources.Load<VisualTreeAsset>(path);
            Assert.That(asset, Is.Not.Null, "Assets/_Project/Resources/" + path + ".uxml is missing");
            return asset.Instantiate();
        }
    }
}
