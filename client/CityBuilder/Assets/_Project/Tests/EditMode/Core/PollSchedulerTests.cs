#nullable enable

using System;
using CityBuilder.Client.Core;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Core
{
    /// <summary>
    /// <see cref="PollScheduler"/> (M0-41) on a clock the test moves by hand: the 120 second foreground rhythm, nothing while paused, a poll
    /// at once on resume, and never two polls at a time.
    /// </summary>
    public sealed class PollSchedulerTests
    {
        /// <summary>The fake real-time clock, in seconds.</summary>
        private double _now;

        /// <summary>The scheduler under test, on the game's interval.</summary>
        private PollScheduler _scheduler = null!;

        /// <summary>Before each test: a scheduler created at second 1000 with the 120 second interval.</summary>
        [SetUp]
        public void CreateScheduler()
        {
            _now = 1000;
            _scheduler = new PollScheduler(() => _now, PollScheduler.DefaultIntervalSeconds);
        }

        /// <summary>The interval of the game is 120 seconds (<c>09</c>).</summary>
        [Test]
        public void DefaultInterval_Is120Seconds()
        {
            Assert.That(PollScheduler.DefaultIntervalSeconds, Is.EqualTo(120d));
        }

        /// <summary>The city was fetched by the start flow, so a new scheduler is not due.</summary>
        [Test]
        public void New_IsNotDueYet()
        {
            Assert.That(_scheduler.IsDue, Is.False);
            Assert.That(_scheduler.TryBegin(), Is.False);
        }

        /// <summary>A poll is due when a whole interval has passed, and not a second before.</summary>
        [Test]
        public void IsDue_AfterTheInterval_NotBefore()
        {
            _now += 119.9;
            Assert.That(_scheduler.IsDue, Is.False);

            _now += 0.1;
            Assert.That(_scheduler.IsDue, Is.True);
        }

        /// <summary>The interval counts from the end of the last poll, not from its start.</summary>
        [Test]
        public void End_RestartsTheIntervalFromNow()
        {
            _now += 120;
            Assert.That(_scheduler.TryBegin(), Is.True);
            _now += 10;
            _scheduler.End();

            _now += 119;
            Assert.That(_scheduler.IsDue, Is.False);
            _now += 1;
            Assert.That(_scheduler.IsDue, Is.True);
        }

        /// <summary>While a poll runs nothing else is due, however late.</summary>
        [Test]
        public void Polling_NeverStartsASecondPoll()
        {
            _now += 500;
            Assert.That(_scheduler.TryBegin(), Is.True);

            Assert.That(_scheduler.IsPolling, Is.True);
            Assert.That(_scheduler.IsDue, Is.False);
            Assert.That(_scheduler.TryBegin(), Is.False);
        }

        /// <summary>A request while a poll runs is kept: one more poll follows it, at once.</summary>
        [Test]
        public void RequestPoll_WhilePolling_IsServedAfterIt()
        {
            _now += 120;
            _scheduler.TryBegin();
            _scheduler.RequestPoll();
            _scheduler.End();

            Assert.That(_scheduler.IsDue, Is.True);
            Assert.That(_scheduler.TryBegin(), Is.True);
            _scheduler.End();
            Assert.That(_scheduler.IsDue, Is.False);
        }

        /// <summary>A request makes a poll due at once and is used up by it.</summary>
        [Test]
        public void RequestPoll_IsDueAtOnce_AndServedOnce()
        {
            _scheduler.RequestPoll();

            Assert.That(_scheduler.IsDue, Is.True);
            Assert.That(_scheduler.TryBegin(), Is.True);
            _scheduler.End();
            Assert.That(_scheduler.IsDue, Is.False);
        }

        /// <summary>No poll in the background, however long it lasts and whatever was asked.</summary>
        [Test]
        public void Pause_NothingIsDue_EvenAfterManyIntervalsOrARequest()
        {
            _scheduler.Pause();
            _now += 10 * 120;
            _scheduler.RequestPoll();

            Assert.That(_scheduler.IsPaused, Is.True);
            Assert.That(_scheduler.IsDue, Is.False);
            Assert.That(_scheduler.TryBegin(), Is.False);
        }

        /// <summary>Coming back to the foreground polls at once, even a second after the last poll.</summary>
        [Test]
        public void Resume_MakesAPollDueAtOnce()
        {
            _scheduler.Pause();
            _now += 1;
            _scheduler.Resume();

            Assert.That(_scheduler.IsPaused, Is.False);
            Assert.That(_scheduler.IsDue, Is.True);
            Assert.That(_scheduler.TryBegin(), Is.True);
            _scheduler.End();
            Assert.That(_scheduler.IsDue, Is.False);
        }

        /// <summary>A resume without a pause (Unity can report one) does not cause a poll.</summary>
        [Test]
        public void Resume_WithoutAPause_ChangesNothing()
        {
            _scheduler.Resume();

            Assert.That(_scheduler.IsDue, Is.False);
        }

        /// <summary>Pausing twice and resuming once is one resume: the app is in the foreground.</summary>
        [Test]
        public void Pause_Twice_ThenResume_IsInTheForeground()
        {
            _scheduler.Pause();
            _scheduler.Pause();
            _scheduler.Resume();

            Assert.That(_scheduler.IsPaused, Is.False);
            Assert.That(_scheduler.IsDue, Is.True);
        }

        /// <summary>A poll that was running when the app went to the background may still end; nothing starts afterwards until the resume.</summary>
        [Test]
        public void Pause_DuringAPoll_LetsItEnd_ButStartsNoneUntilResume()
        {
            _now += 120;
            _scheduler.TryBegin();
            _scheduler.Pause();
            _scheduler.End();
            _now += 500;

            Assert.That(_scheduler.IsDue, Is.False);
            _scheduler.Resume();
            Assert.That(_scheduler.IsDue, Is.True);
        }

        /// <summary>A city fetched by someone else restarts the interval.</summary>
        [Test]
        public void MarkFetched_RestartsTheInterval()
        {
            _now += 100;
            _scheduler.MarkFetched();
            _now += 100;

            Assert.That(_scheduler.IsDue, Is.False);
            _now += 20;
            Assert.That(_scheduler.IsDue, Is.True);
        }

        /// <summary>The constructor refuses a missing clock and an interval that is not above 0 (also NaN).</summary>
        [Test]
        public void Constructor_RefusesBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new PollScheduler(null!, 120));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PollScheduler(() => 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PollScheduler(() => 0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PollScheduler(() => 0, double.NaN));
        }
    }
}
