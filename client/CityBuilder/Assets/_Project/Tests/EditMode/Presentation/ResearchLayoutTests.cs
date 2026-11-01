#nullable enable

using CityBuilder.Client.Presentation.Research;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.EditMode.Presentation
{
    /// <summary>
    /// The layouts <c>ResearchScreen</c> loads by name (M0-52) hold the elements it looks up. It logs an error when one is missing; these tests
    /// find a renamed element before a player does.
    /// </summary>
    public sealed class ResearchLayoutTests
    {
        /// <summary>The screen layout has the root, the panel, the labels, the columns and the two buttons, and the panel starts closed.</summary>
        [Test]
        public void Screen_HasEveryElementTheComponentLooksUp_AndStartsClosed()
        {
            VisualElement root = Layout("UI/Research");

            foreach (string name in new[] { ResearchScreen.RootName, ResearchScreen.PanelName, ResearchScreen.ColumnsName })
            {
                Assert.That(root.Q<VisualElement>(name), Is.Not.Null, name);
            }

            foreach (string name in new[] { ResearchScreen.EraName, ResearchScreen.RuleName })
            {
                Assert.That(root.Q<Label>(name), Is.Not.Null, name);
            }

            foreach (string name in new[] { ResearchScreen.ToggleName, ResearchScreen.CloseName })
            {
                Assert.That(root.Q<Button>(name), Is.Not.Null, name);
            }

            Assert.That(root.Q<VisualElement>(ResearchScreen.PanelName).ClassListContains("research-panel--hidden"), Is.True);
        }

        /// <summary>The root takes no pointer itself, so only the panel and the button block the ground.</summary>
        [Test]
        public void Screen_RootIgnoresThePointer()
        {
            Assert.That(Layout("UI/Research").Q<VisualElement>(ResearchScreen.RootName).pickingMode, Is.EqualTo(PickingMode.Ignore));
        }

        /// <summary>The node layout has the root, the picture, the ring, the labels and the Finance button.</summary>
        [Test]
        public void Node_HasEveryElementTheComponentLooksUp()
        {
            VisualElement root = Layout("UI/ResearchNode");

            foreach (string name in new[] { ResearchScreen.NodeName, ResearchScreen.NodeIconName, ResearchScreen.NodeRingName })
            {
                Assert.That(root.Q<VisualElement>(name), Is.Not.Null, name);
            }

            foreach (string name in new[]
            {
                ResearchScreen.NodeNameName, ResearchScreen.NodeEffectName, ResearchScreen.NodeCostName, ResearchScreen.NodeTimeName,
                ResearchScreen.NodeStatusName, ResearchScreen.NodeNoteName,
            })
            {
                Assert.That(root.Q<Label>(name), Is.Not.Null, name);
            }

            Assert.That(root.Q<Button>(ResearchScreen.NodeFinanceName), Is.Not.Null);
        }

        /// <summary>Loads the layout at <paramref name="path"/> under <c>Resources</c> and builds its tree.</summary>
        private static VisualElement Layout(string path)
        {
            var asset = Resources.Load<VisualTreeAsset>(path);
            Assert.That(asset, Is.Not.Null, "Assets/_Project/Resources/" + path + ".uxml is missing");
            return asset.Instantiate();
        }
    }
}
