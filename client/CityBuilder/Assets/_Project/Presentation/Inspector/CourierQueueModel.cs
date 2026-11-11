#nullable enable

using System;
using System.Collections.Generic;

namespace CityBuilder.Client.Presentation.Inspector
{
    /// <summary>
    /// What the courier station's inspector shows of one courier's work (CR-0008 amendment 1, D18): its delivery lines, each with a Cancel
    /// button, or its repeating route, and a Cancel all button for both. Immutable; two queues that say the same compare equal.
    /// </summary>
    public sealed class CourierQueueModel : IEquatable<CourierQueueModel>
    {
        /// <summary>Makes a queue.</summary>
        /// <param name="slot">The courier's slot.</param>
        /// <param name="title">The heading, such as <c>Courier 1 · 3 deliveries</c>.</param>
        /// <param name="lines">The delivery lines in the order the courier runs them; empty for a courier on a route.</param>
        public CourierQueueModel(int slot, string title, IReadOnlyList<CourierQueueLine> lines)
        {
            Slot = slot;
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Lines = lines ?? throw new ArgumentNullException(nameof(lines));
        }

        /// <summary>The courier's slot at its station: what a cancel names.</summary>
        public int Slot { get; }

        /// <summary>The heading, such as <c>Courier 1 · 3 deliveries</c> or <c>Courier 2 · Route: Woodcutter → Sawmill</c>.</summary>
        public string Title { get; }

        /// <summary>The delivery lines in the order the courier runs them; the first is the one it runs now.</summary>
        public IReadOnlyList<CourierQueueLine> Lines { get; }

        /// <inheritdoc/>
        public bool Equals(CourierQueueModel? other)
        {
            if (other == null || Slot != other.Slot || Title != other.Title || Lines.Count != other.Lines.Count)
            {
                return false;
            }

            for (int i = 0; i < Lines.Count; i++)
            {
                if (!Lines[i].Equals(other.Lines[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as CourierQueueModel);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Slot, Title, Lines.Count);
    }

    /// <summary>One delivery line as the inspector shows it: its id (what Cancel names) and its words.</summary>
    public sealed class CourierQueueLine : IEquatable<CourierQueueLine>
    {
        /// <summary>Makes a line.</summary>
        /// <param name="lineId">The line's id in the city.</param>
        /// <param name="text">The words, such as <c>10 Logs · Woodcutter → Warehouse</c>.</param>
        public CourierQueueLine(int lineId, string text)
        {
            LineId = lineId;
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        /// <summary>The line's id in the city.</summary>
        public int LineId { get; }

        /// <summary>The words, such as <c>10 Logs · Woodcutter → Warehouse · now</c>.</summary>
        public string Text { get; }

        /// <inheritdoc/>
        public bool Equals(CourierQueueLine? other) => other != null && LineId == other.LineId && Text == other.Text;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as CourierQueueLine);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(LineId, Text);
    }
}
