namespace CityBuilder.Client.Presentation.Feedback
{
    /// <summary>What a toast reports: it picks the colour and how long the toast stays (<see cref="ToastQueue"/>).</summary>
    public enum ToastKind
    {
        /// <summary>A command the server accepted.</summary>
        Success,

        /// <summary>A command that did not take effect; the sentence says why and, for a lost connection, to try again.</summary>
        Error,
    }
}
