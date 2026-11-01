#nullable enable

using System;
using CityBuilder.Client.Core;
using CityBuilder.Client.Presentation.Ui;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Ui
{
    /// <summary>The rule behind every command button (M0-55): only a connected client may send, every other state names its reason.</summary>
    public sealed class CommandAvailabilityTests
    {
        /// <summary>A connected client may send a command and has no reason to give.</summary>
        [Test]
        public void Connected_AllowsCommands_WithNoReason()
        {
            CommandAvailability availability = CommandAvailability.For(ConnectionState.Connected);

            Assert.That(availability.IsAllowed, Is.True);
            Assert.That(availability.Reason, Is.Empty);
        }

        /// <summary>Every state but Connected switches commands off, and says why in words that differ from the other states'.</summary>
        [TestCase(ConnectionState.Connecting, CommandAvailability.ConnectingReason)]
        [TestCase(ConnectionState.Offline, CommandAvailability.OfflineReason)]
        [TestCase(ConnectionState.ServerError, CommandAvailability.ServerErrorReason)]
        [TestCase(ConnectionState.Unauthorized, CommandAvailability.UnauthorizedReason)]
        [TestCase(ConnectionState.IncompatibleVersion, CommandAvailability.IncompatibleVersionReason)]
        public void EveryOtherState_ForbidsCommands_WithItsOwnReason(ConnectionState state, string reason)
        {
            CommandAvailability availability = CommandAvailability.For(state);

            Assert.That(availability.IsAllowed, Is.False);
            Assert.That(availability.Reason, Is.EqualTo(reason));
        }

        /// <summary>A new state added to the enum must be given a reason, not fall into "allowed": all the states are listed here.</summary>
        [Test]
        public void EveryState_IsCovered_AndOnlyConnectedIsAllowed()
        {
            foreach (ConnectionState state in Enum.GetValues(typeof(ConnectionState)))
            {
                Assert.That(CommandAvailability.For(state).IsAllowed, Is.EqualTo(state == ConnectionState.Connected), state.ToString());
            }
        }

        /// <summary>A screen that has not heard of the connection holds the default value, which must allow commands and not throw.</summary>
        [Test]
        public void TheDefaultValue_Allows()
        {
            CommandAvailability availability = default;

            Assert.That(availability.IsAllowed, Is.True);
            Assert.That(availability.Reason, Is.Empty);
            Assert.That(availability, Is.EqualTo(CommandAvailability.Allowed));
        }

        /// <summary>The states have different words, so a model that compares availabilities redraws when the state changes between two bad ones.</summary>
        [Test]
        public void TwoDifferentBadStates_AreNotEqual()
        {
            Assert.That(CommandAvailability.For(ConnectionState.Offline), Is.Not.EqualTo(CommandAvailability.For(ConnectionState.ServerError)));
            Assert.That(CommandAvailability.For(ConnectionState.Offline), Is.EqualTo(CommandAvailability.For(ConnectionState.Offline)));
        }
    }
}
