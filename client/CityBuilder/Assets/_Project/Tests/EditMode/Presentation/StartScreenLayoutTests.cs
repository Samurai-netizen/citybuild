#nullable enable

using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.EditMode.Presentation
{
    /// <summary>
    /// The two layouts <c>GameRoot</c> loads by name (M0-38) hold the labels it writes to. <c>GameRoot</c> looks them up by name when it shows a
    /// status, and logs an error when one is missing; this test finds a renamed label before a player does.
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
