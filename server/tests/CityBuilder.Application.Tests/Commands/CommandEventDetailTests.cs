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
    private static readonly Guid _id = new("0b7e9c4a-0000-4000-8000-0000000000de");

    /// <summary>The cases by name: a command and the detail it gives.</summary>
    private static readonly Dictionary<string, (ICityCommand Command, string? Detail)> _casesByName = new()
    {
        ["place-building known"] = (new PlaceBuildingCommand(_id, "cottage", 1, 1), "cottage"),
        ["place-building unknown"] = (new PlaceBuildingCommand(_id, "castle", 1, 1), null),
        ["place-building not an id"] = (new PlaceBuildingCommand(_id, "Not An Id", 1, 1), null),
        ["upgrade-building known"] = (new UpgradeBuildingCommand(_id, 3), "sawmill"),
        ["upgrade-building unknown"] = (new UpgradeBuildingCommand(_id, 99), null),
        ["assign-workers known"] = (new AssignWorkersCommand(_id, 2, 1), "woodcutter"),
        ["assign-workers unknown"] = (new AssignWorkersCommand(_id, 99, 1), null),
        ["set-paused known"] = (new SetPausedCommand(_id, 5, true), "sawmill"),
        ["set-paused unknown"] = (new SetPausedCommand(_id, 99, true), null),
        ["start-research known"] = (new StartResearchCommand(_id, "forestry"), "forestry"),
        ["start-research unknown"] = (new StartResearchCommand(_id, "alchemy"), null),
        ["start-trip known"] = (new StartTripCommand(_id, 2, 3, "logs", "all"), "logs"),
        ["start-trip unknown"] = (new StartTripCommand(_id, 2, 3, "gold", "all"), null),
        ["set-route"] = (new SetRouteCommand(_id, 1, 0, []), null),
        ["cancel-delivery"] = (new CancelDeliveryCommand(_id, 1, 0, null), null),
        ["reenergize-couriers"] = (new ReenergizeCouriersCommand(_id), null),
        ["place-roads"] = (new PlaceRoadsCommand(_id, [new CommandTile(1, 1)]), null),
        ["remove-roads"] = (new RemoveRoadsCommand(_id, [new CommandTile(1, 1)]), null),
        ["upgrade-roads"] = (new UpgradeRoadsCommand(_id, [new CommandTile(1, 1)]), null),
        ["place-test known"] = (new PlaceTestCommand(_id, "sawmill", 1, 1), "sawmill"),
        ["place-test unknown"] = (new PlaceTestCommand(_id, "castle", 1, 1), null),
        ["upgrade-test known"] = (new UpgradeTestCommand(_id, 1), "cottage"),
        ["upgrade-test unknown"] = (new UpgradeTestCommand(_id, 99), null),
    };

    /// <summary>The names of the cases, the theory's data.</summary>
    public static TheoryData<string> Cases => [.. _casesByName.Keys];

    /// <summary>Each command gives the detail of its case, on the settled city before any mutation.</summary>
    /// <param name="name">The case.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void EventDetail_Case_IsTheContentIdOrNull(string name)
    {
        (ICityCommand command, string? detail) = _casesByName[name];
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
        HashSet<Type> withACase = [.. _casesByName.Values.Select(entry => entry.Command.GetType())];

        Assert.NotEmpty(commandTypes);
        Assert.Empty(commandTypes.Where(type => !withACase.Contains(type)).Select(type => type.Name));
    }
}
