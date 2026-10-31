#nullable enable

namespace CityBuilder.Client.Presentation.Feedback
{
    /// <summary>One message on the screen (M0-47): its words, its kind, and when it goes. Made by <see cref="ToastQueue"/>; never changed except its time.</summary>
    public sealed class Toast
    {
        /// <summary>Creates a toast; <see cref="ToastQueue"/> is the only maker.</summary>
        internal Toast(int id, ToastKind kind, string text, long expiresAtMilliseconds)
        {
            Id = id;
            Kind = kind;
            Text = text;
            ExpiresAtMilliseconds = expiresAtMilliseconds;
        }

        /// <summary>A number unique within the queue, so a screen can tell its element of this toast from the others.</summary>
        public int Id { get; }

        /// <summary>What the toast reports.</summary>
        public ToastKind Kind { get; }

        /// <summary>The sentence the player reads.</summary>
        public string Text { get; }

        /// <summary>The time, in the milliseconds the caller counts, from which the toast is gone.</summary>
        public long ExpiresAtMilliseconds { get; internal set; }
    }
}
