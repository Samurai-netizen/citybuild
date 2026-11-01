#nullable enable

using System;
using CityBuilder.Simulation;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// What one building's status shows on the screen (M0-50): the progress ring (which job and how far, in permille), the status bubble (the
    /// reason it makes no progress and the item it names), whether the building looks stalled, and how many worker pips it carries. A plain value made by
    /// <see cref="BuildingStatusModel.Describe"/> from the displayed city; two equal values draw the same picture, so a view redraws only when
    /// the value differs. Display only.
    /// </summary>
    public readonly struct BuildingStatus : IEquatable<BuildingStatus>
    {
        /// <summary>The most worker pips a building shows, however many workers it has: a layout limit, not a game number.</summary>
        public const int MaxPips = 6;

        /// <summary>Makes a status; <see cref="Pips"/> is capped at <see cref="MaxPips"/>.</summary>
        /// <param name="ring">The job the ring counts.</param>
        /// <param name="progressPermille">How far the job is, 0 to 1000; 0 without a ring.</param>
        /// <param name="bubble">The bubble to show.</param>
        /// <param name="bubbleItem">The item the bubble names, or the empty id.</param>
        /// <param name="pips">The workers assigned; at most <see cref="MaxPips"/> are kept.</param>
        public BuildingStatus(StatusRingKind ring, int progressPermille, StatusBubbleKind bubble, ItemId bubbleItem, int pips)
        {
            Ring = ring;
            ProgressPermille = ring == StatusRingKind.None ? 0 : Math.Max(0, Math.Min(1000, progressPermille));
            Bubble = bubble;
            BubbleItem = bubble == StatusBubbleKind.MissingInput || bubble == StatusBubbleKind.StorageFull ? bubbleItem : default;
            Pips = Math.Max(0, Math.Min(MaxPips, pips));
        }

        /// <summary>The job the progress ring counts.</summary>
        public StatusRingKind Ring { get; }

        /// <summary>How far the job is in thousandths (<c>ProgressDisplay.ProgressPermille</c>), 0 without a ring.</summary>
        public int ProgressPermille { get; }

        /// <summary>The status bubble.</summary>
        public StatusBubbleKind Bubble { get; }

        /// <summary>The item a <see cref="StatusBubbleKind.MissingInput"/> or <see cref="StatusBubbleKind.StorageFull"/> bubble names; the empty id for the others.</summary>
        public ItemId BubbleItem { get; }

        /// <summary>Whether the building makes no progress for a reason the player can act on, so it is drawn slightly desaturated. True exactly when there is a bubble.</summary>
        public bool Stalled => Bubble != StatusBubbleKind.None;

        /// <summary>How many worker pips to show: one per assigned worker, at most <see cref="MaxPips"/>.</summary>
        public int Pips { get; }

        /// <inheritdoc />
        public bool Equals(BuildingStatus other)
        {
            return Ring == other.Ring && ProgressPermille == other.ProgressPermille && Bubble == other.Bubble
                && BubbleItem == other.BubbleItem && Pips == other.Pips;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is BuildingStatus other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine((int)Ring, ProgressPermille, (int)Bubble, BubbleItem, Pips);

        /// <summary>Whether two statuses draw the same picture.</summary>
        public static bool operator ==(BuildingStatus left, BuildingStatus right) => left.Equals(right);

        /// <summary>Whether two statuses draw different pictures.</summary>
        public static bool operator !=(BuildingStatus left, BuildingStatus right) => !left.Equals(right);

        /// <summary>The status as words for a test failure: <c>Producing 500, MissingInput logs, 2 pips</c>.</summary>
        public override string ToString()
        {
            return Ring + " " + ProgressPermille + ", " + Bubble + (BubbleItem == default ? string.Empty : " " + BubbleItem.Value) + ", " + Pips + " pips";
        }
    }
}
