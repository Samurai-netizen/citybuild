#nullable enable

using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// Tells a tap from a drag for one pointer (a finger or the mouse). A press that moves less than the threshold before it is released is
    /// a tap; once it moves farther it is a drag and stays one until released. Plain logic over screen positions in pixels.
    /// </summary>
    public sealed class PointerGesture
    {
        /// <summary>How far in pixels a press may move and still be a tap.</summary>
        private readonly float _dragThreshold;

        /// <summary>Where the press started.</summary>
        private Vector2 _start;

        /// <summary>Where the last <see cref="Move"/> left off, so each call reports only the new movement.</summary>
        private Vector2 _last;

        /// <summary>Creates a gesture reader.</summary>
        /// <param name="dragThresholdPixels">How far a press must move to become a drag.</param>
        public PointerGesture(float dragThresholdPixels)
        {
            _dragThreshold = dragThresholdPixels;
        }

        /// <summary>Whether the pointer is down.</summary>
        public bool IsDown { get; private set; }

        /// <summary>Whether the press has moved farther than the threshold, so releasing it is not a tap.</summary>
        public bool IsDragging { get; private set; }

        /// <summary>The pointer went down at <paramref name="position"/>.</summary>
        public void Press(Vector2 position)
        {
            IsDown = true;
            IsDragging = false;
            _start = position;
            _last = position;
        }

        /// <summary>
        /// The pointer moved to <paramref name="position"/>. Returns how far it moved since the last call, in pixels, or zero while the press is
        /// still within the threshold of where it began (or the pointer is up). The movement on the move that crosses the threshold counts
        /// from the press, so a drag loses none of it.
        /// </summary>
        public Vector2 Move(Vector2 position)
        {
            if (!IsDown)
            {
                return Vector2.zero;
            }

            if (!IsDragging)
            {
                if ((position - _start).magnitude <= _dragThreshold)
                {
                    return Vector2.zero;
                }

                IsDragging = true;
            }

            Vector2 delta = position - _last;
            _last = position;
            return delta;
        }

        /// <summary>The pointer went up. Returns true when the press was a tap, that is, it never became a drag.</summary>
        public bool Release()
        {
            bool tap = IsDown && !IsDragging;
            IsDown = false;
            IsDragging = false;
            return tap;
        }

        /// <summary>Forgets the press without it counting as a tap (a second finger came down, so it is a pinch now).</summary>
        public void Cancel()
        {
            IsDown = false;
            IsDragging = false;
        }
    }
}
