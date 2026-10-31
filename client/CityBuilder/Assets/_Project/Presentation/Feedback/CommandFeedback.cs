#nullable enable

using System;
using CityBuilder.Client.Net;
using UnityEngine;

namespace CityBuilder.Client.Presentation.Feedback
{
    /// <summary>
    /// Turns how a command ended into a toast (M0-47): the one place where a result becomes words for the player. It listens to
    /// <see cref="CommandSender.Completed"/>, so every command of every screen is covered without the screen asking: a success gets the
    /// sentence of its command kind, a failure gets the sentence of its error code (<see cref="FeedbackMessages"/>), and a failure's code, status
    /// and trace id go to the log, where a bug report finds them. A replaced worker target never reached the server and shows nothing. The class
    /// decides no rule and changes no state; <c>GameRoot</c> creates it once connected and calls <see cref="Dispose"/> when it is destroyed.
    /// </summary>
    public sealed class CommandFeedback : IDisposable
    {
        /// <summary>The sender whose results are shown.</summary>
        private readonly CommandSender _sender;

        /// <summary>Where the toasts go.</summary>
        private readonly ToastQueue _queue;

        /// <summary>The time the queue counts in, in milliseconds.</summary>
        private readonly Func<long> _nowMilliseconds;

        /// <summary>Whether <see cref="Dispose"/> has run.</summary>
        private bool _disposed;

        /// <summary>Starts listening to the sender.</summary>
        /// <param name="sender">The command sender.</param>
        /// <param name="queue">The toasts to add to.</param>
        /// <param name="nowMilliseconds">The time now in the queue's milliseconds (<see cref="ToastHost.NowMilliseconds"/> in the game).</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public CommandFeedback(CommandSender sender, ToastQueue queue, Func<long> nowMilliseconds)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _nowMilliseconds = nowMilliseconds ?? throw new ArgumentNullException(nameof(nowMilliseconds));
            _sender.Completed += OnCompleted;
        }

        /// <summary>Stops listening to the sender. Safe to call twice.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _sender.Completed -= OnCompleted;
        }

        /// <summary>An intent ended: a toast says how, unless it never reached the server.</summary>
        private void OnCompleted(CommandOutcome outcome)
        {
            switch (outcome.Kind)
            {
                case CommandOutcomeKind.Succeeded:
                    _queue.Show(ToastKind.Success, FeedbackMessages.ForSuccess(outcome.Intent.Kind), _nowMilliseconds());
                    break;
                case CommandOutcomeKind.Failed:
                    ApiError error = outcome.Error
                        ?? new ApiError(FeedbackMessages.CommandFailedCode, 0, "The command did not finish.", null, outcome.RequestId, null);
                    Debug.LogWarning("Command " + outcome.Intent.Kind + " failed after " + outcome.Attempts + " request(s): " + error);
                    _queue.Show(ToastKind.Error, FeedbackMessages.ForError(error), _nowMilliseconds());
                    break;
            }
        }
    }
}
