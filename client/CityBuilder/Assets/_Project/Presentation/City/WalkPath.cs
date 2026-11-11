#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The tiles of one walk with the share of the walk's time each step takes (CR-0008-T2 part D, <c>04</c> section 17), so a walker's place at
    /// any moment of the walk is found without allocating. The walk moves from tile centre to tile centre, so every turn is 90°. Display only:
    /// the walk's start and end times come from the snapshot, this class only spreads them over the steps.
    /// </summary>
    public sealed class WalkPath
    {
        /// <summary>The column of each tile's centre, in tile units (tile column + 0.5).</summary>
        private readonly float[] _x;

        /// <summary>The row of each tile's centre, in tile units (tile row + 0.5).</summary>
        private readonly float[] _z;

        /// <summary>The fraction of the walk at which each step ends, ascending, the last 1; one entry per step.</summary>
        private readonly float[] _end;

        /// <summary>Creates a path.</summary>
        /// <param name="tiles">The walk's tiles, start and end included, each next to the one before; at least one.</param>
        /// <param name="stepWeights">The weight (time) of each step, one per step (<c>tiles.Count - 1</c>), each above 0.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">There is no tile, or the weights do not match the steps.</exception>
        public WalkPath(IReadOnlyList<(int X, int Y)> tiles, IReadOnlyList<long> stepWeights)
        {
            if (tiles == null)
            {
                throw new ArgumentNullException(nameof(tiles));
            }

            if (stepWeights == null)
            {
                throw new ArgumentNullException(nameof(stepWeights));
            }

            if (tiles.Count == 0 || stepWeights.Count != tiles.Count - 1)
            {
                throw new ArgumentException("A path needs one tile and one weight per step.", nameof(tiles));
            }

            _x = new float[tiles.Count];
            _z = new float[tiles.Count];
            for (int i = 0; i < tiles.Count; i++)
            {
                _x[i] = tiles[i].X + 0.5f;
                _z[i] = tiles[i].Y + 0.5f;
            }

            _end = new float[stepWeights.Count];
            double total = 0;
            for (int i = 0; i < stepWeights.Count; i++)
            {
                total += Math.Max(1L, stepWeights[i]);
            }

            double running = 0;
            for (int i = 0; i < stepWeights.Count; i++)
            {
                running += Math.Max(1L, stepWeights[i]);
                _end[i] = (float)(running / total);
            }
        }

        /// <summary>A path of one tile: a walker that stands there.</summary>
        /// <param name="x">The tile's column.</param>
        /// <param name="y">The tile's row.</param>
        /// <returns>The path.</returns>
        public static WalkPath Standing(int x, int y)
        {
            return new WalkPath(new[] { (x, y) }, Array.Empty<long>());
        }

        /// <summary>The number of tiles, start and end included.</summary>
        public int TileCount => _x.Length;

        /// <summary>The number of steps (tiles minus one).</summary>
        public int StepCount => _end.Length;

        /// <summary>
        /// Where the walker is after <paramref name="fraction"/> of the walk's time, and which way it faces. Allocates nothing.
        /// </summary>
        /// <param name="fraction">0 at the start to 1 at the end (clamped).</param>
        /// <param name="x">The column, in tile units, with the tile's centre at +0.5.</param>
        /// <param name="z">The row, in tile units.</param>
        /// <param name="directionX">-1, 0 or 1: the step's direction in columns (0 when the path has no step).</param>
        /// <param name="directionZ">-1, 0 or 1: the step's direction in rows.</param>
        public void Sample(float fraction, out float x, out float z, out int directionX, out int directionZ)
        {
            if (_end.Length == 0)
            {
                x = _x[0];
                z = _z[0];
                directionX = 0;
                directionZ = 0;
                return;
            }

            float f = fraction < 0f ? 0f : (fraction > 1f ? 1f : fraction);
            int step = 0;
            while (step < _end.Length - 1 && f > _end[step])
            {
                step++;
            }

            float from = step == 0 ? 0f : _end[step - 1];
            float length = _end[step] - from;
            float local = length <= 0f ? 1f : Mathf.Clamp01((f - from) / length);
            x = _x[step] + ((_x[step + 1] - _x[step]) * local);
            z = _z[step] + ((_z[step + 1] - _z[step]) * local);
            directionX = Math.Sign(_x[step + 1] - _x[step]);
            directionZ = Math.Sign(_z[step + 1] - _z[step]);
        }
    }
}
