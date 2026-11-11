#nullable enable

using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// Moves a building while it works (visual v2, CR-0008-T2): its moving parts (<see cref="MotionRig"/>: the wheel, the axe, the saw, the
    /// flags) and its chimney smoke (<see cref="SmokePuffs"/>). <see cref="CityView"/> says once a second whether the building works; the
    /// motion eases in and out over <see cref="EaseSeconds"/>, so a building that stops settles at rest instead of freezing mid-swing. The
    /// store fill is separate (<see cref="FillView"/>) and never interrupts the motion. A thin component on the building's view object: it
    /// decides nothing, never calls the server, and its <c>Update</c> allocates nothing and does nothing for a building at rest.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingAnimator : MonoBehaviour
    {
        /// <summary>How long the motion takes to start fully or to settle, in seconds.</summary>
        public const float EaseSeconds = 0.6f;

        /// <summary>The moving parts of the current body, or null.</summary>
        private MotionRig? _rig;

        /// <summary>The chimney smoke of the current body, or null.</summary>
        private SmokePuffs? _smoke;

        /// <summary>How strongly the building moves now, 0 (at rest) to 1.</summary>
        private float _intensity;

        /// <summary>Whether the rig has been put back at rest since the intensity reached 0.</summary>
        private bool _rested = true;

        /// <summary>Whether the building works: set by <see cref="CityView"/> from the displayed city.</summary>
        public bool Working { get; set; }

        /// <summary>How strongly the building moves now, 0 (at rest) to 1.</summary>
        public float Intensity => _intensity;

        /// <summary>The moving parts of the current body, or null.</summary>
        public MotionRig? Rig => _rig;

        /// <summary>The chimney smoke of the current body, or null.</summary>
        public SmokePuffs? Smoke => _smoke;

        /// <summary>
        /// Takes the moving parts and the smoke of a new body (the old body went with its objects). The new parts start at rest; the
        /// intensity carries on, so a building that keeps working after an upgrade keeps moving.
        /// </summary>
        /// <param name="rig">The new body's moving parts, or null.</param>
        /// <param name="smoke">The new body's smoke, or null.</param>
        public void Bind(MotionRig? rig, SmokePuffs? smoke)
        {
            _rig = rig;
            _smoke = smoke;
            _rested = true;
        }

        /// <summary>Moves the motion on by one frame (Unity calls it).</summary>
        private void Update()
        {
            Step(Time.deltaTime);
        }

        /// <summary>
        /// Moves the intensity toward 1 or 0 and the parts and smoke with it, by <paramref name="deltaSeconds"/>. Public so a test can drive it
        /// without the player loop.
        /// </summary>
        /// <param name="deltaSeconds">The frame time, in seconds.</param>
        public void Step(float deltaSeconds)
        {
            float target = Working ? 1f : 0f;
            if (_intensity == target && target == 0f && _rested)
            {
                return;
            }

            _intensity = Mathf.MoveTowards(_intensity, target, deltaSeconds / EaseSeconds);
            if (_rig != null)
            {
                if (_intensity > 0f)
                {
                    _rig.Advance(deltaSeconds, _intensity);
                    _rested = false;
                }
                else if (!_rested)
                {
                    _rig.Rest();
                    _rested = true;
                }
            }
            else
            {
                _rested = _intensity == 0f;
            }

            _smoke?.Tick(deltaSeconds, _intensity);
        }
    }
}
