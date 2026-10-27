using System.Reflection;
using CityBuilder.Application.Cities;
using CityBuilder.Application.Content;
using CityBuilder.Application.Players;

namespace CityBuilder.Application.Tests;

/// <summary>
/// Guards the dependency rule for Application (core-instructions/03-architecture.md, dependency table), that every change of a stored city
/// goes through <see cref="ICityTransactionRunner"/> (core-instructions/06-data-model.md, write transaction pattern), and that only the
/// first login inserts a city without a lock (docs/adr/ADR-0009-row-lock-own-city.md).
/// </summary>
public sealed class ApplicationDependencyTests
{
    /// <summary>Frameworks that Application must never depend on: use cases reach HTTP and the database only through ports.</summary>
    private static readonly string[] _forbiddenAssemblyPrefixes = ["Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore"];

    /// <summary>The Application assembly references neither ASP.NET Core nor Entity Framework Core.</summary>
    [Fact]
    public void GetReferencedAssemblies_OfApplication_ContainNoAspNetCoreOrEntityFrameworkCore()
    {
        // Bound to a type, so the test inspects the assembly the build produced and fails to compile if the type moves away.
        Assembly application = typeof(IContentProvider).Assembly;

        string[] forbidden = application.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => _forbiddenAssemblyPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(forbidden);
    }

    /// <summary>
    /// Only <see cref="CityTransactionRunner"/> and its handle <see cref="CityTransaction"/> (which only the runner creates) hold an
    /// <see cref="ICityRepository"/> (a constructor parameter, a field or a property): every other Application type, every use case included,
    /// reaches a city through the runner, so none loads or writes a city without its row lock, and none locks a second city.
    /// </summary>
    [Fact]
    public void Types_OfApplication_OnlyTheRunnerDependsOnTheCityRepository()
    {
        Assert.Empty(HoldersOf(typeof(ICityRepository), typeof(CityTransactionRunner), typeof(CityTransaction)));
    }

    /// <summary>
    /// Only <see cref="DevLogin"/> holds an <see cref="INewCityRepository"/>: the insert of a new player's city, the one city write without a
    /// row lock, has one caller, and no use case can use it to write over an existing city.
    /// </summary>
    [Fact]
    public void Types_OfApplication_OnlyTheFirstLoginDependsOnTheNewCityRepository()
    {
        Assert.Empty(HoldersOf(typeof(INewCityRepository), typeof(DevLogin)));
    }

    /// <summary>
    /// The Application types, other than <paramref name="allowed"/> and interfaces, that hold a <paramref name="port"/>: a constructor
    /// parameter, a field or a property of that type.
    /// </summary>
    /// <param name="port">The port.</param>
    /// <param name="allowed">The types that may hold it.</param>
    /// <returns>The full names of the other holders; empty when there are none.</returns>
    private static string[] HoldersOf(Type port, params Type[] allowed)
    {
        const BindingFlags members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        return port.Assembly.GetTypes()
            .Where(type => !allowed.Contains(type) && !type.IsInterface)
            .Where(type =>
                type.GetConstructors(members).SelectMany(constructor => constructor.GetParameters())
                    .Any(parameter => parameter.ParameterType == port)
                || type.GetFields(members).Any(field => field.FieldType == port)
                || type.GetProperties(members).Any(property => property.PropertyType == port))
            .Select(type => type.FullName ?? type.Name)
            .ToArray();
    }
}
