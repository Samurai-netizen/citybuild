#nullable enable

using CityBuilder.Simulation.State;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The object that stands for one building of the city on the screen (M0-44): it remembers which building it shows (id, type, level, phase) and
    /// the era it was drawn for, so <see cref="CityView"/> can tell what changed, and it holds the <c>Body</c> object that the factory makes: an
    /// instance of the building's model (CR-0005), or, for a type or level with no model, the primitives of a look. It also holds the anchors
    /// of the model, found by name once when the body is made. A thin component: it decides nothing and never calls the server. Only
    /// <see cref="BuildingViewFactory"/> changes it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingView : MonoBehaviour
    {
        /// <summary>The name of the empty object above the roof where the status bubble and the progress ring go (every building model has one).</summary>
        public const string StatusAnchorName = "Anchor_Status";

        /// <summary>The name of the empty object at the door, where workers walk in and out (every building model has one).</summary>
        public const string DoorAnchorName = "Anchor_Door";

        /// <summary>The name of the empty object at the chimney top, for smoke (only models with a chimney have one).</summary>
        public const string SmokeAnchorName = "Anchor_Smoke";

        /// <summary>The name of the empty object where a stockpile prop would stand (the Woodcutter and the Sawmill have one).</summary>
        public const string StockpileAnchorName = "Anchor_Stockpile";

        /// <summary>The name of the Sawmill's water wheel, a mesh that turns around its local X axis (a later task animates it).</summary>
        public const string WheelName = "Wheel";

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

        /// <summary>The model the body is an instance of: the building's model, or the construction site while it is built or upgraded. Null when the body is primitives.</summary>
        public GameObject? Model { get; internal set; }

        /// <summary>The primitive look the body is built from, or null when the body is an instance of a <see cref="Model"/>.</summary>
        public BuildingLook? Look { get; internal set; }

        /// <summary>The object that holds the building: the model's instance or the look's primitives. It is stretched upward for the level only when it is primitives. Null until the factory has built it.</summary>
        public Transform? Body { get; internal set; }

        /// <summary>How tall the body is drawn now, in thousandths of its level 1 height (<see cref="BuildingLooks.HeightPermille"/>); a model is always 1000.</summary>
        public int HeightPermille { get; internal set; }

        /// <summary>The number of primitives in the body; 0 for a model.</summary>
        public int PartCount => Look == null || Body == null ? 0 : Body.childCount;

        /// <summary>Where the status bubble and the progress ring go (<see cref="StatusAnchorName"/>), or null when the body has none.</summary>
        public Transform? StatusAnchor { get; internal set; }

        /// <summary>Where workers walk in and out (<see cref="DoorAnchorName"/>), or null.</summary>
        public Transform? DoorAnchor { get; internal set; }

        /// <summary>Where smoke would rise (<see cref="SmokeAnchorName"/>), or null when the model has no chimney.</summary>
        public Transform? SmokeAnchor { get; internal set; }

        /// <summary>Where a stockpile prop would stand (<see cref="StockpileAnchorName"/>), or null.</summary>
        public Transform? StockpileAnchor { get; internal set; }

        /// <summary>The Sawmill's water wheel (<see cref="WheelName"/>), or null. Nothing turns it yet.</summary>
        public Transform? Wheel { get; internal set; }
    }
}
