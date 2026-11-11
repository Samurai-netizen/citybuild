#nullable enable

using System;
using System.Collections.Generic;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The moving parts the art knows (visual v2, CR-0008-T2), by name: a part of a model is a child mesh with one of these names, or the
    /// name and a number (<c>Flag_2</c>), next to an empty called the part's name and <see cref="PoseSuffix"/>. The rows are the same as
    /// <c>MOTIONS</c> in <c>visual-v2/blender/lib.py</c> and <c>visual-v2/MOTIONS.md</c>. A code table like <see cref="BuildingLooks"/>, never a
    /// game number; built once and never changed. Display only.
    /// </summary>
    public static class PartMotions
    {
        /// <summary>What the empty that marks a part's pose adds to the part's name.</summary>
        public const string PoseSuffix = "_Pose";

        /// <summary>The Sawmill's water wheel.</summary>
        public const string Wheel = "Wheel";

        /// <summary>The motions by part name.</summary>
        private static readonly Dictionary<string, PartMotion> Table = new Dictionary<string, PartMotion>(StringComparer.Ordinal)
        {
            [Wheel] = new PartMotion(MotionKind.Spin, 4f, 0f),
            ["Sign"] = new PartMotion(MotionKind.Spin, 3f, 0f),
            ["Vane"] = new PartMotion(MotionKind.Spin, 7f, 0f),
            ["Axe"] = new PartMotion(MotionKind.Chop, 1.4f, 0f),
            ["Hammer"] = new PartMotion(MotionKind.Chop, 0.8f, 0f),
            ["Saw"] = new PartMotion(MotionKind.PingPong, 0.9f, 0f),
            ["Hoist"] = new PartMotion(MotionKind.PingPong, 5f, 0f),
            ["Quill"] = new PartMotion(MotionKind.Wave, 1.1f, 0f),
            ["Flag"] = new PartMotion(MotionKind.Wave, 1.6f, 0f),
            ["Leg_L"] = new PartMotion(MotionKind.Wave, 0.6f, 0f),
            ["Leg_R"] = new PartMotion(MotionKind.Wave, 0.6f, 0.5f),
            ["Arm_L"] = new PartMotion(MotionKind.Wave, 0.6f, 0.5f),
            ["Arm_R"] = new PartMotion(MotionKind.Wave, 0.6f, 0f),
        };

        /// <summary>The number of part names in the table.</summary>
        public static int Count => Table.Count;

        /// <summary>
        /// The motion of a part name: its own row, or for a name that ends in an underscore and digits (<c>Flag_2</c>) the row of the name
        /// before them. Allocates only for such a numbered name.
        /// </summary>
        /// <param name="partName">The name of a child object of a model.</param>
        /// <param name="motion">The motion, or default.</param>
        /// <returns>True when the name is a moving part.</returns>
        public static bool TryGet(string partName, out PartMotion motion)
        {
            if (partName == null)
            {
                throw new ArgumentNullException(nameof(partName));
            }

            if (Table.TryGetValue(partName, out motion))
            {
                return true;
            }

            int cut = partName.LastIndexOf('_');
            if (cut <= 0 || cut == partName.Length - 1)
            {
                return false;
            }

            for (int i = cut + 1; i < partName.Length; i++)
            {
                if (partName[i] < '0' || partName[i] > '9')
                {
                    return false;
                }
            }

            return Table.TryGetValue(partName.Substring(0, cut), out motion);
        }

        /// <summary>Whether the table has an exact row for this name (no numbered names).</summary>
        /// <param name="partName">A part name.</param>
        /// <returns>True when the name has its own row.</returns>
        public static bool Has(string partName)
        {
            return Table.ContainsKey(partName);
        }
    }
}
