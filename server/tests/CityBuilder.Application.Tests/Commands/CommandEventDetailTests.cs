using CityBuilder.Application.Commands;
using CityBuilder.Application.Commands.AssignWorkers;
using CityBuilder.Application.Commands.CancelDelivery;
using CityBuilder.Application.Commands.PlaceBuilding;
using CityBuilder.Application.Commands.ReenergizeCouriers;
using CityBuilder.Application.Commands.Roads;
using CityBuilder.Application.Commands.SetPaused;
using CityBuilder.Application.Commands.SetRoute;
using CityBuilder.Application.Commands.StartResearch;
using CityBuilder.Application.Commands.StartTrip;
using CityBuilder.Application.Commands.UpgradeBuilding;
using CityBuilder.Application.Tests.Cities;
using CityBuilder.Simulation.State;
using static CityBuilder.Application.Tests.Cities.SettleFixture;

namespace CityBuilder.Application.Tests.Commands;

/// <summary>
/// <see cref="ICityCommand.EventDetail"/> of every command (CR-0009, the <c>detail</c> of its player event): the content id it names when the
/// content (or the city) has it, <see langword="null"/> for an id it doesn't have and for a command that names none. The cases run against
/// <see cref="PipelineFixture.TwoSawmillsRepository"/>'s city: cottage 1, woodcutter 2, sawmills 3 and 5. A command type without a case fails
/// <see cref="EveryCityCommand_HasAnEventDetailCase"/>, so a later command can't skip its detail.
/// </summary>
public sealed class CommandEventDetailTests
{
    /// <summary>A request id for every case; it plays no part in the detail.</summary>
    private static readonly Guid Id = new("0b7e9c4a-0000-4000-8000-0000000000de");

    /// <summary>The cases by name: a command and the detail it gives.</summary>
    private static readonly Dictionary<string, (ICityCommand Command, string? Detail)> CasesByName = new()
    {
        ["place-building known"] = (new PlaceBuildingCommand(Id, "cottage", 1, 1), "cottage"),
        ["place-building unknown"] = (new PlaceBuildingCommand(Id, "castle", 1, 1), null),
        ["place-building not an id"] = (new PlaceBuildingCommand(Id, "Not An Id", 1, 1), null),
        ["upgrade-building known"] = (new UpgradeBuildingCommand(Id, 3), "sawmill"),
        ["upgrade-building unknown"] = (new UpgradeBuildingCommand(Id, 99), null),
        ["assign-workers known"] = (new AssignWorkersCommand(Id, 2, 1), "woodcutter"),
        ["assign-workers unknown"] = (new AssignWorkersCommand(Id, 99, 1), null),
        ["set-paused known"] = (new SetPausedCommand(Id, 5, true), "sawmill"),
        ["set-paused unknown"] = (new SetPausedCommand(Id, 99, true), null),
        ["start-research known"] = (new StartResearchCommand(Id, "forestry"), "forestry"),
        ["start-research unknown"] = (new StartResearchCommand(Id, "alchemy"), null),
        ["start-trip known"] = (new StartTripCommand(Id, 2, 3, "logs", "all"), "logs"),
        ["start-trip unknown"] = (new StartTripCommand(Id, 2, 3, "gold", "all"), null),
        ["set-route"] = (new SetRouteCommand(Id, 1, 0, []), null),
        ["cancel-delivery"] = (new CancelDeliveryCommand(Id, 1, 0, null), null),
        ["reenergize-couriers"] = (new ReenergizeCouriersCommand(Id), null),
        ["place-roads"] = (new PlaceRoadsCommand(Id, [new CommandTile(1, 1)]), null),
        ["remove-roads"] = (new RemoveRoadsCommand(Id, [new CommandTile(1, 1)]), null),
        ["upgrade-roads"] = (new UpgradeRoadsCommand(Id, [new CommandTile(1, 1)]), null),
        ["place-test known"] = (new PlaceTestCommand(Id, "sawmill", 1, 1), "sawmill"),
        ["place-test unknown"] = (new PlaceTestCommand(Id, "castle", 1, 1), null),
        ["upgrade-test known"] = (new UpgradeTestCommand(Id, 1), "cottage"),
        ["upgrade-test unknown"] = (new UpgradeTestCommand(Id, 99), null),
    };

    /// <summary>The names of the cases, the theory's data.</summary>
    public static TheoryData<string> Cases => [.. CasesByName.Keys];

    /// <summary>Each command gives the detail of its case, on the settled city before any mutation.</summary>
    /// <param name="name">The case.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void EventDetail_Case_IsTheContentIdOrNull(string name)
    {
        (ICityCommand command, string? detail) = CasesByName[name];
        CityState city = PipelineFixture.TwoSawmillsRepository().Stored(PlayerId).State;

        Assert.Equal(detail, command.EventDetail(city, SettleFixture.Content));
    }

    /// <summary>
    /// Every <see cref="ICityCommand"/> of the Application (and the test commands) has a case above, so a command added later by the recipe
    /// (<c>docs/how-to/add-command.md</c>) fails here until its detail is stated and tested.
    /// </summary>
    [Fact]
    public void EveryCityCommand_HasAnEventDetailCase()
    {
        Type[] commandTypes = [.. typeof(ICityCommand).Assembly.GetTypes().Concat(typeof(CommandEventDetailTests).Assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false, IsNested: false } && typeof(ICityCommand).IsAssignableFrom(type))];
        HashSet<Type> withACase = [.. CasesByName.Values.Select(entry => entry.Command.GetType())];

        Assert.NotEmpty(commandTypes);
        Assert.Empty(commandTypes.Where(type => !withACase.Contains(type)).Select(type => type.Name));
    }
}
