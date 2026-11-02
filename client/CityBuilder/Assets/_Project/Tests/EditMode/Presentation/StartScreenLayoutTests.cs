#nullable enable

using CityBuilder.Client.Presentation.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.EditMode.Presentation
{
    /// <summary>
    /// The two layouts <c>GameRoot</c> loads by name (M0-38) hold the labels it writes to. <c>GameRoot</c> looks them up by name when it shows a
    /// status, and logs an error when one is missing; this test finds a renamed label before a player does. Since M0-59 both have a full-screen
    /// element that <c>ConnectionStatusView</c> pads by the safe area.
    /// </summary>
    public sealed class StartScreenLayoutTests
    {
        /// <summary>The status layout has the label <c>connection-status</c>.</summary>
        [Test]
        public void ConnectionStatusLayout_HasTheStatusLabel()
        {
            VisualElement root = Layout("UI/ConnectionStatus");

            Assert.That(root.Q<Label>("connection-status"), Is.Not.Null);
        }

        /// <summary>The status label sits in the full-screen root that is padded by the safe area, and that root takes no pointer, so the ground keeps its presses.</summary>
        [Test]
        public void ConnectionStatusLayout_HasASafeAreaRoot_ThatIgnoresThePointer()
        {
            VisualElement? statusRoot = Layout("UI/ConnectionStatus").Q<VisualElement>(ConnectionStatusView.StatusRootName);

            Assert.That(statusRoot, Is.Not.Null);
            Assert.That(statusRoot!.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(statusRoot.Q<Label>(ConnectionStatusView.StatusLabelName), Is.Not.Null);
        }

        /// <summary>The fatal error layout has the full-screen panel that is padded by the safe area, with the title and the lines in it.</summary>
        [Test]
        public void FatalErrorLayout_HasASafeAreaPanel_AroundItsLabels()
        {
            VisualElement? panel = Layout("UI/FatalError").Q<VisualElement>(ConnectionStatusView.FatalPanelName);

            Assert.That(panel, Is.Not.Null);
            Assert.That(panel!.Q<Label>(ConnectionStatusView.FatalTitleName), Is.Not.Null);
        }

        /// <summary>The fatal error layout has the title and the details labels.</summary>
        [Test]
        public void FatalErrorLayout_HasTheTitleAndTheDetailsLabels()
        {
            VisualElement root = Layout("UI/FatalError");

            Assert.That(root.Q<Label>("fatal-error-title"), Is.Not.Null);
            Assert.That(root.Q<Label>("fatal-error-details"), Is.Not.Null);
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
