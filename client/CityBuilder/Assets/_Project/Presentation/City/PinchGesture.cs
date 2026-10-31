#nullable enable

using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// Turns the changing distance between two fingers into a camera zoom factor: fingers moving apart zoom in, together zoom out. Plain
    /// logic over screen positions in pixels.
    /// </summary>
    public sealed class PinchGesture
    {
        /// <summary>The fingers' distance at the last call, or 0 when no pinch is going on.</summary>
        private float _lastDistance;

        /// <summary>Whether a pinch is going on.</summary>
        public bool IsActive => _lastDistance > 0f;

        /// <summary>
        /// The two fingers are now at these positions. Returns the factor to give <see cref="CityCameraModel.Zoom"/>: the old distance
        /// divided by the new one (below 1 when the fingers spread). The first call of a pinch returns 1, as do fingers on the same spot.
        /// </summary>
        public float Update(Vector2 first, Vector2 second)
        {
            float distance = (first - second).magnitude;
            if (!(distance > 0f))
            {
                _lastDistance = 0f;
                return 1f;
            }

            float factor = _lastDistance > 0f ? _lastDistance / distance : 1f;
            _lastDistance = distance;
            return factor;
        }

        /// <summary>The pinch is over (a finger went up): the next <see cref="Update"/> starts a new one.</summary>
        public void End()
        {
            _lastDistance = 0f;
        }
    }
}
