using System.Reflection;
using CityBuilder.Application.Cities;
using CityBuilder.Application.Content;

namespace CityBuilder.Application.Tests;

/// <summary>
/// Guards the dependency rule for Application (core-instructions/03-architecture.md, dependency table), and that every city write goes
/// through <see cref="ICityTransactionRunner"/> (core-instructions/06-data-model.md, write transaction pattern).
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
        const BindingFlags members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        Assembly application = typeof(ICityRepository).Assembly;
        Type[] allowed = [typeof(CityTransactionRunner), typeof(CityTransaction)];

        string[] holders = application.GetTypes()
            .Where(type => !allowed.Contains(type) && !type.IsInterface)
            .Where(type =>
                type.GetConstructors(members).SelectMany(constructor => constructor.GetParameters())
                    .Any(parameter => parameter.ParameterType == typeof(ICityRepository))
                || type.GetFields(members).Any(field => field.FieldType == typeof(ICityRepository))
                || type.GetProperties(members).Any(property => property.PropertyType == typeof(ICityRepository)))
            .Select(type => type.FullName ?? type.Name)
            .ToArray();

        Assert.Empty(holders);
    }
}
