#nullable enable

using System;
using CityBuilder.Client.Net;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary><see cref="CommandPolicy"/>: the numbers of <c>09</c>, which failures are worth a repeat, and the values it refuses.</summary>
    public sealed class CommandPolicyTests
    {
        /// <summary>The default is 3 attempts, 0.5 s then 1 s between them (a 2 s entry for a 4th), and a debounce of 400 ms.</summary>
        [Test]
        public void TheDefault_IsTheSpecifiedPolicy()
        {
            CommandPolicy policy = CommandPolicy.Default;

            Assert.That(policy.MaxAttempts, Is.EqualTo(3));
            Assert.That(policy.BackoffAfter(1), Is.EqualTo(TimeSpan.FromSeconds(0.5)));
            Assert.That(policy.BackoffAfter(2), Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(policy.StepperDebounce, Is.EqualTo(TimeSpan.FromMilliseconds(400)));
        }

        /// <summary>There is no wait after the last attempt or before the first.</summary>
        [TestCase(0)]
        [TestCase(3)]
        public void BackoffAfter_OutsideTheRepeats_Throws(int failedAttempt)
        {
            Assert.That(() => CommandPolicy.Default.BackoffAfter(failedAttempt), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        /// <summary>A lost connection, a timeout, any 5xx (a proxy's page too) and a concurrency conflict are repeated.</summary>
        [TestCase(ApiErrorCodes.ClientNetworkError, 0)]
        [TestCase(ApiErrorCodes.ClientTimeout, 0)]
        [TestCase(ApiErrorCodes.ConcurrencyConflict, 409)]
        [TestCase(ApiErrorCodes.InternalError, 500)]
        [TestCase(ApiErrorCodes.ClientInvalidResponse, 502)]
        [TestCase(ApiErrorCodes.ClientInvalidResponse, 503)]
        public void IsRetryable_ForTransientFailures(string code, int status)
        {
            Assert.That(CommandPolicy.IsRetryable(new ApiError(code, status, "x", null, null, null)), Is.True);
        }

        /// <summary>A rule's refusal, a rate limit, a login problem and an answer that is wrong at 2xx are final.</summary>
        [TestCase(ApiErrorCodes.WorkerLimit, 422)]
        [TestCase(ApiErrorCodes.InsufficientResource, 422)]
        [TestCase(ApiErrorCodes.TileOccupied, 409)]
        [TestCase(ApiErrorCodes.BuildingBusy, 409)]
        [TestCase(ApiErrorCodes.IdempotencyConflict, 409)]
        [TestCase(ApiErrorCodes.InvalidArgument, 400)]
        [TestCase(ApiErrorCodes.BuildingNotFound, 404)]
        [TestCase(ApiErrorCodes.RateLimited, 429)]
        [TestCase(ApiErrorCodes.AuthenticationRequired, 401)]
        [TestCase(ApiErrorCodes.ClientInvalidResponse, 200)]
        public void IsRetryable_IsFalseForTheRest(string code, int status)
        {
            Assert.That(CommandPolicy.IsRetryable(new ApiError(code, status, "x", null, null, null)), Is.False);
        }

        /// <summary>No attempts, too few waits, a negative wait or a negative debounce are refused.</summary>
        [Test]
        public void BadValues_AreRefused()
        {
            TimeSpan[] one = { TimeSpan.FromSeconds(1) };

            Assert.That(() => new CommandPolicy(0, one, TimeSpan.Zero), Throws.ArgumentException);
            Assert.That(() => new CommandPolicy(3, one, TimeSpan.Zero), Throws.ArgumentException);
            Assert.That(() => new CommandPolicy(2, new[] { TimeSpan.FromSeconds(-1) }, TimeSpan.Zero), Throws.ArgumentException);
            Assert.That(() => new CommandPolicy(2, one, TimeSpan.FromSeconds(-1)), Throws.ArgumentException);
            Assert.That(new CommandPolicy(1, Array.Empty<TimeSpan>(), TimeSpan.Zero).MaxAttempts, Is.EqualTo(1));
        }
    }
}
