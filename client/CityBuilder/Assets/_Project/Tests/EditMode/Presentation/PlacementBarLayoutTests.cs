#nullable enable

using CityBuilder.Client.Presentation.Build;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.EditMode.Presentation
{
    /// <summary>
    /// The layout <c>PlacementBar</c> loads by name (M0-46) holds the elements it looks up. It logs an error when one is missing; this test
    /// finds a renamed element before a player does.
    /// </summary>
    public sealed class PlacementBarLayoutTests
    {
        /// <summary>The layout has the root, the panel, the three labels and the two buttons, and the panel starts hidden.</summary>
        [Test]
        public void Layout_HasThePanelTheLabelsAndTheButtons_AndStartsHidden()
        {
            VisualElement root = Layout();

            Assert.That(root.Q<VisualElement>(PlacementBar.RootName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(PlacementBar.PanelName), Is.Not.Null);
            foreach (string name in new[] { PlacementBar.PromptName, PlacementBar.HintName, PlacementBar.ErrorName })
            {
                Assert.That(root.Q<Label>(name), Is.Not.Null, name);
            }

            Assert.That(root.Q<Button>(PlacementBar.PlaceName), Is.Not.Null);
            Assert.That(root.Q<Button>(PlacementBar.CancelName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(PlacementBar.PanelName).ClassListContains("placement-panel--hidden"), Is.True);
        }

        /// <summary>The root takes no pointer itself, so only the panel blocks the ground and the empty screen still pans and taps.</summary>
        [Test]
        public void Layout_RootIgnoresThePointer()
        {
            Assert.That(Layout().Q<VisualElement>(PlacementBar.RootName).pickingMode, Is.EqualTo(PickingMode.Ignore));
        }

        /// <summary>Loads the layout by its <c>Resources</c> path and builds its tree.</summary>
        private static VisualElement Layout()
        {
            var asset = Resources.Load<VisualTreeAsset>("UI/PlacementBar");
            Assert.That(asset, Is.Not.Null, "Assets/_Project/Resources/UI/PlacementBar.uxml is missing");
            return asset.Instantiate();
        }
    }
}
