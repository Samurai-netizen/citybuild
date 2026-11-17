#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;

namespace CityBuilder.Client.Presentation.Feedback
{
    /// <summary>
    /// The re-energize popup's wiring (M0-C-5): <see cref="Ask"/> opens the <see cref="ReenergizePrompt"/> (the delivery and route flows call
    /// it when their courier rests); its Re-energize button sends <c>reenergize-couriers</c>, and success closes the popup, while a refusal
    /// leaves it open (the toast says why). <c>GameRoot</c> creates it and calls <see cref="Dispose"/> when it is destroyed.
    /// </summary>
    public sealed class ReenergizePromptFlow : IDisposable
    {
        /// <summary>The popup.</summary>
        private readonly ReenergizePrompt _prompt;

        /// <summary>Sends the re-energize command.</summary>
        private readonly CommandSender _sender;

        /// <summary>Ends a command that is still open when the game object goes away.</summary>
        private readonly CancellationToken _cancellationToken;

        /// <summary>Whether <see cref="Dispose"/> has run.</summary>
        private bool _disposed;

        /// <summary>Starts listening to the popup's button.</summary>
        /// <exception cref="ArgumentNullException">The popup or the sender is null.</exception>
        public ReenergizePromptFlow(ReenergizePrompt prompt, CommandSender sender, CancellationToken cancellationToken)
        {
            _prompt = prompt != null ? prompt : throw new ArgumentNullException(nameof(prompt));
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _cancellationToken = cancellationToken;
            _prompt.ReenergizeRequested += OnReenergizeRequested;
        }

        /// <summary>Opens the popup: an action needs a resting courier.</summary>
        public void Ask()
        {
            if (!_disposed)
            {
                _prompt.Show();
            }
        }

        /// <summary>Stops listening. Safe to call twice.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _prompt.ReenergizeRequested -= OnReenergizeRequested;
        }

        /// <summary>The player pressed Re-energize couriers: send the command.</summary>
        private void OnReenergizeRequested()
        {
            if (!_disposed)
            {
                _ = ReenergizeAsync();
            }
        }

        /// <summary>Sends <c>reenergize-couriers</c>, closes the popup on success and turns the button on again after the answer.</summary>
        private async Task ReenergizeAsync()
        {
            _prompt.SetBusy(true);
            try
            {
                CommandOutcome outcome = await _sender.SendAsync(CommandIntent.ReenergizeCouriers(), _cancellationToken);
                if (!_disposed && outcome.IsSuccess)
                {
                    _prompt.Dismiss();
                }
            }
            catch (OperationCanceledException)
            {
                // The game object went away; the flow is disposed with it.
            }
            catch (Exception exception)
            {
                ClientLog.Exception(exception);
            }
            finally
            {
                if (!_disposed)
                {
                    _prompt.SetBusy(false);
                }
            }
        }
    }
}
