#nullable enable

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// How a moving part of a model moves (visual v2, CR-0008-T2). The art gives each moving part a pose, the far end of its motion, as a
    /// sibling empty <c>&lt;part&gt;_Pose</c>; <see cref="PartMotions"/> gives the kind by the part's name. Display only.
    /// </summary>
    public enum MotionKind
    {
        /// <summary>Turns all the way round the axis from the part to its pose, one turn per cycle (a water wheel, a coin sign).</summary>
        Spin,

        /// <summary>Rises slowly to the pose, drops back fast and rests a moment (an axe, a hammer).</summary>
        Chop,

        /// <summary>Glides to the pose and back (a saw stroke, a hoist).</summary>
        PingPong,

        /// <summary>Swings to the pose, back through rest to the mirror of the pose and back (a flag, legs and arms).</summary>
        Wave,
    }
}
