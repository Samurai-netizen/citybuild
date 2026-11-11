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

    /// <summary>Sends a free worker with goods from one building's store to another building (<c>04</c> section 18).</summary>
    /// <param name="SourceId">The id of the building whose store the goods come from.</param>
    /// <param name="TargetId">The id of the building they go to.</param>
    /// <param name="Item">The good's id.</param>
    /// <param name="Amount">How much of the source's stock.</param>
    public sealed record StartTrip(int SourceId, int TargetId, string Item, Amount Amount) : Command
    {
        /// <inheritdoc/>
        public override MutationResult Mutate(CityState city, GameContent content) =>
            TripRules.StartTrip(city, content, SourceId, TargetId, new ItemId(Item), Amount);

        /// <summary>The text a failing property prints, for example <c>StartTrip 3->5 logs half</c>.</summary>
        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"StartTrip {SourceId}->{TargetId} {Item} {Amount}");
    }

    /// <summary>Sets the route of a courier station's slot (<c>04</c> section 19); no stops clears it.</summary>
    /// <param name="StationId">The courier station's id.</param>
    /// <param name="Slot">The slot.</param>
    /// <param name="Stops">The stops in order.</param>
    public sealed record SetRoute(int StationId, int Slot, IReadOnlyList<RouteStop> Stops) : Command
    {
        /// <inheritdoc/>
        public override MutationResult Mutate(CityState city, GameContent content) => CourierRules.SetRoute(city, content, StationId, Slot, Stops);

        /// <summary>
        /// Equal when the text is: the station, the slot and every stop with its actions (a record would compare the stop list by reference,
        /// so two generations of one seed would differ).
        /// </summary>
        public bool Equals(SetRoute? other) => other is not null && ToString() == other.ToString();

        /// <summary>The hash of the text, in step with <see cref="Equals(SetRoute)"/>.</summary>
        public override int GetHashCode() => ToString().GetHashCode(StringComparison.Ordinal);

        /// <summary>The text a failing property prints, for example <c>SetRoute 4/1 3[Take logs all] 5[Put logs half]</c>.</summary>
        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"SetRoute {StationId}/{Slot}")
            + string.Concat(Stops.Select(stop => string.Create(CultureInfo.InvariantCulture, $" {stop.BuildingId}[")
                + string.Join("; ", stop.Actions.Select(action => $"{action.Kind} {action.Item} {action.Amount}")) + "]"));
    }

    /// <summary>
    /// Lets <paramref name="Seconds"/> pass: one settlement over that window, with the player present all of it or absent from
    /// <paramref name="AbsentAfter"/> seconds after its start (<c>04</c> section 2).
    /// </summary>
    /// <param name="Seconds">The seconds to pass; the engine counts a negative value as 0 (<c>04</c> section 1).</param>
    /// <param name="AbsentAfter">Null when the player is present the whole window; else the seconds after the window's start at which the
    /// absence begins (the engine's <c>absentFrom</c> is the start plus this).</param>
    public sealed record Advance(long Seconds, long? AbsentAfter = null) : CityOperation
    {
        /// <summary>Advances the city by <see cref="Seconds"/>; time passing is never rejected, so the report is never null.</summary>
        public override SettlementReport? TryApply(CityState city, GameContent content) =>
            Simulator.Advance(city, content, Seconds, AbsentFrom(city.Clock));

        /// <summary>The engine's <c>absentFrom</c> for a window that starts at <paramref name="start"/>: null when the player stays present.</summary>
        public long? AbsentFrom(long start) => AbsentAfter is { } after ? start + after : null;

        /// <summary>The text a failing property prints, for example <c>Advance 600</c> or <c>Advance 3600 absent after 120</c>.</summary>
        public override string ToString() => AbsentAfter is { } after
            ? string.Create(CultureInfo.InvariantCulture, $"Advance {Seconds} absent after {after}")
            : string.Create(CultureInfo.InvariantCulture, $"Advance {Seconds}");
    }

    /// <summary>Cancels one delivery line of a courier, or all its lines and its route (<c>04</c> section 8, CR-0008 D18).</summary>
    /// <param name="StationId">The courier station's id.</param>
    /// <param name="Slot">The courier's slot.</param>
    /// <param name="LineId">The line to cancel, or null for all.</param>
    public sealed record CancelDelivery(int StationId, int Slot, int? LineId) : Command
    {
        /// <inheritdoc/>
        public override MutationResult Mutate(CityState city, GameContent content) => CourierRules.CancelDelivery(city, content, StationId, Slot, LineId);

        /// <summary>The text a failing property prints, for example <c>CancelDelivery 4/1 line 7</c> or <c>CancelDelivery 4/1 all</c>.</summary>
        public override string ToString() => LineId is { } line
            ? string.Create(CultureInfo.InvariantCulture, $"CancelDelivery {StationId}/{Slot} line {line}")
            : string.Create(CultureInfo.InvariantCulture, $"CancelDelivery {StationId}/{Slot} all");
    }

    /// <summary>Re-energizes every courier of the city (<c>04</c> section 8).</summary>
    public sealed record ReenergizeCouriers : Command
    {
        /// <inheritdoc/>
        public override MutationResult Mutate(CityState city, GameContent content) => CourierRules.ReenergizeCouriers(city, content);

        /// <summary>The text a failing property prints: <c>ReenergizeCouriers</c>.</summary>
        public override string ToString() => "ReenergizeCouriers";
    }
}
