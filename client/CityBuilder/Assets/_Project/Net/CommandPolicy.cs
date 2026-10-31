#nullable enable

using System;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// How <see cref="CommandSender"/> repeats and delays commands: the most requests one intent may send, the waits between them and the
    /// worker stepper's debounce (<c>09</c> Runtime structure and UX rules). The numbers are the client's own behaviour, not game
    /// numbers (<c>02</c> rule 8 is about the game's economy), so they live here and a test can pass others.
    /// </summary>
    public sealed class CommandPolicy
    {
        /// <summary>The waits before each repeat; entry 0 is before the 2nd send.</summary>
        private readonly TimeSpan[] _backoff;

        /// <summary>Creates a policy.</summary>
        /// <param name="maxAttempts">The most requests one intent may send, including the first; at least 1.</param>
        /// <param name="backoff">The wait before each repeat, in order: entry 0 before the 2nd send. It needs one entry for every repeat the maximum allows (<paramref name="maxAttempts"/> - 1); more are kept unused.</param>
        /// <param name="stepperDebounce">How long a worker target waits for a newer one before it is sent; not negative.</param>
        /// <exception cref="ArgumentException">A value is out of range.</exception>
        public CommandPolicy(int maxAttempts, TimeSpan[] backoff, TimeSpan stepperDebounce)
        {
            if (maxAttempts < 1)
            {
                throw new ArgumentException("At least one attempt is needed.", nameof(maxAttempts));
            }

            if (backoff == null || backoff.Length < maxAttempts - 1)
            {
                throw new ArgumentException("A wait is needed for every repeat (" + (maxAttempts - 1) + ").", nameof(backoff));
            }

            foreach (TimeSpan wait in backoff)
            {
                if (wait < TimeSpan.Zero)
                {
                    throw new ArgumentException("A wait must not be negative.", nameof(backoff));
                }
            }

            if (stepperDebounce < TimeSpan.Zero)
            {
                throw new ArgumentException("The debounce must not be negative.", nameof(stepperDebounce));
            }

            MaxAttempts = maxAttempts;
            _backoff = (TimeSpan[])backoff.Clone();
            StepperDebounce = stepperDebounce;
        }

        /// <summary>
        /// The policy the game uses (<c>09</c>): up to 3 requests per intent, waiting 0.5 s and then 1 s between them, and a stepper debounce
        /// of 400 ms. The 2 s of the specified 0.5 / 1 / 2 would come before a 4th request, which 3 attempts never make.
        /// </summary>
        public static CommandPolicy Default { get; } = new CommandPolicy(
            3,
            new[] { TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) },
            TimeSpan.FromMilliseconds(400));

        /// <summary>The most requests one intent may send, the first included.</summary>
        public int MaxAttempts { get; }

        /// <summary>How long a worker target waits for a newer one before it is sent.</summary>
        public TimeSpan StepperDebounce { get; }

        /// <summary>The wait after the failed attempt number <paramref name="failedAttempt"/> (1 for the first) and before the next send.</summary>
        /// <exception cref="ArgumentOutOfRangeException">There is no next send: the attempt number is not from 1 to <see cref="MaxAttempts"/> - 1.</exception>
        public TimeSpan BackoffAfter(int failedAttempt)
        {
            if (failedAttempt < 1 || failedAttempt >= MaxAttempts)
            {
                throw new ArgumentOutOfRangeException(nameof(failedAttempt));
            }

            return _backoff[failedAttempt - 1];
        }

        /// <summary>
        /// Whether an error is worth sending the same command again: a request that did not reach the server or got no answer in time, a
        /// server fault (5xx, also a proxy's page in place of the answer) and <c>CONCURRENCY_CONFLICT</c>. Every other refusal is final: a
        /// business rule's 4xx would answer the same again, <c>RATE_LIMITED</c> asks for a wait the player decides on, and
        /// <c>AUTHENTICATION_REQUIRED</c> needs a new login (M0-41).
        /// </summary>
        public static bool IsRetryable(ApiError error)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            return error.Code == ApiErrorCodes.ClientNetworkError
                || error.Code == ApiErrorCodes.ClientTimeout
                || error.Code == ApiErrorCodes.ConcurrencyConflict
                || error.Status >= 500;
        }
    }
}
