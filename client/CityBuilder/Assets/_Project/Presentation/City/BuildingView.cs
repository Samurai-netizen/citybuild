#nullable enable

using CityBuilder.Simulation.State;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The object that stands for one building of the city on the screen (M0-44): it remembers which building it shows (id, type, level, phase) and
    /// the era it was drawn for, so <see cref="CityView"/> can tell what changed, and it holds the <c>Body</c> object that the factory fills with
    /// the look's primitives. A thin component: it decides nothing and never calls the server. Only <see cref="BuildingViewFactory"/> changes it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingView : MonoBehaviour
    {
        /// <summary>The building's id in its city.</summary>
        public int BuildingId { get; internal set; }

        /// <summary>The building's type id in the content.</summary>
        public string TypeId { get; internal set; } = string.Empty;

        /// <summary>The level shown.</summary>
        public int Level { get; internal set; }

        /// <summary>The phase shown.</summary>
        public BuildingPhase Phase { get; internal set; }

        /// <summary>The era the look was chosen for.</summary>
        public int Era { get; internal set; }

        /// <summary>The look the body is built from.</summary>
        public BuildingLook Look { get; internal set; } = BuildingLooks.Fallback;

        /// <summary>The object that holds the look's primitives; it is stretched upward for the level and the phase. Null until the factory has built it.</summary>
        public Transform? Body { get; internal set; }

        /// <summary>How tall the body is drawn now, in thousandths of its level 1 height (<see cref="BuildingLooks.HeightPermille"/>).</summary>
        public int HeightPermille { get; internal set; }

        /// <summary>The number of primitives in the body.</summary>
        public int PartCount => Body == null ? 0 : Body.childCount;
    }
}
