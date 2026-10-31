namespace CityBuilder.Client.Net
{
    /// <summary>How one player intent ended (<see cref="CommandOutcome"/>).</summary>
    public enum CommandOutcomeKind
    {
        /// <summary>The server accepted the command and its snapshot went to the city store.</summary>
        Succeeded,

        /// <summary>The command did not take effect: the server refused it, the retries ran out, or the answer was unusable. <see cref="CommandOutcome.Error"/> says why.</summary>
        Failed,

        /// <summary>A newer worker target for the same building replaced this one before it was sent; nothing reached the server.</summary>
        Superseded,
    }
}
