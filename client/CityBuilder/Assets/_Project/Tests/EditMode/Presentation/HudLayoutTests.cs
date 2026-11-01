#nullable enable

using CityBuilder.Client.Presentation.Hud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.EditMode.Presentation
{
    /// <summary>
    /// The two layouts <c>HudView</c> loads by name (M0-49) hold the elements it looks up and take no pointer. It logs an error when an element
    /// is missing; this test finds a renamed element before a player does.
    /// </summary>
    public sealed class HudLayoutTests
    {
        /// <summary>The HUD layout has the root, the chip row, the counters, the ring, the research labels, the era badge and the connection indicator.</summary>
        [Test]
        public void HudLayout_HasEveryElementTheViewLooksUp()
        {
            VisualElement root = Layout("UI/Hud");

            foreach (string name in new[] { HudView.RootName, HudView.ChipListName, HudView.RingName, HudView.ConnectionDotName })
            {
                Assert.That(root.Q<VisualElement>(name), Is.Not.Null, name);
            }

            foreach (string name in new[]
            {
                HudView.WorkersName, HudView.BuildersName, HudView.ResearchNameName, HudView.ResearchTimeName, HudView.EraName,
                HudView.ConnectionTextName,
            })
            {
                Assert.That(root.Q<Label>(name), Is.Not.Null, name);
            }
        }

        /// <summary>The chip layout has the chip and the four labels the view writes to.</summary>
        [Test]
        public void ChipLayout_HasTheChipAndItsLabels()
        {
            VisualElement root = Layout("UI/HudChip");

            Assert.That(root.Q<VisualElement>(HudView.ChipName), Is.Not.Null);
            foreach (string name in new[] { HudView.ChipIconName, HudView.ChipStockName, HudView.ChipCapName, HudView.ChipRateName })
            {
                Assert.That(root.Q<Label>(name), Is.Not.Null, name);
            }
        }

        /// <summary>No element of either layout takes the pointer, so a press on the HUD is still the ground's.</summary>
        [TestCase("UI/Hud")]
        [TestCase("UI/HudChip")]
        public void EveryElement_IgnoresThePointer(string path)
        {
            foreach (VisualElement element in Layout(path).Query<VisualElement>().ToList())
            {
                // The template container that Instantiate wraps the tree in is not part of the layout file.
                if (element is TemplateContainer)
                {
                    continue;
                }

                Assert.That(element.pickingMode, Is.EqualTo(PickingMode.Ignore), element.name + " (" + element.GetType().Name + ")");
            }
        }

        /// <summary>Loads the layout at <paramref name="path"/> from <c>Resources</c> and builds its tree.</summary>
        private static VisualElement Layout(string path)
        {
            var asset = Resources.Load<VisualTreeAsset>(path);
            Assert.That(asset, Is.Not.Null, "Assets/_Project/Resources/" + path + ".uxml is missing");
            return asset.Instantiate();
        }
    }
}
