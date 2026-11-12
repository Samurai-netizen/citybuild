#nullable enable

using CityBuilder.Client.Presentation.Ui;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Ui
{
    /// <summary>
    /// <see cref="UiRoot.HideInDock"/>, which hides the dock's Build and Research buttons while a building's menu is open (CR-0008 D22, the
    /// owner's playtest point 4; gone instead of faded since playtest round 4, the owner's change): the button is invisible and can't be pressed,
    /// and it comes back the same way.
    /// </summary>
    public sealed class DockHideTests
    {
        /// <summary>Hiding makes the button invisible and disables it; showing it again undoes both.</summary>
        [Test]
        public void HideInDock_HidesAndDisables_ShowRestores()
        {
            var button = new Button { text = "Build" };

            UiRoot.HideInDock(button, true);
            Assert.That((button.style.visibility.value, button.enabledSelf), Is.EqualTo((Visibility.Hidden, false)));

            UiRoot.HideInDock(button, false);
            Assert.That((button.style.visibility.keyword, button.enabledSelf), Is.EqualTo((StyleKeyword.Null, true)));
        }
    }
}
