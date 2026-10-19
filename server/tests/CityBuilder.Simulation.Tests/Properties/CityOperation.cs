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

    private static SettlementReport? Command(CityState city, GameContent content, MutationResult result) =>
        result.IsOk ? Simulator.Advance(city, content, 0) : null;

    /// <summary>Places a building of <paramref name="Type"/> on the tile.</summary>
    public sealed record PlaceBuilding(string Type, int X, int Y) : CityOperation
    {
        public override SettlementReport? TryApply(CityState city, GameContent content) =>
            Command(city, content, CityRules.PlaceBuilding(city, content, new BuildingTypeId(Type), X, Y));

        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"PlaceBuilding {Type} ({X},{Y})");
    }

    /// <summary>Upgrades the building to its next level.</summary>
    public sealed record UpgradeBuilding(int BuildingId) : CityOperation
    {
        public override SettlementReport? TryApply(CityState city, GameContent content) =>
            Command(city, content, CityRules.UpgradeBuilding(city, content, BuildingId));

        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"UpgradeBuilding {BuildingId}");
    }

    /// <summary>Sets the building's workers.</summary>
    public sealed record AssignWorkers(int BuildingId, int Workers) : CityOperation
    {
        public override SettlementReport? TryApply(CityState city, GameContent content) =>
            Command(city, content, CityRules.AssignWorkers(city, content, BuildingId, Workers));

        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"AssignWorkers {BuildingId} {Workers}");
    }

    /// <summary>Starts researching the node.</summary>
    public sealed record StartResearch(string ResearchId) : CityOperation
    {
        public override SettlementReport? TryApply(CityState city, GameContent content) =>
            Command(city, content, CityRules.StartResearch(city, content, new ResearchId(ResearchId)));

        public override string ToString() => $"StartResearch {ResearchId}";
    }

    /// <summary>Lets <paramref name="Seconds"/> pass: one settlement over that window.</summary>
    public sealed record Advance(long Seconds) : CityOperation
    {
        public override SettlementReport? TryApply(CityState city, GameContent content) => Simulator.Advance(city, content, Seconds);

        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"Advance {Seconds}");
    }
}
