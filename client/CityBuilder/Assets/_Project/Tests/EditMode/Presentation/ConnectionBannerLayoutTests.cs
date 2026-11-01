#nullable enable

using CityBuilder.Client.Presentation.Connection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.EditMode.Presentation
{
    /// <summary>
    /// The layout <c>ConnectionBanner</c> loads by name (M0-55) holds every element it looks up, takes no pointer at its root, and starts with the
    /// banner and the blocking screen hidden. The component logs an error when an element is missing; this test finds a renamed one before a player does.
    /// </summary>
    public sealed class ConnectionBannerLayoutTests
    {
        /// <summary>Every name the component looks up exists, with the type it expects.</summary>
        [Test]
        public void Layout_HasEveryElementTheComponentLooksUp()
        {
            VisualElement root = Layout();

            Assert.That(root.Q<VisualElement>(ConnectionBanner.RootName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(ConnectionBanner.BannerName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(ConnectionBanner.BlockerName), Is.Not.Null);
            Assert.That(root.Q<Label>(ConnectionBanner.MessageName), Is.Not.Null);
            Assert.That(root.Q<Label>(ConnectionBanner.SyncedName), Is.Not.Null);
            Assert.That(root.Q<Label>(ConnectionBanner.BlockerTitleName), Is.Not.Null);
            Assert.That(root.Q<Label>(ConnectionBanner.BlockerMessageName), Is.Not.Null);
            Assert.That(root.Q<Button>(ConnectionBanner.ActionName), Is.Not.Null);
            Assert.That(root.Q<Button>(ConnectionBanner.BlockerActionName), Is.Not.Null);
        }

        /// <summary>The root takes no pointer, so a press beside the banner reaches the ground and the screens below; the banner and the blocker do take it.</summary>
        [Test]
        public void Layout_TheRootTakesNoPointer_TheBannerAndTheBlockerDo()
        {
            VisualElement root = Layout();

            Assert.That(root.Q<VisualElement>(ConnectionBanner.RootName).pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(root.Q<VisualElement>(ConnectionBanner.BannerName).pickingMode, Is.EqualTo(PickingMode.Position));
            Assert.That(root.Q<VisualElement>(ConnectionBanner.BlockerName).pickingMode, Is.EqualTo(PickingMode.Position));
        }

        /// <summary>Both parts start hidden: a connected client never flashes a banner before the first model arrives.</summary>
        [Test]
        public void Layout_StartsWithBothPartsHidden()
        {
            VisualElement root = Layout();

            Assert.That(root.Q<VisualElement>(ConnectionBanner.BannerName).ClassListContains("connection-banner--hidden"), Is.True);
            Assert.That(root.Q<VisualElement>(ConnectionBanner.BlockerName).ClassListContains("connection-blocker--hidden"), Is.True);
        }

        /// <summary>Loads the layout by its <c>Resources</c> path and builds its tree.</summary>
        private static VisualElement Layout()
        {
            var asset = Resources.Load<VisualTreeAsset>("UI/ConnectionBanner");
            Assert.That(asset, Is.Not.Null, "Assets/_Project/Resources/UI/ConnectionBanner.uxml is missing");
            return asset.Instantiate();
        }
    }
}
