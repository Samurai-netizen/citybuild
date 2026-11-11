#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The moving parts of one model instance (visual v2, CR-0008-T2): every object at or below the body whose name has a
    /// <see cref="PartMotions"/> row and that has a sibling called its name and <see cref="PartMotions.PoseSuffix"/>. The part's own
    /// transform is its rest and the sibling's is the far end of its motion; both came through the same model import, so the turn between
    /// them is the part's axis and no axis is written in code. <see cref="Find"/> runs once when a body is built; <see cref="Advance"/> then
    /// poses the parts every frame without allocating. Display only.
    /// </summary>
    public sealed class MotionRig
    {
        /// <summary>The moving parts.</summary>
        private readonly Transform[] _parts;

        /// <summary>Each part's position at rest, in its parent's space.</summary>
        private readonly Vector3[] _restPositions;

        /// <summary>Each part's rotation at rest, in its parent's space.</summary>
        private readonly Quaternion[] _restRotations;

        /// <summary>Each part's turn from rest to its pose: the axis, in the part's own rest frame.</summary>
        private readonly Vector3[] _axes;

        /// <summary>Each part's turn from rest to its pose: the angle in degrees (0 when the part only slides).</summary>
        private readonly float[] _angles;

        /// <summary>Each part's move from rest to its pose, in its parent's space.</summary>
        private readonly Vector3[] _offsets;

        /// <summary>How each part moves.</summary>
        private readonly PartMotion[] _motions;

        /// <summary>The motion time: seconds of full motion so far (time runs slower while the motion eases in or out).</summary>
        private float _seconds;

        /// <summary>Creates a rig of the given parts and poses; <see cref="Find"/> is how a view makes one.</summary>
        /// <param name="parts">The moving parts.</param>
        /// <param name="poses">The pose of each part, in the same order.</param>
        /// <param name="motions">How each part moves, in the same order.</param>
        private MotionRig(IReadOnlyList<Transform> parts, IReadOnlyList<Transform> poses, IReadOnlyList<PartMotion> motions)
        {
            int count = parts.Count;
            _parts = new Transform[count];
            _restPositions = new Vector3[count];
            _restRotations = new Quaternion[count];
            _axes = new Vector3[count];
            _angles = new float[count];
            _offsets = new Vector3[count];
            _motions = new PartMotion[count];
            for (int i = 0; i < count; i++)
            {
                Transform part = parts[i];
                Transform pose = poses[i];
                _parts[i] = part;
                _restPositions[i] = part.localPosition;
                _restRotations[i] = part.localRotation;
                Quaternion turn = Quaternion.Inverse(part.localRotation) * pose.localRotation;
                turn.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f)
                {
                    angle -= 360f;
                }

                bool turns = Mathf.Abs(angle) > 0.01f && axis.sqrMagnitude > 0.5f;
                _axes[i] = turns ? axis.normalized : Vector3.up;
                _angles[i] = turns ? angle : 0f;
                _offsets[i] = pose.localPosition - part.localPosition;
                _motions[i] = motions[i];
            }
        }

        /// <summary>The number of moving parts.</summary>
        public int Count => _parts.Length;

        /// <summary>The motion time: seconds of full motion so far.</summary>
        public float Seconds => _seconds;

        /// <summary>The moving part at an index, for tests and tools.</summary>
        /// <param name="index">0 to <see cref="Count"/> - 1.</param>
        /// <returns>The part.</returns>
        public Transform PartAt(int index)
        {
            return _parts[index];
        }

        /// <summary>
        /// The rig of a model instance: its moving parts with their poses, or null when it has none (a cottage). Walks the hierarchy once;
        /// call it when a body is built, not every frame.
        /// </summary>
        /// <param name="body">The model instance.</param>
        /// <returns>The rig, or null.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="body"/> is null.</exception>
        public static MotionRig? Find(Transform body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            var parts = new List<Transform>();
            var poses = new List<Transform>();
            var motions = new List<PartMotion>();
            Collect(body, parts, poses, motions);
            return parts.Count == 0 ? null : new MotionRig(parts, poses, motions);
        }

        /// <summary>
        /// Moves the motion time on by <paramref name="deltaSeconds"/> at the given <paramref name="intensity"/> and poses every part. At
        /// intensity 1 the parts move fully; between 1 and 0 they move slower and less far, and at 0 they are at rest. Allocates nothing.
        /// </summary>
        /// <param name="deltaSeconds">The frame time, in seconds.</param>
        /// <param name="intensity">How much the parts move, 0 to 1 (clamped).</param>
        public void Advance(float deltaSeconds, float intensity)
        {
            float amount = Mathf.Clamp01(intensity);
            _seconds += deltaSeconds * amount;
            for (int i = 0; i < _parts.Length; i++)
            {
                PartMotion motion = _motions[i];
                float weight = motion.Weight(_seconds);
                Transform part = _parts[i];
                if (motion.Kind == MotionKind.Spin)
                {
                    float turn = weight - Mathf.Floor(weight);
                    part.localRotation = _restRotations[i] * Quaternion.AngleAxis(360f * turn, _axes[i]);
                    continue;
                }

                float w = weight * amount;
                part.localRotation = _restRotations[i] * Quaternion.AngleAxis(_angles[i] * w, _axes[i]);
                part.localPosition = _restPositions[i] + _offsets[i] * w;
            }
        }

        /// <summary>Puts every part back at rest and the motion time back to 0.</summary>
        public void Rest()
        {
            _seconds = 0f;
            for (int i = 0; i < _parts.Length; i++)
            {
                _parts[i].localRotation = _restRotations[i];
                _parts[i].localPosition = _restPositions[i];
            }
        }

        /// <summary>Adds every moving part at or below <paramref name="node"/> with its pose and motion, depth first.</summary>
        private static void Collect(Transform node, List<Transform> parts, List<Transform> poses, List<PartMotion> motions)
        {
            Transform? parent = node.parent;
            if (parent != null && PartMotions.TryGet(node.name, out PartMotion motion))
            {
                Transform? pose = parent.Find(node.name + PartMotions.PoseSuffix);
                if (pose != null)
                {
                    parts.Add(node);
                    poses.Add(pose);
                    motions.Add(motion);
                }
            }

            for (int i = 0; i < node.childCount; i++)
            {
                Collect(node.GetChild(i), parts, poses, motions);
            }
        }
    }
}
