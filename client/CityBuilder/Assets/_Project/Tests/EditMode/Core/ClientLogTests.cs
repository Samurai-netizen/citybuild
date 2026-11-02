#nullable enable

using System;
using CityBuilder.Client.Core;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Core
{
    /// <summary>
    /// <see cref="ClientLogger"/> and <see cref="ClientLog"/> (M0-57) with a sink that keeps the lines: the level filter, the default
    /// level per build type, the sink it is given and the exception line.
    /// </summary>
    public sealed class ClientLogTests
    {
        /// <summary>The sink the logger under test writes to.</summary>
        private RecordingLogSink _sink = null!;

        /// <summary>A logger over the recording sink at <paramref name="level"/>.</summary>
        private ClientLogger NewLogger(LogLevel level)
        {
            _sink = new RecordingLogSink();
            return new ClientLogger(level, _sink);
        }

        /// <summary>At the Debug level every method writes, each with its own level.</summary>
        [Test]
        public void AtDebug_EveryMethodWrites_WithItsLevel()
        {
            ClientLogger log = NewLogger(LogLevel.Debug);

            log.Debug("d");
            log.Info("i");
            log.Warning("w");
            log.Error("e");

            Assert.That(_sink.Lines.ConvertAll(line => line.Level), Is.EqualTo(new[] { LogLevel.Debug, LogLevel.Info, LogLevel.Warning, LogLevel.Error }));
            Assert.That(_sink.Lines.ConvertAll(line => line.Message), Is.EqualTo(new[] { "d", "i", "w", "e" }));
        }

        /// <summary>A line below the level is dropped; one at or above it is written.</summary>
        [Test]
        public void BelowTheLevel_IsDropped_AtOrAboveIsWritten()
        {
            ClientLogger log = NewLogger(LogLevel.Warning);

            log.Debug("d");
            log.Info("i");
            log.Warning("w");
            log.Error("e");

            Assert.That(_sink.Lines.ConvertAll(line => line.Message), Is.EqualTo(new[] { "w", "e" }));
        }

        /// <summary>The level Off writes nothing, not even an error.</summary>
        [Test]
        public void LevelOff_WritesNothing()
        {
            ClientLogger log = NewLogger(LogLevel.Off);

            log.Error("e");
            log.Exception(new InvalidOperationException("boom"));

            Assert.That(_sink.Lines, Is.Empty);
            Assert.That(log.IsEnabled(LogLevel.Error), Is.False);
        }

        /// <summary><see cref="ClientLogger.IsEnabled"/> agrees with the filter, so a caller can skip building a message.</summary>
        [Test]
        public void IsEnabled_FollowsTheLevel()
        {
            ClientLogger log = NewLogger(LogLevel.Info);

            Assert.That(log.IsEnabled(LogLevel.Debug), Is.False);
            Assert.That(log.IsEnabled(LogLevel.Info), Is.True);
            Assert.That(log.IsEnabled(LogLevel.Error), Is.True);
        }

        /// <summary>A development build (and the Editor) shows Debug; a release build shows Warning and above only.</summary>
        [Test]
        public void DefaultLevel_IsDebugInDevelopment_WarningInRelease()
        {
            Assert.That(ClientLog.DefaultLevelFor(true), Is.EqualTo(LogLevel.Debug));
            Assert.That(ClientLog.DefaultLevelFor(false), Is.EqualTo(LogLevel.Warning));
        }

        /// <summary>An exception is one Error line that names its type and carries the exception for the stack trace.</summary>
        [Test]
        public void Exception_IsAnErrorLine_WithTheException()
        {
            ClientLogger log = NewLogger(LogLevel.Debug);
            InvalidOperationException exception = new InvalidOperationException("boom");

            log.Exception(exception);

            Assert.That(_sink.Lines, Has.Count.EqualTo(1));
            Assert.That(_sink.Lines[0].Level, Is.EqualTo(LogLevel.Error));
            Assert.That(_sink.Lines[0].Message, Is.EqualTo("InvalidOperationException: boom"));
            Assert.That(_sink.Lines[0].Exception, Is.SameAs(exception));
        }

        /// <summary>A logger needs a sink: a missing one is a wiring mistake found at construction.</summary>
        [Test]
        public void ALoggerWithoutASink_IsRefused()
        {
            Assert.That(() => new ClientLogger(LogLevel.Debug, null!), Throws.ArgumentNullException);
        }

        /// <summary>The game's logger follows the build type and writes to the Unity console.</summary>
        [Test]
        public void Default_FollowsTheBuildType()
        {
            Assert.That(ClientLog.Default.Level, Is.EqualTo(ClientLog.DefaultLevelFor(UnityEngine.Debug.isDebugBuild)));
        }
    }
}
