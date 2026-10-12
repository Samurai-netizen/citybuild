using System.Reflection;

namespace CityBuilder.Application.Tests;

/// <summary>Guards the dependency rule for Application (core-instructions/03-architecture.md, dependency table).</summary>
public sealed class ApplicationDependencyTests
{
    // Frameworks that Application must never depend on: use cases reach HTTP and the database only through ports.
    private static readonly string[] _forbiddenAssemblyPrefixes = ["Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore"];

    [Fact]
    public void GetReferencedAssemblies_OfApplication_ContainNoAspNetCoreOrEntityFrameworkCore()
    {
        Assembly application = Assembly.Load(new AssemblyName("CityBuilder.Application"));

        string[] forbidden = application.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => _forbiddenAssemblyPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(forbidden);
    }
}
