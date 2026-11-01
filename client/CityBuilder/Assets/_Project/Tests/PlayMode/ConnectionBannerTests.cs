#nullable enable

using System.Collections;
using CityBuilder.Client.Core;
using CityBuilder.Client.Presentation.Connection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Tests.PlayMode
{
    /// <summary>
    /// The connection banner and the blocking screen in a running scene (M0-55): the real <c>UI/ConnectionBanner</c> layout over a
    /// <see cref="ConnectionBannerModel"/>. The banner shows its words for the offline, server-error and signed-out states, the blocker covers the
    /// screen for an incompatible version, a connected client shows neither, and a press on the action only raises the event, and only when it works.
    /// </summary>
    public sealed class ConnectionBannerTests
    {
        /// <summary>The object the banner is made under.</summary>
        private GameObject _parent = null!;

        /// <summary>The component under test.</summary>
        private ConnectionBanner _banner = null!;

        /// <summary>How many times the action was requested.</summary>
        private int _requested;

        /// <summary>Before each test: a banner that has shown nothing yet; waits a frame for the document's tree.</summary>
        [UnitySetUp]
        public IEnumerator CreateBanner()
        {
            _parent = new GameObject("Test connection parent");
            _banner = ConnectionBanner.Create(_parent.transform)!;
            _requested = 0;
            _banner.ActionRequested += () => _requested++;
            yield return null;
        }

        /// <summary>After each test: destroys everything.</summary>
        [TearDown]
        public void Destroy()
        {
            Object.DestroyImmediate(_parent);
        }

        /// <summary>Whether the banner strip is shown (not hidden by its class).</summary>
        private bool BannerShown() => !Element(ConnectionBanner.BannerName).ClassListContains("connection-banner--hidden");

        /// <summary>Whether the blocking screen is shown.</summary>
        private bool BlockerShown() => !Element(ConnectionBanner.BlockerName).ClassListContains("connection-blocker--hidden");

        /// <summary>An element of the tree by name.</summary>
        private VisualElement Element(string name) => _banner.Root!.Q<VisualElement>(name);

        /// <summary>A label of the tree by name.</summary>
        private Label LabelOf(string name) => _banner.Root!.Q<Label>(name);

        /// <summary>A button of the tree by name.</summary>
        private Button ButtonOf(string name) => _banner.Root!.Q<Button>(name);

        /// <summary>Before any model neither part is shown, and no screen position is the banner's.</summary>
        [UnityTest]
        public IEnumerator BeforeAnyModel_NothingIsShown()
        {
            yield return null;

            Assert.That(BannerShown(), Is.False);
            Assert.That(BlockerShown(), Is.False);
            Assert.That(_banner.IsOverUi(new Vector2(Screen.width / 2f, Screen.height / 2f)), Is.False);
        }

        /// <summary>An offline model shows the banner with its message, the sync age and "Try again"; the blocker stays hidden.</summary>
        [UnityTest]
        public IEnumerator Offline_ShowsTheBannerWithItsWords()
        {
            _banner.Show(ConnectionBannerModel.Create(ConnectionState.Offline, 200, false));
            yield return null;

            Assert.That(BannerShown(), Is.True);
            Assert.That(BlockerShown(), Is.False);
            Assert.That(LabelOf(ConnectionBanner.MessageName).text, Is.EqualTo(ConnectionBannerModel.OfflineMessage));
            Assert.That(LabelOf(ConnectionBanner.SyncedName).text, Is.EqualTo("Last synced 3m ago"));
            Assert.That(ButtonOf(ConnectionBanner.ActionName).text, Is.EqualTo("Try again"));
            Assert.That(ButtonOf(ConnectionBanner.ActionName).enabledSelf, Is.True);
        }

        /// <summary>A signed-out model offers "Sign in again" on the banner.</summary>
        [UnityTest]
        public IEnumerator Unauthorized_OffersSignInAgain()
        {
            _banner.Show(ConnectionBannerModel.Create(ConnectionState.Unauthorized, 30, false));
            yield return null;

            Assert.That(BannerShown(), Is.True);
            Assert.That(ButtonOf(ConnectionBanner.ActionName).text, Is.EqualTo("Sign in again"));
        }

        /// <summary>An incompatible version shows the blocking screen with its title and message and hides the banner.</summary>
        [UnityTest]
        public IEnumerator IncompatibleVersion_ShowsTheBlockingScreen()
        {
            _banner.Show(ConnectionBannerModel.Create(ConnectionState.IncompatibleVersion, 30, false));
            yield return null;

            Assert.That(BlockerShown(), Is.True);
            Assert.That(BannerShown(), Is.False);
            Assert.That(LabelOf(ConnectionBanner.BlockerTitleName).text, Is.EqualTo("Update needed"));
            Assert.That(LabelOf(ConnectionBanner.BlockerMessageName).text, Is.EqualTo(ConnectionBannerModel.BlockingMessage));
            Assert.That(ButtonOf(ConnectionBanner.BlockerActionName).text, Is.EqualTo("Check again"));
        }

        /// <summary>While the blocking screen is up every position on the screen is the UI's, so the ground gets no press.</summary>
        [UnityTest]
        public IEnumerator TheBlockingScreen_TakesTheWholeScreen()
        {
            _banner.Show(ConnectionBannerModel.Create(ConnectionState.IncompatibleVersion, 30, false));
            yield return null;
            yield return null;

            Assert.That(_banner.IsOverUi(new Vector2(Screen.width / 2f, Screen.height / 2f)), Is.True);
            Assert.That(_banner.IsOverUi(new Vector2(2f, 2f)), Is.True);
        }

        /// <summary>A model that shows nothing (connected) hides both parts again after an outage, and the ground gets its presses back.</summary>
        [UnityTest]
        public IEnumerator Connected_HidesBothParts_AfterAnOutage()
        {
            _banner.Show(ConnectionBannerModel.Create(ConnectionState.IncompatibleVersion, 30, false));
            yield return null;

            _banner.Show(ConnectionBannerModel.Create(ConnectionState.Connected, 0, false));
            yield return null;
            yield return null;

            Assert.That(BannerShown(), Is.False);
            Assert.That(BlockerShown(), Is.False);
            Assert.That(_banner.IsOverUi(new Vector2(Screen.width / 2f, Screen.height / 2f)), Is.False);
        }

        /// <summary>A press on the action raises the event once; while a retry runs the button is off and a press raises nothing.</summary>
        [UnityTest]
        public IEnumerator PressAction_RaisesTheEvent_OnlyWhileTheActionWorks()
        {
            _banner.Show(ConnectionBannerModel.Create(ConnectionState.Offline, 30, false));
            yield return null;

            _banner.PressAction();
            Assert.That(_requested, Is.EqualTo(1));

            _banner.Show(ConnectionBannerModel.Create(ConnectionState.Offline, 30, true));
            yield return null;

            Assert.That(ButtonOf(ConnectionBanner.ActionName).text, Is.EqualTo("Trying…"));
            Assert.That(ButtonOf(ConnectionBanner.ActionName).enabledSelf, Is.False);
            _banner.PressAction();
            Assert.That(_requested, Is.EqualTo(1));
        }

        /// <summary>The same model again changes nothing on the screen (the banner is shown once a second and must not redraw for nothing).</summary>
        [UnityTest]
        public IEnumerator TheSameModelAgain_IsNotDrawnAgain()
        {
            ConnectionBannerModel first = ConnectionBannerModel.Create(ConnectionState.Offline, 61, false);
            _banner.Show(first);
            yield return null;

            _banner.Show(ConnectionBannerModel.Create(ConnectionState.Offline, 90, false));

            Assert.That(_banner.Model, Is.SameAs(first));
        }
    }
}
