#nullable enable

using CityBuilder.Client.Presentation.Feedback;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.EditMode.Presentation
{
    /// <summary>
    /// The layout <see cref="ReenergizePrompt"/> loads by name (<c>ReenergizePrompt</c>, M0-C-5) holds the elements it looks up, starts hidden,
    /// lets the pointer through while hidden, and its two buttons are full buttons of the shared style.
    /// </summary>
    public sealed class ReenergizePromptLayoutTests
    {
        /// <summary>The layout has every element the prompt looks up, the scrim starts hidden and the root takes no pointer.</summary>
        [Test]
        public void Layout_HasEveryElementThePromptLooksUp_AndStartsHidden()
        {
            VisualElement root = Load();

            Assert.That(root.Q<VisualElement>(ReenergizePrompt.RootName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(ReenergizePrompt.ScrimName), Is.Not.Null);
            Assert.That(root.Q<Label>(ReenergizePrompt.TitleName), Is.Not.Null);
            Assert.That(root.Q<Label>(ReenergizePrompt.MessageName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(ReenergizePrompt.ScrimName).ClassListContains(ReenergizePrompt.HiddenClass), Is.True);
            Assert.That(root.Q<VisualElement>(ReenergizePrompt.RootName).pickingMode, Is.EqualTo(PickingMode.Ignore));
            foreach (string name in new[] { ReenergizePrompt.ReenergizeName, ReenergizePrompt.NotNowName })
            {
                Button button = root.Q<Button>(name);
                Assert.That(button, Is.Not.Null, name);
                Assert.That(button.ClassListContains("btn"), Is.True, name);
            }
        }

        /// <summary>The message is the owner's sentence (M0-C-5).</summary>
        [Test]
        public void Message_IsTheOwnersSentence()
        {
            Assert.That(ReenergizePrompt.MessageText, Is.EqualTo("You need to re-energize your couriers to do that"));
        }

        /// <summary>Loads the layout by its <c>Resources</c> path and builds its tree.</summary>
        private static VisualElement Load()
        {
            var asset = Resources.Load<VisualTreeAsset>("UI/ReenergizePrompt");
            Assert.That(asset, Is.Not.Null, "Assets/_Project/Resources/UI/ReenergizePrompt.uxml is missing");
            return asset.Instantiate();
        }
    }
}
