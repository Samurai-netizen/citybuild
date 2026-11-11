#nullable enable

using CityBuilder.Client.Presentation.Ui;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Ui
{
    /// <summary>
    /// <see cref="UiRoot.Dim"/>, which fades the dock's Build and Research buttons while a building's menu is open (CR-0008 D22, the owner's
    /// playtest point 4): the button gets the dimmed class and can't be pressed, and it comes back the same way.
    /// </summary>
    public sealed class DockDimTests
    {
        /// <summary>Dimming adds the class and disables the button; undimming removes both.</summary>
        [Test]
        public void Dim_AddsTheClassAndDisables_UndimRestores()
        {
            var button = new Button { text = "Build" };

            UiRoot.Dim(button, true);
            Assert.That((button.ClassListContains(UiRoot.DimmedClass), button.enabledSelf), Is.EqualTo((true, false)));

            UiRoot.Dim(button, false);
            Assert.That((button.ClassListContains(UiRoot.DimmedClass), button.enabledSelf), Is.EqualTo((false, true)));
        }
    }
}
