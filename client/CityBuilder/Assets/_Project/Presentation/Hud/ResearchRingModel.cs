#nullable enable

using System;
using CityBuilder.Client.Presentation.Build;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Display;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Presentation.Hud
{
    /// <summary>
    /// The research ring of the HUD as plain values: how far the running research has come (permille, from
    /// <c>ProgressDisplay.ResearchProgressPermille</c> at the displayed city's clock), its name and the time left. With no research running it says so
    /// and the ring is empty. Immutable; two rings that say the same compare equal.
    /// </summary>
    public sealed class ResearchRingModel : IEquatable<ResearchRingModel>
    {
        /// <summary>What the ring says while no research runs.</summary>
        public const string IdleName = "No research";

        /// <summary>Creates a ring; use <see cref="Create"/>.</summary>
        private ResearchRingModel(bool isActive, int permille, string name, string timeLeftText)
        {
            IsActive = isActive;
            Permille = permille;
            Name = name;
            TimeLeftText = timeLeftText;
        }

        /// <summary>Whether a research runs.</summary>
        public bool IsActive { get; }

        /// <summary>How far the research has come, 0 to 1000; 0 when none runs.</summary>
        public int Permille { get; }

        /// <summary>The node's name from the content (its id when the content does not know it), or <see cref="IdleName"/>.</summary>
        public string Name { get; }

        /// <summary>The time left, such as <c>12m 30s</c> (<see cref="DurationText"/>); <c>0s</c> once the end has passed and the next snapshot has not arrived; empty when none runs.</summary>
        public string TimeLeftText { get; }

        /// <summary>Makes the ring for <paramref name="city"/>, the city the client displays now; its clock is the clock shown.</summary>
        /// <param name="content">The game content the node's name comes from.</param>
        /// <param name="city">The displayed city.</param>
        public static ResearchRingModel Create(GameContent content, CityState city)
        {
            ActiveResearch? research = city.ActiveResearch;
            if (research == null)
            {
                return new ResearchRingModel(false, 0, IdleName, string.Empty);
            }

            string name = content.TryGetResearch(research.Id, out ResearchDefinition? definition) ? definition.Name : research.Id.Value;
            long secondsLeft = Math.Max(0, research.EndsAt - city.Clock);
            return new ResearchRingModel(
                true,
                ProgressDisplay.ResearchProgressPermille(city, city.Clock),
                name,
                DurationText.Format(secondsLeft));
        }

        /// <summary>True when every value is the same: the view has nothing to redraw.</summary>
        public bool Equals(ResearchRingModel? other)
        {
            return other != null
                && IsActive == other.IsActive
                && Permille == other.Permille
                && Name == other.Name
                && TimeLeftText == other.TimeLeftText;
        }

        /// <summary>See <see cref="Equals(ResearchRingModel?)"/>.</summary>
        public override bool Equals(object? obj) => Equals(obj as ResearchRingModel);

        /// <summary>A hash of the values that go with <see cref="Equals(ResearchRingModel?)"/>.</summary>
        public override int GetHashCode() => Permille ^ Name.GetHashCode() ^ TimeLeftText.GetHashCode();
    }
}
