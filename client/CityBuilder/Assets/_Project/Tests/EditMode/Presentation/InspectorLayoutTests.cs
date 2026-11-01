#nullable enable

using CityBuilder.Client.Presentation.Inspector;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.EditMode.Presentation
{
    /// <summary>
    /// The layout <c>BuildingInspector</c> loads by name (M0-51) holds the elements it looks up. It logs an error when one is missing; this test
    /// finds a renamed element before a player does.
    /// </summary>
    public sealed class InspectorLayoutTests
    {
        /// <summary>The layout has every label, the stepper row, the four buttons and the icon, and the panel starts hidden.</summary>
        [Test]
        public void Layout_HasEveryElementTheComponentLooksUp_AndStartsHidden()
        {
            VisualElement root = Layout();

            foreach (string name in new[] { BuildingInspector.RootName, BuildingInspector.PanelName, BuildingInspector.IconName, BuildingInspector.StepperName })
            {
                Assert.That(root.Q<VisualElement>(name), Is.Not.Null, name);
            }

            foreach (string name in new[]
            {
                BuildingInspector.NameName, BuildingInspector.LevelName, BuildingInspector.StatusName, BuildingInspector.TimeName,
                BuildingInspector.RecipeName, BuildingInspector.RatesName, BuildingInspector.WorkersName, BuildingInspector.PreviewName,
                BuildingInspector.StepperNoteName, BuildingInspector.UpgradeTitleName, BuildingInspector.UpgradeCostName,
                BuildingInspector.UpgradeTimeName, BuildingInspector.UpgradeGainName, BuildingInspector.UpgradeNoteName,
            })
            {
                Assert.That(root.Q<Label>(name), Is.Not.Null, name);
            }

            foreach (string name in new[] { BuildingInspector.MinusName, BuildingInspector.PlusName, BuildingInspector.UpgradeName, BuildingInspector.CloseName })
            {
                Assert.That(root.Q<Button>(name), Is.Not.Null, name);
            }

            Assert.That(root.Q<VisualElement>(BuildingInspector.PanelName).ClassListContains("inspector-panel--hidden"), Is.True);
        }

        /// <summary>The root takes no pointer itself, so only the panel blocks the ground and the empty screen still pans and taps.</summary>
        [Test]
        public void Layout_RootIgnoresThePointer()
        {
            Assert.That(Layout().Q<VisualElement>(BuildingInspector.RootName).pickingMode, Is.EqualTo(PickingMode.Ignore));
        }

        /// <summary>Loads the layout by its <c>Resources</c> path and builds its tree.</summary>
        private static VisualElement Layout()
        {
            var asset = Resources.Load<VisualTreeAsset>("UI/Inspector");
            Assert.That(asset, Is.Not.Null, "Assets/_Project/Resources/UI/Inspector.uxml is missing");
            return asset.Instantiate();
        }
    }
}
