#nullable enable

using System;
using CityBuilder.Client.Core;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Core
{
    /// <summary>
    /// <see cref="ClientLog"/> (M0-57) with a sink that keeps the lines: the level filter, the default level per build type, the
    /// replaceable sink and the exception line.
    /// </summary>
    public sealed class ClientLogTests
    {
        /// <summary>The sink the log writes to in a test.</summary>
        private RecordingLogSink _sink = null!;

        /// <summary>Before each test: the log writes to a recording sink at the Debug level.</summary>
        [SetUp]
        public void UseARecordingSink()
        {
            _sink = new RecordingLogSink();
            ClientLog.Sink = _sink;
            ClientLog.Level = LogLevel.Debug;
        }

        /// <summary>After each test: the build's default level and the Unity sink, so other tests are not changed.</summary>
        [TearDown]
        public void RestoreTheDefaults()
        {
            ClientLog.Reset();
        }

        /// <summary>At the Debug level every method writes, each with its own level.</summary>
        [Test]
        public void AtDebug_EveryMethodWrites_WithItsLevel()
        {
            ClientLog.Debug("d");
            ClientLog.Info("i");
            ClientLog.Warning("w");
            ClientLog.Error("e");

            Assert.That(_sink.Lines.ConvertAll(line => line.Level), Is.EqualTo(new[] { LogLevel.Debug, LogLevel.Info, LogLevel.Warning, LogLevel.Error }));
            Assert.That(_sink.Lines.ConvertAll(line => line.Message), Is.EqualTo(new[] { "d", "i", "w", "e" }));
        }

        /// <summary>A line below the level is dropped; one at or above it is written.</summary>
        [Test]
        public void BelowTheLevel_IsDropped_AtOrAboveIsWritten()
        {
            ClientLog.Level = LogLevel.Warning;

            ClientLog.Debug("d");
            ClientLog.Info("i");
            ClientLog.Warning("w");
            ClientLog.Error("e");

            Assert.That(_sink.Lines.ConvertAll(line => line.Message), Is.EqualTo(new[] { "w", "e" }));
        }

        /// <summary>The level Off writes nothing, not even an error.</summary>
        [Test]
        public void LevelOff_WritesNothing()
        {
            ClientLog.Level = LogLevel.Off;

            ClientLog.Error("e");
            ClientLog.Exception(new InvalidOperationException("boom"));

            Assert.That(_sink.Lines, Is.Empty);
            Assert.That(ClientLog.IsEnabled(LogLevel.Error), Is.False);
        }

        /// <summary><see cref="ClientLog.IsEnabled"/> agrees with the filter, so a caller can skip building a message.</summary>
        [Test]
        public void IsEnabled_FollowsTheLevel()
        {
            ClientLog.Level = LogLevel.Info;

            Assert.That(ClientLog.IsEnabled(LogLevel.Debug), Is.False);
            Assert.That(ClientLog.IsEnabled(LogLevel.Info), Is.True);
            Assert.That(ClientLog.IsEnabled(LogLevel.Error), Is.True);
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
            InvalidOperationException exception = new InvalidOperationException("boom");

            ClientLog.Exception(exception);

            Assert.That(_sink.Lines, Has.Count.EqualTo(1));
            Assert.That(_sink.Lines[0].Level, Is.EqualTo(LogLevel.Error));
            Assert.That(_sink.Lines[0].Message, Is.EqualTo("InvalidOperationException: boom"));
            Assert.That(_sink.Lines[0].Exception, Is.SameAs(exception));
        }

        /// <summary>Reset gives the build's default level back and drops the replaced sink.</summary>
        [Test]
        public void Reset_RestoresTheDefaultSinkAndLevel()
        {
            ClientLog.Level = LogLevel.Off;

            ClientLog.Reset();

            Assert.That(ClientLog.Sink, Is.InstanceOf<UnityLogSink>());
            Assert.That(ClientLog.Level, Is.EqualTo(ClientLog.DefaultLevelFor(UnityEngine.Debug.isDebugBuild)));
        }
    }
}
