#nullable enable

using System;
using CityBuilder.Client.Core;
using CityBuilder.Client.Presentation.Connection;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Connection
{
    /// <summary>The words and flags of the connection banner and the blocking screen, for every connection state (M0-55).</summary>
    public sealed class ConnectionBannerModelTests
    {
        /// <summary>A connected client, and one that is still connecting, show nothing: the city is current or on its way.</summary>
        [TestCase(ConnectionState.Connected)]
        [TestCase(ConnectionState.Connecting)]
        public void Create_ConnectedOrConnecting_ShowsNothing(ConnectionState state)
        {
            ConnectionBannerModel model = ConnectionBannerModel.Create(state, 600, false);

            Assert.That(model.IsAnythingShown, Is.False);
            Assert.That(model.IsBannerShown, Is.False);
            Assert.That(model.IsBlocking, Is.False);
            Assert.That(model.Message, Is.Empty);
            Assert.That(model.SyncedText, Is.Empty);
            Assert.That(model.ActionText, Is.Empty);
        }

        /// <summary>Offline shows the banner with the age of the last sync and "Try again".</summary>
        [Test]
        public void Create_Offline_ShowsTheBannerWithTheSyncAgeAndTryAgain()
        {
            ConnectionBannerModel model = ConnectionBannerModel.Create(ConnectionState.Offline, 200, false);

            Assert.That(model.IsBannerShown, Is.True);
            Assert.That(model.IsBlocking, Is.False);
            Assert.That(model.Message, Is.EqualTo(ConnectionBannerModel.OfflineMessage));
            Assert.That(model.SyncedText, Is.EqualTo("Last synced 3m ago"));
            Assert.That(model.ActionText, Is.EqualTo("Try again"));
            Assert.That(model.ActionEnabled, Is.True);
        }

        /// <summary>A server fault shows the banner with its own line and the same action.</summary>
        [Test]
        public void Create_ServerError_ShowsTheBannerWithItsOwnLine()
        {
            ConnectionBannerModel model = ConnectionBannerModel.Create(ConnectionState.ServerError, 12, false);

            Assert.That(model.IsBannerShown, Is.True);
            Assert.That(model.Message, Is.EqualTo(ConnectionBannerModel.ServerErrorMessage));
            Assert.That(model.SyncedText, Is.EqualTo("Last synced 12s ago"));
            Assert.That(model.ActionText, Is.EqualTo("Try again"));
        }

        /// <summary>A refused login shows the banner with "Sign in again": the re-login flow.</summary>
        [Test]
        public void Create_Unauthorized_OffersSignInAgain()
        {
            ConnectionBannerModel model = ConnectionBannerModel.Create(ConnectionState.Unauthorized, 3600, false);

            Assert.That(model.IsBannerShown, Is.True);
            Assert.That(model.IsBlocking, Is.False);
            Assert.That(model.Message, Is.EqualTo(ConnectionBannerModel.UnauthorizedMessage));
            Assert.That(model.SyncedText, Is.EqualTo("Last synced 1h ago"));
            Assert.That(model.ActionText, Is.EqualTo("Sign in again"));
        }

        /// <summary>An incompatible version blocks: no banner, a title and a message that tell the player what to do, and "Check again".</summary>
        [Test]
        public void Create_IncompatibleVersion_BlocksWithAClearMessage()
        {
            ConnectionBannerModel model = ConnectionBannerModel.Create(ConnectionState.IncompatibleVersion, 30, false);

            Assert.That(model.IsBlocking, Is.True);
            Assert.That(model.IsBannerShown, Is.False);
            Assert.That(model.BlockingTitleText, Is.EqualTo("Update needed"));
            Assert.That(model.BlockingMessageText, Does.Contain("does not match the server"));
            Assert.That(model.BlockingMessageText, Does.Contain("Install the latest version"));
            Assert.That(model.ActionText, Is.EqualTo("Check again"));
            Assert.That(model.ActionEnabled, Is.True);
        }

        /// <summary>While a retry runs the button says so and is off, in the banner and on the blocking screen alike.</summary>
        [TestCase(ConnectionState.Offline)]
        [TestCase(ConnectionState.ServerError)]
        [TestCase(ConnectionState.Unauthorized)]
        [TestCase(ConnectionState.IncompatibleVersion)]
        public void Create_WhileRetrying_TheActionIsOff(ConnectionState state)
        {
            ConnectionBannerModel model = ConnectionBannerModel.Create(state, 30, true);

            Assert.That(model.ActionText, Is.EqualTo("Trying…"));
            Assert.That(model.ActionEnabled, Is.False);
        }

        /// <summary>Only the three states with a banner and the blocking state show anything; every state of the enum is accounted for.</summary>
        [Test]
        public void Create_EveryState_ShowsOneOfNothingBannerOrBlocking()
        {
            foreach (ConnectionState state in Enum.GetValues(typeof(ConnectionState)))
            {
                ConnectionBannerModel model = ConnectionBannerModel.Create(state, 100, false);

                Assert.That(model.IsBannerShown && model.IsBlocking, Is.False, state.ToString());
                bool shown = state != ConnectionState.Connected && state != ConnectionState.Connecting;
                Assert.That(model.IsAnythingShown, Is.EqualTo(shown), state.ToString());
            }
        }

        /// <summary>A negative age (a clock that went back) reads as just now.</summary>
        [Test]
        public void Create_NegativeAge_IsJustNow()
        {
            Assert.That(ConnectionBannerModel.Create(ConnectionState.Offline, -5, false).SyncedText, Is.EqualTo("Last synced just now"));
        }

        /// <summary>Two models of the same inputs are equal, and a change of the age's unit makes them differ, so the screen redraws only then.</summary>
        [Test]
        public void Equals_SameInputs_AreEqual_AndADifferentAgeIsNot()
        {
            ConnectionBannerModel a = ConnectionBannerModel.Create(ConnectionState.Offline, 61, false);
            ConnectionBannerModel same = ConnectionBannerModel.Create(ConnectionState.Offline, 90, false);
            ConnectionBannerModel later = ConnectionBannerModel.Create(ConnectionState.Offline, 120, false);

            Assert.That(a, Is.EqualTo(same));
            Assert.That(a, Is.Not.EqualTo(later));
            Assert.That(a, Is.Not.EqualTo(ConnectionBannerModel.Create(ConnectionState.ServerError, 61, false)));
        }
    }
}
