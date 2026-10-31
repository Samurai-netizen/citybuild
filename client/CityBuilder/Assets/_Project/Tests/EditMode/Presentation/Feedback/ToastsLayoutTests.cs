#nullable enable

using CityBuilder.Client.Presentation.Feedback;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Feedback
{
    /// <summary>
    /// The layout <c>ToastHost</c> loads by name (M0-47) holds the elements it looks up and takes no pointer. The host logs an error when an
    /// element is missing; this test finds a renamed one before a player does.
    /// </summary>
    public sealed class ToastsLayoutTests
    {
        /// <summary>The layout has the root and the list.</summary>
        [Test]
        public void Layout_HasTheRootAndTheList()
        {
            VisualElement root = Layout();

            Assert.That(root.Q<VisualElement>(ToastHost.RootName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(ToastHost.ListName), Is.Not.Null);
        }

        /// <summary>Neither the root nor the list picks, so a press under a toast reaches the ground and the screens below.</summary>
        [Test]
        public void Layout_TakesNoPointer()
        {
            VisualElement root = Layout();

            Assert.That(root.Q<VisualElement>(ToastHost.RootName).pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(root.Q<VisualElement>(ToastHost.ListName).pickingMode, Is.EqualTo(PickingMode.Ignore));
        }

        /// <summary>Loads the layout by its <c>Resources</c> path and builds its tree.</summary>
        private static VisualElement Layout()
        {
            var asset = Resources.Load<VisualTreeAsset>("UI/Toasts");
            Assert.That(asset, Is.Not.Null, "Assets/_Project/Resources/UI/Toasts.uxml is missing");
            return asset.Instantiate();
        }
    }
}
