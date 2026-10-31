namespace CityBuilder.Client.Net
{
    /// <summary>Where a <see cref="PendingCommand"/> is on its way to the server; what the UI shows next to a pending action.</summary>
    public enum CommandPhase
    {
        /// <summary>Not sent yet: the worker stepper's debounce is running, or an earlier worker command of the same building is still on its way.</summary>
        Waiting,

        /// <summary>A request is open.</summary>
        Sending,

        /// <summary>The last attempt failed in a way worth repeating and the sender is waiting to send the same command again.</summary>
        Retrying,
    }
}
