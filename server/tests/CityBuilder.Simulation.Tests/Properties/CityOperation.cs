using System.Globalization;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Rules;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Tests.Properties;

/// <summary>
/// One step of a generated city's history: a player's command, run as the server runs it (the mutation through <see cref="CityRules"/>, then
/// a zero-length advance, <c>04</c> sections 7 and 8), or time passing (<see cref="Simulator.Advance"/>). Nothing sets state directly. The
/// text form is what a failing property prints, one operation per line.
/// </summary>
internal abstract record CityOperation
{
    /// <summary>
    /// Runs the operation on <paramref name="city"/>. Returns the report of the advance (for a command, of its zero-length advance), or null
    /// when the rules rejected the command, which then changed nothing.
    /// </summary>
    public abstract SettlementReport? TryApply(CityState city, GameContent content);

    /// <summary>A player's command: the mutation alone (<see cref="Mutate"/>), then, when it was applied, a zero-length advance.</summary>
    public abstract record Command : CityOperation
    {
        /// <summary>Runs the mutation and, when the rules applied it, its zero-length advance; null when they rejected it.</summary>
        public sealed override SettlementReport? TryApply(CityState city, GameContent content) =>
            Mutate(city, content).IsOk ? Simulator.Advance(city, content, 0) : null;

        /// <summary>Runs only the mutation through <see cref="CityRules"/>, without the zero-length advance that follows it.</summary>
        public abstract MutationResult Mutate(CityState city, GameContent content);
    }

    /// <summary>Places a building of <paramref name="Type"/> on the tile.</summary>
    /// <param name="Type">The building type's id.</param>
    /// <param name="X">The tile's x coordinate.</param>
    /// <param name="Y">The tile's y coordinate.</param>
    public sealed record PlaceBuilding(string Type, int X, int Y) : Command
    {
        /// <inheritdoc/>
        public override MutationResult Mutate(CityState city, GameContent content) =>
            CityRules.PlaceBuilding(city, content, new BuildingTypeId(Type), X, Y);

        /// <summary>The text a failing property prints, for example <c>PlaceBuilding hut (2,3)</c>.</summary>
        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"PlaceBuilding {Type} ({X},{Y})");
    }

    /// <summary>Upgrades the building to its next level.</summary>
    /// <param name="BuildingId">The id of the building to upgrade.</param>
    public sealed record UpgradeBuilding(int BuildingId) : Command
    {
        /// <inheritdoc/>
        public override MutationResult Mutate(CityState city, GameContent content) => CityRules.UpgradeBuilding(city, content, BuildingId);

        /// <summary>The text a failing property prints, for example <c>UpgradeBuilding 4</c>.</summary>
        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"UpgradeBuilding {BuildingId}");
    }

    /// <summary>Sets the building's workers.</summary>
    /// <param name="BuildingId">The id of the building.</param>
    /// <param name="Workers">The workers it gets, in place of those it had.</param>
    public sealed record AssignWorkers(int BuildingId, int Workers) : Command
    {
        /// <inheritdoc/>
        public override MutationResult Mutate(CityState city, GameContent content) => CityRules.AssignWorkers(city, content, BuildingId, Workers);

        /// <summary>The text a failing property prints, for example <c>AssignWorkers 4 2</c> (building 4 gets 2 workers).</summary>
        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"AssignWorkers {BuildingId} {Workers}");
    }

    /// <summary>Starts researching the node.</summary>
    /// <param name="ResearchId">The research node's id.</param>
    public sealed record StartResearch(string ResearchId) : Command
    {
        /// <inheritdoc/>
        public override MutationResult Mutate(CityState city, GameContent content) => CityRules.StartResearch(city, content, new ResearchId(ResearchId));

        /// <summary>The text a failing property prints, for example <c>StartResearch carpentry</c>.</summary>
        public override string ToString() => $"StartResearch {ResearchId}";
    }

    /// <summary>Lets <paramref name="Seconds"/> pass: one settlement over that window.</summary>
    /// <param name="Seconds">The seconds to pass; the engine counts a negative value as 0 (<c>04</c> section 1).</param>
    public sealed record Advance(long Seconds) : CityOperation
    {
        /// <summary>Advances the city by <see cref="Seconds"/>; time passing is never rejected, so the report is never null.</summary>
        public override SettlementReport? TryApply(CityState city, GameContent content) => Simulator.Advance(city, content, Seconds);

        /// <summary>The text a failing property prints, for example <c>Advance 600</c>.</summary>
        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"Advance {Seconds}");
    }
}
