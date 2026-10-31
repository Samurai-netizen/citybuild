#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>
    /// A stand-in for waiting, for <c>CommandSender</c>: it records every wait it was asked for and finishes it when the test says so (or at
    /// once), so a test of a 0.5 second backoff or a 400 ms debounce takes no time and has no race. A wait whose token is cancelled ends
    /// cancelled, as <c>Task.Delay</c> does.
    /// </summary>
    public sealed class FakeDelay
    {
        /// <summary>The waits that have not finished or been cancelled, oldest first.</summary>
        private readonly List<TaskCompletionSource<bool>> _open = new List<TaskCompletionSource<bool>>();

        /// <summary>Every wait asked for, oldest first.</summary>
        public List<TimeSpan> Requested { get; } = new List<TimeSpan>();

        /// <summary>Whether a wait finishes as soon as it is asked for (the backoff tests); otherwise it waits for <see cref="CompleteAt"/>.</summary>
        public bool FinishAtOnce { get; set; }

        /// <summary>How many waits are open.</summary>
        public int OpenCount => _open.Count;

        /// <summary>The wait itself, in the shape <c>CommandSender</c> takes.</summary>
        public Task Delay(TimeSpan duration, CancellationToken cancellationToken)
        {
            Requested.Add(duration);
            cancellationToken.ThrowIfCancellationRequested();
            if (FinishAtOnce)
            {
                return Task.CompletedTask;
            }

            var wait = new TaskCompletionSource<bool>();
            _open.Add(wait);
            cancellationToken.Register(() =>
            {
                _open.Remove(wait);
                wait.TrySetCanceled();
            });
            return wait.Task;
        }

        /// <summary>Finishes the open wait number <paramref name="index"/> (0 is the oldest).</summary>
        public void CompleteAt(int index)
        {
            TaskCompletionSource<bool> wait = _open[index];
            _open.RemoveAt(index);
            wait.SetResult(true);
        }
    }
}
